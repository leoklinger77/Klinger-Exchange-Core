using KlingerExchange.Matching.Domain;
using KlingerExchange.Matching.Domain.Enums;
using KlingerExchange.Matching.Engine;
using KlingerExchange.Matching.Engine.Repository;

namespace KlingerExchange.Tests;

public class MatchingEngineTests {
    private static MatchingEngine CreateEngine() {
        var repo = new OrderBookRepository();
        return new MatchingEngine(repo);
    }

    // ── New Order ───────────────────────────────────────────────────
    [Fact]
    public void ProcessNewOrder_NoCounterOrders_AddsToBook() {
        var engine = CreateEngine();

        var (order, fills) = engine.ProcessNewOrder("CLO001", "PETR4", Side.Buy, 25.00m, 100m);

        Assert.Equal("CLO001", order.ClOrdId);
        Assert.Equal("PETR4", order.Symbol);
        Assert.Equal(Side.Buy, order.Side);
        Assert.Equal(25.00m, order.Price);
        Assert.Equal(100m, order.Quantity);
        Assert.Equal(OrderStatus.New, order.Status);
        Assert.Empty(fills);
    }

    [Fact]
    public void ProcessNewOrder_AssignsUniqueOrderIds() {
        var engine = CreateEngine();

        var (order1, _) = engine.ProcessNewOrder("CLO001", "PETR4", Side.Buy, 25.00m, 100m);
        var (order2, _) = engine.ProcessNewOrder("CLO002", "PETR4", Side.Buy, 25.00m, 100m);

        Assert.NotEqual(order1.OrderId, order2.OrderId);
        Assert.True(order2.OrderId > order1.OrderId);
    }

    [Fact]
    public void ProcessNewOrder_MatchesCrossingOrders_ProducesFills() {
        var engine = CreateEngine();

        // Resting sell order
        engine.ProcessNewOrder("SELL001", "PETR4", Side.Sell, 25.00m, 100m);

        // Incoming buy that crosses the sell
        var (order, fills) = engine.ProcessNewOrder("BUY001", "PETR4", Side.Buy, 25.00m, 100m);

        Assert.Single(fills);
        Assert.Equal(25.00m, fills[0].Price);
        Assert.Equal(100m, fills[0].Quantity);
        Assert.True(order.IsFilled);
        Assert.Equal(OrderStatus.Filled, order.Status);
    }

    [Fact]
    public void ProcessNewOrder_PartialMatch_OrderRemainsInBook() {
        var engine = CreateEngine();

        // Resting sell for 50
        engine.ProcessNewOrder("SELL001", "PETR4", Side.Sell, 25.00m, 50m);

        // Incoming buy for 100 — matches 50, 50 remains
        var (order, fills) = engine.ProcessNewOrder("BUY001", "PETR4", Side.Buy, 25.00m, 100m);

        Assert.Single(fills);
        Assert.Equal(50m, fills[0].Quantity);
        Assert.Equal(OrderStatus.PartiallyFilled, order.Status);
        Assert.Equal(50m, order.LeavesQty);
    }

    [Fact]
    public void ProcessNewOrder_MultipleResting_MatchesInPriceTimePriority() {
        var engine = CreateEngine();

        engine.ProcessNewOrder("SELL001", "PETR4", Side.Sell, 25.00m, 100m);
        engine.ProcessNewOrder("SELL002", "PETR4", Side.Sell, 25.00m, 100m);
        engine.ProcessNewOrder("SELL003", "PETR4", Side.Sell, 26.00m, 100m);

        var (order, fills) = engine.ProcessNewOrder("BUY001", "PETR4", Side.Buy, 26.00m, 250m);

        // Should match: SELL001 (100@25), SELL002 (100@25), SELL003 (50@26)
        Assert.Equal(3, fills.Count);
        Assert.Equal(25.00m, fills[0].Price);
        Assert.Equal(100m, fills[0].Quantity);
        Assert.Equal(25.00m, fills[1].Price);
        Assert.Equal(100m, fills[1].Quantity);
        Assert.Equal(26.00m, fills[2].Price);
        Assert.Equal(50m, fills[2].Quantity);
        Assert.Equal(OrderStatus.Filled, order.Status);
    }

    [Fact]
    public void ProcessNewOrder_NoMatch_WhenPriceDoesNotCross() {
        var engine = CreateEngine();

        engine.ProcessNewOrder("SELL001", "PETR4", Side.Sell, 30.00m, 100m);
        var (order, fills) = engine.ProcessNewOrder("BUY001", "PETR4", Side.Buy, 25.00m, 100m);

        Assert.Empty(fills);
        Assert.Equal(OrderStatus.New, order.Status);
    }

