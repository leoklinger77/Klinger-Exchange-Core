using KlingerExchange.Matching.Domain;
using KlingerExchange.Matching.Domain.Enums;
using KlingerExchange.Matching.Domain.Struct;

namespace KlingerExchange.Tests;

public class FastOrderBookTests {
    private static Order MakeBuy(long id, decimal price, decimal qty) =>
        new(id, $"CLO{id:D3}", "PETR4", Side.Buy, price, qty);

    private static Order MakeSell(long id, decimal price, decimal qty) =>
        new(id, $"CLO{id:D3}", "PETR4", Side.Sell, price, qty);

    // ── Price conversion ────────────────────────────────────────────
    [Theory]
    [InlineData(25.50)]
    [InlineData(0.01)]
    [InlineData(999.99)]
    public void ToFixedPrice_RoundTrips(decimal price) {
        var fixedPrice = FastOrderBook.ToFixedPrice(price);
        var back = FastOrderBook.FromFixedPrice(fixedPrice);
        Assert.Equal(price, back);
    }

    // ── Add / Remove ────────────────────────────────────────────────
    [Fact]
    public void AddOrder_ThenRemove_Succeeds() {
        var book = new FastOrderBook("PETR4");
        book.AddOrder(MakeBuy(1, 25.00m, 100));

        var (success, side) = book.RemoveOrder(1);

        Assert.True(success);
        Assert.Equal(Side.Buy, side);
    }

    [Fact]
    public void RemoveOrder_NonExistent_ReturnsFalse() {
        var book = new FastOrderBook("PETR4");

        var (success, _) = book.RemoveOrder(999);

        Assert.False(success);
    }

    [Fact]
    public void TryGetOrder_ExistingOrder_ReturnsTrue() {
        var book = new FastOrderBook("PETR4");
        book.AddOrder(MakeBuy(1, 25.00m, 100));

        var found = book.TryGetOrder(1, out var price, out var side);

        Assert.True(found);
        Assert.Equal(FastOrderBook.ToFixedPrice(25.00m), price);
        Assert.Equal(Side.Buy, side);
    }

    [Fact]
    public void TryGetOrder_NonExistent_ReturnsFalse() {
        var book = new FastOrderBook("PETR4");
        Assert.False(book.TryGetOrder(999, out _, out _));
    }

    // ── Best bid/ask (Top of Book) ──────────────────────────────────
    [Fact]
    public void GetTopOfBook_EmptyBook_ReturnsZeros() {
        var book = new FastOrderBook("PETR4");
        var (bid, bidQty, ask, askQty) = book.GetTopOfBook();

        Assert.Equal(0m, bid);
        Assert.Equal(0m, bidQty);
        Assert.Equal(0m, ask);
        Assert.Equal(0m, askQty);
    }

    [Fact]
    public void GetTopOfBook_WithOrders_ReturnsBestPrices() {
        var book = new FastOrderBook("PETR4");
        book.AddOrder(MakeBuy(1, 24.00m, 100));
        book.AddOrder(MakeBuy(2, 25.00m, 200));
        book.AddOrder(MakeSell(3, 26.00m, 300));
        book.AddOrder(MakeSell(4, 27.00m, 400));

        var (bid, bidQty, ask, askQty) = book.GetTopOfBook();

        Assert.Equal(25.00m, bid);   // Best (highest) bid
        Assert.Equal(200m, bidQty);
        Assert.Equal(26.00m, ask);   // Best (lowest) ask
        Assert.Equal(300m, askQty);
    }

    [Fact]
    public void Bids_SortedDescending_HighestFirst() {
        var book = new FastOrderBook("PETR4");
        book.AddOrder(MakeBuy(1, 23.00m, 100));
        book.AddOrder(MakeBuy(2, 25.00m, 200));
        book.AddOrder(MakeBuy(3, 24.00m, 300));

        var (bestBid, _, _, _) = book.GetTopOfBook();
        Assert.Equal(25.00m, bestBid);
    }

    [Fact]
    public void Asks_SortedAscending_LowestFirst() {
        var book = new FastOrderBook("PETR4");
        book.AddOrder(MakeSell(1, 28.00m, 100));
        book.AddOrder(MakeSell(2, 26.00m, 200));
        book.AddOrder(MakeSell(3, 27.00m, 300));

        var (_, _, bestAsk, _) = book.GetTopOfBook();
        Assert.Equal(26.00m, bestAsk);
    }

    // ── Matching ────────────────────────────────────────────────────
    [Fact]
    public void MatchOrder_BuyAgainstSells_MatchesAtBestPrice() {
        var book = new FastOrderBook("PETR4");
        book.AddOrder(MakeSell(1, 26.00m, 100));
        book.AddOrder(MakeSell(2, 27.00m, 200));

        var remaining = 100m;
        var matches = new List<MatchResult>();
        book.MatchOrder(Side.Buy, 27.00m, ref remaining, matches);

        Assert.Single(matches);
        Assert.Equal(1, matches[0].CounterOrderId);
        Assert.Equal(26.00m, matches[0].Price);
        Assert.Equal(100m, matches[0].Quantity);
        Assert.Equal(0m, remaining);
    }

