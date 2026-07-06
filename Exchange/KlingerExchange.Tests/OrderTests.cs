using KlingerExchange.Matching.Domain;
using KlingerExchange.Matching.Domain.Enums;
using KlingerExchange.Matching.Domain.Struct;

namespace KlingerExchange.Tests;

public class OrderTests {
    [Fact]
    public void Constructor_SetsDefaultValues() {
        var order = new Order(1, "CLO001", "PETR4", Side.Buy, 25.50m, 100m);

        Assert.Equal(1, order.OrderId);
        Assert.Equal("CLO001", order.ClOrdId);
        Assert.Equal("PETR4", order.Symbol);
        Assert.Equal(Side.Buy, order.Side);
        Assert.Equal(25.50m, order.Price);
        Assert.Equal(100m, order.Quantity);
        Assert.Equal(0m, order.FilledQty);
        Assert.Equal(OrderStatus.New, order.Status);
    }

    [Fact]
    public void LeavesQty_ReturnsRemainingQuantity() {
        var order = new Order(1, "CLO001", "PETR4", Side.Buy, 25.50m, 100m);
        Assert.Equal(100m, order.LeavesQty);

        var partially = order.WithFill(40m);
        Assert.Equal(60m, partially.LeavesQty);
    }

    [Fact]
    public void IsFilled_ReturnsFalseForNewOrder() {
        var order = new Order(1, "CLO001", "PETR4", Side.Buy, 25.50m, 100m);
        Assert.False(order.IsFilled);
    }

    [Fact]
    public void WithFill_PartialFill_SetsPartiallyFilledStatus() {
        var order = new Order(1, "CLO001", "PETR4", Side.Buy, 25.50m, 100m);
        var filled = order.WithFill(40m);

        Assert.Equal(40m, filled.FilledQty);
        Assert.Equal(60m, filled.LeavesQty);
        Assert.Equal(OrderStatus.PartiallyFilled, filled.Status);
        Assert.False(filled.IsFilled);
    }

    [Fact]
    public void WithFill_CompleteFill_SetsFilledStatus() {
        var order = new Order(1, "CLO001", "PETR4", Side.Buy, 25.50m, 100m);
        var filled = order.WithFill(100m);

        Assert.Equal(100m, filled.FilledQty);
        Assert.Equal(0m, filled.LeavesQty);
        Assert.Equal(OrderStatus.Filled, filled.Status);
        Assert.True(filled.IsFilled);
    }

    [Fact]
    public void WithFill_MultipleFills_AccumulatesCorrectly() {
        var order = new Order(1, "CLO001", "PETR4", Side.Buy, 25.50m, 100m);
        var first = order.WithFill(30m);
        var second = first.WithFill(70m);

        Assert.Equal(100m, second.FilledQty);
        Assert.True(second.IsFilled);
        Assert.Equal(OrderStatus.Filled, second.Status);
    }

    [Fact]
    public void WithStatus_PreservesAllFieldsExceptStatus() {
        var order = new Order(1, "CLO001", "PETR4", Side.Buy, 25.50m, 100m);
        var cancelled = order.WithStatus(OrderStatus.Canceled);

        Assert.Equal(1, cancelled.OrderId);
        Assert.Equal("CLO001", cancelled.ClOrdId);
        Assert.Equal("PETR4", cancelled.Symbol);
        Assert.Equal(25.50m, cancelled.Price);
        Assert.Equal(100m, cancelled.Quantity);
        Assert.Equal(OrderStatus.Canceled, cancelled.Status);
    }

    [Fact]
    public void WithFill_PreservesImmutability() {
        var original = new Order(1, "CLO001", "PETR4", Side.Buy, 25.50m, 100m);
        var filled = original.WithFill(50m);

        Assert.Equal(0m, original.FilledQty);
        Assert.Equal(OrderStatus.New, original.Status);
        Assert.Equal(50m, filled.FilledQty);
    }
}
