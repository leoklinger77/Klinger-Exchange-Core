using KlingerExchange.Matching.Domain;
using KlingerExchange.Matching.Domain.Enums;
using KlingerExchange.Matching.Domain.Struct;

namespace KlingerExchange.Tests;

public class FastPriceLevelTests {
    private static Order MakeOrder(long id, decimal qty, decimal price = 25.00m) =>
        new(id, $"CLO{id:D3}", "PETR4", Side.Buy, price, qty);

    [Fact]
    public void NewLevel_IsEmpty() {
        var level = new FastPriceLevel(2500000000L);

        Assert.True(level.IsEmpty);
        Assert.Equal(0, level.OrderCount);
        Assert.Equal(0m, level.TotalQuantity);
    }

    [Fact]
    public void AddOrder_UpdatesCountAndQuantity() {
        var level = new FastPriceLevel(2500000000L);
        level.AddOrder(MakeOrder(1, 100));
        level.AddOrder(MakeOrder(2, 200));

        Assert.Equal(2, level.OrderCount);
        Assert.Equal(300m, level.TotalQuantity);
        Assert.False(level.IsEmpty);
    }

    [Fact]
    public void RemoveOrder_ByOrderId_ReducesQuantity() {
        var level = new FastPriceLevel(2500000000L);
        level.AddOrder(MakeOrder(1, 100));
        level.AddOrder(MakeOrder(2, 200));

        var removed = level.RemoveOrder(1);

        Assert.True(removed);
        Assert.Equal(1, level.OrderCount);
        Assert.Equal(200m, level.TotalQuantity);
    }

    [Fact]
    public void RemoveOrder_NonExistent_ReturnsFalse() {
        var level = new FastPriceLevel(2500000000L);
        level.AddOrder(MakeOrder(1, 100));

        var removed = level.RemoveOrder(999);

        Assert.False(removed);
        Assert.Equal(1, level.OrderCount);
    }

    [Fact]
    public void FirstOrderLeavesQty_ReturnsFirstOrderQuantity() {
        var level = new FastPriceLevel(2500000000L);
        level.AddOrder(MakeOrder(1, 100));
        level.AddOrder(MakeOrder(2, 200));

        Assert.Equal(100m, level.FirstOrderLeavesQty);
    }

    [Fact]
    public void FirstOrderLeavesQty_EmptyLevel_ReturnsZero() {
        var level = new FastPriceLevel(2500000000L);
        Assert.Equal(0m, level.FirstOrderLeavesQty);
    }

    [Fact]
    public void ApplyFillToFirst_PartialFill_ReducesLeavesQty() {
        var level = new FastPriceLevel(2500000000L);
        level.AddOrder(MakeOrder(1, 100));

        var result = level.ApplyFillToFirst(40m, out var orderId, out var removed);

        Assert.True(result);
        Assert.Equal(1, orderId);
        Assert.False(removed);
        Assert.Equal(60m, level.TotalQuantity);
        Assert.Equal(60m, level.FirstOrderLeavesQty);
    }

    [Fact]
    public void ApplyFillToFirst_CompleteFill_RemovesOrder() {
        var level = new FastPriceLevel(2500000000L);
        level.AddOrder(MakeOrder(1, 100));
        level.AddOrder(MakeOrder(2, 200));

        var result = level.ApplyFillToFirst(100m, out var orderId, out var removed);

        Assert.True(result);
        Assert.Equal(1, orderId);
        Assert.True(removed);
        Assert.Equal(1, level.OrderCount);
        Assert.Equal(200m, level.TotalQuantity);
    }

    [Fact]
    public void ApplyFillToFirst_FIFO_FillsOldestFirst() {
        var level = new FastPriceLevel(2500000000L);
        level.AddOrder(MakeOrder(1, 100));
        level.AddOrder(MakeOrder(2, 200));
        level.AddOrder(MakeOrder(3, 300));

        level.ApplyFillToFirst(100m, out var firstId, out _);
        Assert.Equal(1, firstId);

        level.ApplyFillToFirst(200m, out var secondId, out _);
        Assert.Equal(2, secondId);

        level.ApplyFillToFirst(300m, out var thirdId, out _);
        Assert.Equal(3, thirdId);

        Assert.True(level.IsEmpty);
    }

    [Fact]
    public void ApplyFillToOrder_ByOrderId_ReducesQuantity() {
        var level = new FastPriceLevel(2500000000L);
        level.AddOrder(MakeOrder(1, 100));
        level.AddOrder(MakeOrder(2, 200));

        var found = level.ApplyFillToOrder(2, 50m, out var removed);

        Assert.True(found);
        Assert.False(removed);
        Assert.Equal(250m, level.TotalQuantity);
    }

    [Fact]
    public void ApplyFillToOrder_FullFill_RemovesOrder() {
        var level = new FastPriceLevel(2500000000L);
        level.AddOrder(MakeOrder(1, 100));
        level.AddOrder(MakeOrder(2, 200));

        var found = level.ApplyFillToOrder(1, 100m, out var removed);

        Assert.True(found);
        Assert.True(removed);
        Assert.Equal(1, level.OrderCount);
    }

    [Fact]
    public void ApplyFillToOrder_NonExistent_ReturnsFalse() {
        var level = new FastPriceLevel(2500000000L);
        level.AddOrder(MakeOrder(1, 100));

        var found = level.ApplyFillToOrder(999, 50m, out var removed);

        Assert.False(found);
        Assert.False(removed);
    }
}