    [Fact]
    public void MatchOrder_SellAgainstBids_MatchesAtBestPrice() {
        var book = new FastOrderBook("PETR4");
        book.AddOrder(MakeBuy(1, 25.00m, 100));
        book.AddOrder(MakeBuy(2, 24.00m, 200));

        var remaining = 100m;
        var matches = new List<MatchResult>();
        book.MatchOrder(Side.Sell, 24.00m, ref remaining, matches);

        Assert.Single(matches);
        Assert.Equal(1, matches[0].CounterOrderId);
        Assert.Equal(25.00m, matches[0].Price);
        Assert.Equal(100m, matches[0].Quantity);
        Assert.Equal(0m, remaining);
    }

    [Fact]
    public void MatchOrder_NoMatch_WhenPriceDoesNotCross() {
        var book = new FastOrderBook("PETR4");
        book.AddOrder(MakeSell(1, 30.00m, 100));

        var remaining = 100m;
        var matches = new List<MatchResult>();
        book.MatchOrder(Side.Buy, 25.00m, ref remaining, matches);

        Assert.Empty(matches);
        Assert.Equal(100m, remaining);
    }

    [Fact]
    public void MatchOrder_PartialFill_LeavesResidualInBook() {
        var book = new FastOrderBook("PETR4");
        book.AddOrder(MakeSell(1, 26.00m, 50));

        var remaining = 100m;
        var matches = new List<MatchResult>();
        book.MatchOrder(Side.Buy, 26.00m, ref remaining, matches);

        Assert.Single(matches);
        Assert.Equal(50m, matches[0].Quantity);
        Assert.Equal(50m, remaining);
    }

    [Fact]
    public void MatchOrder_MultipleCounterOrders_FillsInPriceTimePriority() {
        var book = new FastOrderBook("PETR4");
        book.AddOrder(MakeSell(1, 26.00m, 50));   // Best price, first
        book.AddOrder(MakeSell(2, 26.00m, 50));   // Same price, second (time priority)
        book.AddOrder(MakeSell(3, 27.00m, 100));  // Worse price

        var remaining = 120m;
        var matches = new List<MatchResult>();
        book.MatchOrder(Side.Buy, 27.00m, ref remaining, matches);

        Assert.Equal(3, matches.Count);
        Assert.Equal(1, matches[0].CounterOrderId);   // First at 26
        Assert.Equal(50m, matches[0].Quantity);
        Assert.Equal(2, matches[1].CounterOrderId);   // Second at 26
        Assert.Equal(50m, matches[1].Quantity);
        Assert.Equal(3, matches[2].CounterOrderId);   // Third at 27
        Assert.Equal(20m, matches[2].Quantity);
        Assert.Equal(0m, remaining);
    }

    // ── Replace ─────────────────────────────────────────────────────
    [Fact]
    public void ReplaceOrder_ExistingOrder_RemovesFromBook() {
        var book = new FastOrderBook("PETR4");
        book.AddOrder(MakeBuy(1, 25.00m, 100));

        var (success, _) = book.ReplaceOrder(1, 26.00m, 200m);

        Assert.True(success);
        // Order was removed; caller re-adds with new params (tested via MatchingEngine)
    }

    [Fact]
    public void ReplaceOrder_NonExistent_ReturnsFalse() {
        var book = new FastOrderBook("PETR4");
        var (success, _) = book.ReplaceOrder(999, 26.00m, 200m);
        Assert.False(success);
    }

    // ── ApplyFillToOrder (recovery path) ────────────────────────────
    [Fact]
    public void ApplyFillToOrder_ReducesQuantity() {
        var book = new FastOrderBook("PETR4");
        book.AddOrder(MakeBuy(1, 25.00m, 100));

        var applied = book.ApplyFillToOrder(1, 40m);

        Assert.True(applied);
        var (bid, bidQty, _, _) = book.GetTopOfBook();
        Assert.Equal(25.00m, bid);
        Assert.Equal(60m, bidQty);
    }

    [Fact]
    public void ApplyFillToOrder_FullFill_RemovesFromBook() {
        var book = new FastOrderBook("PETR4");
        book.AddOrder(MakeBuy(1, 25.00m, 100));

        var applied = book.ApplyFillToOrder(1, 100m);

        Assert.True(applied);
        var (bid, _, _, _) = book.GetTopOfBook();
        Assert.Equal(0m, bid);
    }

    [Fact]
    public void ApplyFillToOrder_NonExistent_ReturnsFalse() {
        var book = new FastOrderBook("PETR4");
        Assert.False(book.ApplyFillToOrder(999, 50m));
    }

    // ── GetAllOrderIds ──────────────────────────────────────────────
    [Fact]
    public void GetAllOrderIds_ReturnsAllActiveOrders() {
        var book = new FastOrderBook("PETR4");
        book.AddOrder(MakeBuy(1, 25.00m, 100));
        book.AddOrder(MakeSell(2, 26.00m, 200));

        var ids = book.GetAllOrderIds().OrderBy(id => id).ToList();
        Assert.Equal(new long[] { 1, 2 }, ids);
    }
}
