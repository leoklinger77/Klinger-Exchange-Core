using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace KlingerExchange.Matching.Domain;

/// <summary>
/// High-performance order book using long (fixed-point) prices.
/// Target: &lt;50µs for match operations.
/// </summary>
public sealed class FastOrderBook
{
    // Price multiplier: 100_000_000 gives 8 decimal places precision
    private const long PRICE_MULTIPLIER = 100_000_000L;

    private readonly SortedList<long, FastPriceLevel> _bids;
    private readonly SortedList<long, FastPriceLevel> _asks;
    private readonly ConcurrentDictionary<long, (long PriceFixed, Side Side)> _orderIndex;

    public string Symbol { get; }

    public FastOrderBook(string symbol)
    {
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

    public void AddOrder(Order order)
    {
        var priceFixed = ToFixedPrice(order.Price);
        var book = order.Side == Side.Buy ? _bids : _asks;

        if (!book.TryGetValue(priceFixed, out var level))
        {
            level = new FastPriceLevel(priceFixed);
            book[priceFixed] = level;
        }

        level.AddOrder(order);
        _orderIndex[order.OrderId] = (priceFixed, order.Side);
    }

    public bool RemoveOrder(long orderId)
    {
        if (!_orderIndex.TryRemove(orderId, out var location))
            return false;

        var book = location.Side == Side.Buy ? _bids : _asks;
        if (!book.TryGetValue(location.PriceFixed, out var level))
            return false;

        var removed = level.RemoveOrder(orderId);
        if (removed && level.IsEmpty)
            book.Remove(location.PriceFixed);

        return removed;
    }

    /// <summary>
    /// Match incoming order against the book. Single pass, no repeated lookups.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void MatchOrder(Side incomingSide, decimal limitPrice, ref decimal remainingQty, List<MatchResult> matches)
    {
        var limitPriceFixed = ToFixedPrice(limitPrice);
        var book = incomingSide == Side.Buy ? _asks : _bids;

        while (remainingQty > 0 && book.Count > 0)
        {
            var bestLevel = book.Values[0];
            var bestPriceFixed = book.Keys[0];

            // Price check
            if (incomingSide == Side.Buy)
            {
                if (bestPriceFixed > limitPriceFixed)
                    break;
            }
            else
            {
                if (bestPriceFixed < limitPriceFixed)
                    break;
            }

            if (bestLevel.OrderCount == 0)
            {
                book.RemoveAt(0);
                continue;
            }

            var fillQty = Math.Min(remainingQty, bestLevel.FirstOrderLeavesQty);

            if (!bestLevel.ApplyFillToFirst(fillQty, out var originalOrderId, out var removed))
                break;

            matches.Add(new MatchResult
            {
                CounterOrderId = originalOrderId,
                Price = FromFixedPrice(bestPriceFixed),
                Quantity = fillQty
            });

            remainingQty -= fillQty;

            if (removed)
            {
                _orderIndex.TryRemove(originalOrderId, out _);
            }

            if (bestLevel.IsEmpty)
            {
                book.RemoveAt(0);
            }
        }
    }

    public (decimal BidPrice, decimal BidQty, decimal AskPrice, decimal AskQty) GetTopOfBook()
    {
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

/// <summary>
/// Price level optimized for fast first-order access.
/// Uses array-backed list instead of LinkedList for cache locality.
/// </summary>
public sealed class FastPriceLevel
{
    private readonly List<FastOrder> _orders;
    public long PriceFixed { get; }
    public decimal TotalQuantity { get; private set; }

    public FastPriceLevel(long priceFixed)
    {
        PriceFixed = priceFixed;
        _orders = new List<FastOrder>(4); // Pre-allocate for common case
        TotalQuantity = 0;
    }

    public int OrderCount => _orders.Count;
    public bool IsEmpty => _orders.Count == 0;

    public decimal FirstOrderLeavesQty => _orders.Count > 0 ? _orders[0].LeavesQty : 0;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AddOrder(Order order)
    {
        _orders.Add(new FastOrder(order.OrderId, order.LeavesQty));
        TotalQuantity += order.LeavesQty;
    }

    public bool RemoveOrder(long orderId)
    {
        for (int i = 0; i < _orders.Count; i++)
        {
            if (_orders[i].OrderId == orderId)
            {
                TotalQuantity -= _orders[i].LeavesQty;
                _orders.RemoveAt(i);
                return true;
            }
        }
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool ApplyFillToFirst(decimal fillQty, out long originalOrderId, out bool removed)
    {
        if (_orders.Count == 0)
        {
            originalOrderId = 0;
            removed = false;
            return false;
        }

        var first = _orders[0];
        originalOrderId = first.OrderId;
        TotalQuantity -= fillQty;

        var newLeavesQty = first.LeavesQty - fillQty;

        if (newLeavesQty <= 0)
        {
            _orders.RemoveAt(0);
            removed = true;
        }
        else
        {
            _orders[0] = new FastOrder(first.OrderId, newLeavesQty);
            removed = false;
        }

        return true;
    }
}

/// <summary>
/// Minimal order representation for the book (only what's needed for matching).
/// </summary>
public readonly struct FastOrder
{
    public readonly long OrderId;
    public readonly decimal LeavesQty;

    public FastOrder(long orderId, decimal leavesQty)
    {
        OrderId = orderId;
        LeavesQty = leavesQty;
    }
}
