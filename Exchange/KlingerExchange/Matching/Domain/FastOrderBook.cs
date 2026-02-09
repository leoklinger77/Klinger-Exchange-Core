using KlingerExchange.Matching.Domain.Enums;
using KlingerExchange.Matching.Domain.Struct;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace KlingerExchange.Matching.Domain;

/// <summary>
/// High-performance order book using long (fixed-point) prices.
/// Target: &lt;50µs for match operations.
/// </summary>
public sealed class FastOrderBook {
    // Price multiplier: 100_000_000 gives 8 decimal places precision
    private const long PRICE_MULTIPLIER = 100_000_000L;

    private readonly SortedList<long, FastPriceLevel> _bids;
    private readonly SortedList<long, FastPriceLevel> _asks;
    private readonly ConcurrentDictionary<long, (long PriceFixed, Side Side)> _orderIndex;

    public string Symbol { get; }

    public FastOrderBook(string symbol) {
        Symbol = symbol;
        // Bids: highest price first (descending)
        _bids = new SortedList<long, FastPriceLevel>(Comparer<long>.Create((a, b) => b.CompareTo(a)));
        // Asks: lowest price first (ascending)
        _asks = new SortedList<long, FastPriceLevel>();
        _orderIndex = new ConcurrentDictionary<long, (long, Side)>();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long ToFixedPrice(decimal price) => (long)(price * PRICE_MULTIPLIER);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static decimal FromFixedPrice(long priceFixed) => (decimal)priceFixed / PRICE_MULTIPLIER;

    public void AddOrder(Order order) {
        var priceFixed = ToFixedPrice(order.Price);
        var book = order.Side == Side.Buy ? _bids : _asks;

        if (!book.TryGetValue(priceFixed, out var level)) {
            level = new FastPriceLevel(priceFixed);
            book[priceFixed] = level;
        }

        level.AddOrder(order);
        _orderIndex[order.OrderId] = (priceFixed, order.Side);
    }

    public (bool success, Side side) RemoveOrder(long orderId) {
        if (!_orderIndex.TryRemove(orderId, out var location))
            return (false, Side.Buy);

        var book = location.Side == Side.Buy ? _bids : _asks;
        if (!book.TryGetValue(location.PriceFixed, out var level))
            return (false, location.Side);

        var removed = level.RemoveOrder(orderId);
        if (removed && level.IsEmpty)
            book.Remove(location.PriceFixed);

        return (removed, location.Side);
    }

    public bool TryGetOrder(long orderId, out long priceFixed, out Side side) {
        if (_orderIndex.TryGetValue(orderId, out var location)) {
            priceFixed = location.PriceFixed;
            side = location.Side;
            return true;
        }
        priceFixed = 0;
        side = Side.Buy;
        return false;
    }

    public (bool success, Order? oldOrder) ReplaceOrder(long orderId, decimal? newPrice, decimal? newQuantity) {
        if (!_orderIndex.TryGetValue(orderId, out var location))
            return (false, null);

        var book = location.Side == Side.Buy ? _bids : _asks;
        if (!book.TryGetValue(location.PriceFixed, out var level))
            return (false, null);

        // Remove old order from current price level
        var removed = level.RemoveOrder(orderId);
        if (!removed)
            return (false, null);

        if (level.IsEmpty)
            book.Remove(location.PriceFixed);

        // Remove from index (will be re-added with new price)
        _orderIndex.TryRemove(orderId, out _);

        return (true, null);
    }

    /// <summary>
    /// Match incoming order against the book. Single pass, no repeated lookups.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void MatchOrder(Side incomingSide, decimal limitPrice, ref decimal remainingQty, List<MatchResult> matches) {
        var limitPriceFixed = ToFixedPrice(limitPrice);
        var book = incomingSide == Side.Buy ? _asks : _bids;

        while (remainingQty > 0 && book.Count > 0) {
            var bestLevel = book.Values[0];
            var bestPriceFixed = book.Keys[0];

            // Price check
            if (incomingSide == Side.Buy) {
                if (bestPriceFixed > limitPriceFixed)
                    break;
            } else {
                if (bestPriceFixed < limitPriceFixed)
                    break;
            }

            if (bestLevel.OrderCount == 0) {
                book.RemoveAt(0);
                continue;
            }

            var fillQty = Math.Min(remainingQty, bestLevel.FirstOrderLeavesQty);

            if (!bestLevel.ApplyFillToFirst(fillQty, out var originalOrderId, out var removed))
                break;

            matches.Add(new MatchResult {
                CounterOrderId = originalOrderId,
                Price = FromFixedPrice(bestPriceFixed),
                Quantity = fillQty
            });

            remainingQty -= fillQty;

            if (removed) {
                _orderIndex.TryRemove(originalOrderId, out _);
            }

            if (bestLevel.IsEmpty) {
                book.RemoveAt(0);
            }
        }
    }

    public (decimal BidPrice, decimal BidQty, decimal AskPrice, decimal AskQty) GetTopOfBook() {
        var bestBid = _bids.Count > 0 ? _bids.Values[0] : null;
        var bestAsk = _asks.Count > 0 ? _asks.Values[0] : null;

        return (
            bestBid != null ? FromFixedPrice(_bids.Keys[0]) : 0,
            bestBid?.TotalQuantity ?? 0,
            bestAsk != null ? FromFixedPrice(_asks.Keys[0]) : 0,
            bestAsk?.TotalQuantity ?? 0
        );
    }

    public IEnumerable<long> GetAllOrderIds() => _orderIndex.Keys;
}
