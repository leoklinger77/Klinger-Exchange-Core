using KlingerExchange.EventStore;
using KlingerExchange.EventStore.StructModels;
using KlingerExchange.Matching.Domain.Enums;
using KlingerExchange.Matching.Engine.Repository;

namespace KlingerExchange.Tests;

public class OrderBookRebuilderTests {
    private static long _seq;

    private static (EventHeader, object) Accepted(long orderId, string symbol, byte side, decimal price, decimal qty) =>
        (new EventHeader(++_seq, EventType.OrderAccepted, DateTime.UtcNow.Ticks, 0),
         new OrderAcceptedEvent(orderId, $"CLO{orderId:D3}", symbol, side, price, qty, DateTime.UtcNow.Ticks));

    private static (EventHeader, object) Trade(long buyId, long sellId, string symbol, decimal price, decimal qty) =>
        (new EventHeader(++_seq, EventType.Trade, DateTime.UtcNow.Ticks, 0),
         new TradeEvent(buyId, sellId, symbol, price, qty, DateTime.UtcNow.Ticks));

    private static (EventHeader, object) Filled(long orderId) =>
        (new EventHeader(++_seq, EventType.OrderFilled, DateTime.UtcNow.Ticks, 0),
         new OrderFilledEvent(orderId, $"CLO{orderId:D3}", 0m, DateTime.UtcNow.Ticks));

    private static (EventHeader, object) Cancelled(long orderId, string symbol) =>
        (new EventHeader(++_seq, EventType.OrderCancelled, DateTime.UtcNow.Ticks, 0),
         new OrderCancelledEvent(orderId, $"CLO{orderId:D3}", symbol, DateTime.UtcNow.Ticks));

    [Fact]
    public void Replay_OrderAccepted_RebuildsOrderInBook() {
        _seq = 0;
        var repo = new OrderBookRepository();
        var rebuilder = new OrderBookRebuilder(repo);

        rebuilder.Replay(new[] {
            Accepted(1, "PETR4", 1, 25.00m, 100m)
        });

        Assert.True(repo.TryGetBook("PETR4", out var book));
        var (bid, bidQty, _, _) = book!.GetTopOfBook();
        Assert.Equal(25.00m, bid);
        Assert.Equal(100m, bidQty);
        Assert.Equal(1, rebuilder.OrdersRebuilt);
    }

    [Fact]
    public void Replay_OrderCancelled_RemovesFromBook() {
        _seq = 0;
        var repo = new OrderBookRepository();
        var rebuilder = new OrderBookRebuilder(repo);

        rebuilder.Replay(new[] {
            Accepted(1, "PETR4", 1, 25.00m, 100m),
            Cancelled(1, "PETR4")
        });

        Assert.True(repo.TryGetBook("PETR4", out var book));
        var (bid, _, _, _) = book!.GetTopOfBook();
        Assert.Equal(0m, bid);
    }

    [Fact]
    public void Replay_TradeEvent_ReducesOrderQuantity() {
        _seq = 0;
        var repo = new OrderBookRepository();
        var rebuilder = new OrderBookRebuilder(repo);

        // Two resting orders, then a trade between them
        rebuilder.Replay(new[] {
            Accepted(1, "PETR4", 1, 25.00m, 100m),   // Buy 100
            Accepted(2, "PETR4", 2, 25.00m, 100m),   // Sell 100
            Trade(1, 2, "PETR4", 25.00m, 40m)         // Trade 40
        });

        Assert.True(repo.TryGetBook("PETR4", out var book));
        var (bid, bidQty, ask, askQty) = book!.GetTopOfBook();

        Assert.Equal(25.00m, bid);
        Assert.Equal(60m, bidQty);     // 100 - 40
        Assert.Equal(25.00m, ask);
        Assert.Equal(60m, askQty);     // 100 - 40
        Assert.Equal(1, rebuilder.TradesApplied);
    }

    [Fact]
    public void Replay_FullTrade_RemovesBothOrders() {
        _seq = 0;
        var repo = new OrderBookRepository();
        var rebuilder = new OrderBookRebuilder(repo);

        rebuilder.Replay(new[] {
            Accepted(1, "PETR4", 1, 25.00m, 100m),
            Accepted(2, "PETR4", 2, 25.00m, 100m),
            Trade(1, 2, "PETR4", 25.00m, 100m),
            Filled(1),
            Filled(2)
        });

        Assert.True(repo.TryGetBook("PETR4", out var book));
        var (bid, _, ask, _) = book!.GetTopOfBook();
        Assert.Equal(0m, bid);
        Assert.Equal(0m, ask);
    }

    [Fact]
    public void Replay_MultipleTrades_AccumulatesFills() {
        _seq = 0;
        var repo = new OrderBookRepository();
        var rebuilder = new OrderBookRebuilder(repo);

        rebuilder.Replay(new[] {
            Accepted(1, "PETR4", 1, 25.00m, 100m),
            Accepted(2, "PETR4", 2, 25.00m, 100m),
            Trade(1, 2, "PETR4", 25.00m, 30m),
            Trade(1, 2, "PETR4", 25.00m, 30m),
            Trade(1, 2, "PETR4", 25.00m, 40m),
            Filled(1),
            Filled(2)
        });

        Assert.True(repo.TryGetBook("PETR4", out var book));
        var (bid, _, ask, _) = book!.GetTopOfBook();
        Assert.Equal(0m, bid);
        Assert.Equal(0m, ask);
        Assert.Equal(3, rebuilder.TradesApplied);
    }

    [Fact]
    public void Replay_MultipleSymbols_RebuildsCorrectBooks() {
        _seq = 0;
        var repo = new OrderBookRepository();
        var rebuilder = new OrderBookRebuilder(repo);

        rebuilder.Replay(new[] {
            Accepted(1, "PETR4", 1, 25.00m, 100m),
            Accepted(2, "VALE3", 2, 60.00m, 200m)
        });

        Assert.True(repo.TryGetBook("PETR4", out var petr));
        Assert.True(repo.TryGetBook("VALE3", out var vale));

        var (petrBid, _, _, _) = petr!.GetTopOfBook();
        var (_, _, valeAsk, _) = vale!.GetTopOfBook();

        Assert.Equal(25.00m, petrBid);
        Assert.Equal(60.00m, valeAsk);
    }

    [Fact]
    public void Replay_ComplexScenario_AcceptTradeCancelSequence() {
        _seq = 0;
        var repo = new OrderBookRepository();
        var rebuilder = new OrderBookRebuilder(repo);

        rebuilder.Replay(new[] {
            Accepted(1, "PETR4", 1, 25.00m, 200m),   // Buy 200
            Accepted(2, "PETR4", 2, 25.00m, 100m),   // Sell 100
            Trade(1, 2, "PETR4", 25.00m, 100m),       // Full match on sell, partial on buy
            Filled(2),                                  // Sell fully filled -> removed
            Accepted(3, "PETR4", 1, 24.00m, 300m),   // Another buy at lower price
            Cancelled(3, "PETR4")                      // Cancel it
        });

        Assert.True(repo.TryGetBook("PETR4", out var book));
        var (bid, bidQty, ask, _) = book!.GetTopOfBook();

        Assert.Equal(25.00m, bid);
        Assert.Equal(100m, bidQty);   // 200 - 100 trade
        Assert.Equal(0m, ask);         // Sell was fully filled, no asks remaining
        Assert.Equal(5, rebuilder.EventsProcessed);
    }
}