    [Fact]
    public void ProcessNewOrder_DifferentSymbols_DoNotMatch() {
        var engine = CreateEngine();

        engine.ProcessNewOrder("SELL001", "VALE3", Side.Sell, 25.00m, 100m);
        var (order, fills) = engine.ProcessNewOrder("BUY001", "PETR4", Side.Buy, 25.00m, 100m);

        Assert.Empty(fills);
    }

    // ── Cancel ──────────────────────────────────────────────────────
    [Fact]
    public void ProcessCancel_ExistingOrder_ReturnsSuccess() {
        var engine = CreateEngine();
        var (order, _) = engine.ProcessNewOrder("CLO001", "PETR4", Side.Buy, 25.00m, 100m);

        var (success, side) = engine.ProcessCancel("PETR4", order.OrderId);

        Assert.True(success);
        Assert.Equal(Side.Buy, side);
    }

    [Fact]
    public void ProcessCancel_NonExistentOrder_ReturnsFalse() {
        var engine = CreateEngine();

        var (success, _) = engine.ProcessCancel("PETR4", 999);

        Assert.False(success);
    }

    [Fact]
    public void ProcessCancel_CancelledOrder_DoesNotMatch() {
        var engine = CreateEngine();

        var (sellOrder, _) = engine.ProcessNewOrder("SELL001", "PETR4", Side.Sell, 25.00m, 100m);
        engine.ProcessCancel("PETR4", sellOrder.OrderId);

        var (buyOrder, fills) = engine.ProcessNewOrder("BUY001", "PETR4", Side.Buy, 25.00m, 100m);
        Assert.Empty(fills);
    }

    // ── Replace ─────────────────────────────────────────────────────
    [Fact]
    public void ProcessReplace_UpdatesPrice() {
        var engine = CreateEngine();
        var (order, _) = engine.ProcessNewOrder("CLO001", "PETR4", Side.Buy, 25.00m, 100m);

        var (success, newOrder) = engine.ProcessReplace(order.OrderId, "PETR4", "CLO002", 26.00m, null);

        Assert.True(success);
        Assert.Equal(26.00m, newOrder.Price);
        Assert.Equal(100m, newOrder.Quantity);
    }

    [Fact]
    public void ProcessReplace_UpdatesQuantity() {
        var engine = CreateEngine();
        var (order, _) = engine.ProcessNewOrder("CLO001", "PETR4", Side.Buy, 25.00m, 100m);

        var (success, newOrder) = engine.ProcessReplace(order.OrderId, "PETR4", "CLO002", null, 200m);

        Assert.True(success);
        Assert.Equal(25.00m, newOrder.Price);
        Assert.Equal(200m, newOrder.Quantity);
    }

    [Fact]
    public void ProcessReplace_NonExistentOrder_ReturnsFalse() {
        var engine = CreateEngine();

        var (success, _) = engine.ProcessReplace(999, "PETR4", "CLO002", 26.00m, null);

        Assert.False(success);
    }

    [Fact]
    public void ProcessReplace_ReplacedOrder_StillMatchable() {
        var engine = CreateEngine();

        var (sellOrder, _) = engine.ProcessNewOrder("SELL001", "PETR4", Side.Sell, 26.00m, 100m);
        engine.ProcessReplace(sellOrder.OrderId, "PETR4", "SELL002", 25.00m, null);

        // Buy at 25 should now match the replaced sell at 25
        var (buyOrder, fills) = engine.ProcessNewOrder("BUY001", "PETR4", Side.Buy, 25.00m, 100m);
        Assert.Single(fills);
        Assert.Equal(25.00m, fills[0].Price);
    }

    // ── GetOrderInfo ────────────────────────────────────────────────
    [Fact]
    public void GetOrderInfo_ExistingOrder_Found() {
        var engine = CreateEngine();
        var (order, _) = engine.ProcessNewOrder("CLO001", "PETR4", Side.Buy, 25.00m, 100m);

        var (found, info) = engine.GetOrderInfo(order.OrderId);

        Assert.True(found);
        Assert.Equal("CLO001", info.ClOrdId);
    }

    [Fact]
    public void GetOrderInfo_NonExistent_NotFound() {
        var engine = CreateEngine();
        var (found, _) = engine.GetOrderInfo(999);
        Assert.False(found);
    }

    // ── Fill IDs (buy/sell assignment) ──────────────────────────────
    [Fact]
    public void Fill_BuyOrderId_And_SellOrderId_CorrectlyAssigned() {
        var engine = CreateEngine();

        var (sellOrder, _) = engine.ProcessNewOrder("SELL001", "PETR4", Side.Sell, 25.00m, 100m);
        var (buyOrder, fills) = engine.ProcessNewOrder("BUY001", "PETR4", Side.Buy, 25.00m, 100m);

        Assert.Single(fills);
        Assert.Equal(buyOrder.OrderId, fills[0].BuyOrderId);
        Assert.Equal(sellOrder.OrderId, fills[0].SellOrderId);
    }
}
