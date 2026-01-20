namespace KlingerExchange.Matching.Domain;

public readonly struct MatchResult
{
    public long CounterOrderId { get; init; }
    public decimal Price { get; init; }
    public decimal Quantity { get; init; }
}

public sealed class OrderBook
{
    private readonly SortedDictionary<decimal, PriceLevel> _bids;
    private readonly SortedDictionary<decimal, PriceLevel> _asks;
    private readonly System.Collections.Concurrent.ConcurrentDictionary<long, (decimal Price, Side Side)> _orderIndex;
    private readonly object _bookLock = new object();

    public string Symbol { get; }

    public OrderBook(string symbol)
    {
        Symbol = symbol;
        _bids = new SortedDictionary<decimal, PriceLevel>(Comparer<decimal>.Create((a, b) => b.CompareTo(a)));
        _asks = new SortedDictionary<decimal, PriceLevel>();
        _orderIndex = new System.Collections.Concurrent.ConcurrentDictionary<long, (decimal, Side)>();
    }

    public void AddOrder(Order order)
    {
        lock (_bookLock)
        {
            var book = order.Side == Side.Buy ? _bids : _asks;

            if (!book.TryGetValue(order.Price, out var level))
            {
                level = new PriceLevel(order.Price);
                book[order.Price] = level;
            }

            level.AddOrder(order);
            _orderIndex[order.OrderId] = (order.Price, order.Side);
        }
    }

    public bool RemoveOrder(long orderId)
    {
        if (!_orderIndex.TryRemove(orderId, out var location))
            return false;

        lock (_bookLock)
        {
            var book = location.Side == Side.Buy ? _bids : _asks;
            if (!book.TryGetValue(location.Price, out var level))
                return false;

            var removed = level.RemoveOrder(orderId);
            if (removed && level.IsEmpty)
                book.Remove(location.Price);

            return removed;
        }
    }

    public void UpdateOrderFill(long orderId, decimal fillQty)
    {
        if (!_orderIndex.TryGetValue(orderId, out var location))
            return;

        lock (_bookLock)
        {
            var book = location.Side == Side.Buy ? _bids : _asks;
            if (!book.TryGetValue(location.Price, out var level))
                return;

            level.UpdateOrderFill(orderId, fillQty);

            if (level.IsEmpty)
            {
                book.Remove(location.Price);
                _orderIndex.TryRemove(orderId, out _);
            }
        }
    }

    public void MatchOrder(Side incomingSide, decimal limitPrice, ref decimal remainingQty, List<MatchResult> matches)
    {
        lock (_bookLock)
        {
            var book = incomingSide == Side.Buy ? _asks : _bids;

            while (remainingQty > 0 && book.Count > 0)
            {
                var bestLevel = book.First().Value;

                if (incomingSide == Side.Buy)
                {
                    if (bestLevel.Price > limitPrice)
                        break;
                }
                else
                {
                    if (bestLevel.Price < limitPrice)
                        break;
                }

                if (bestLevel.Orders.First == null)
                {
                    book.Remove(bestLevel.Price);
                    continue;
                }

                var counterOrder = bestLevel.Orders.First.Value;
                var fillQty = Math.Min(remainingQty, counterOrder.LeavesQty);

                if (!bestLevel.ApplyFillToFirst(fillQty, out var originalOrder, out _, out var removed))
                    break;

                matches.Add(new MatchResult
                {
                    CounterOrderId = originalOrder.OrderId,
                    Price = originalOrder.Price,
                    Quantity = fillQty
                });

                remainingQty -= fillQty;

                if (removed)
                {
                    _orderIndex.TryRemove(originalOrder.OrderId, out _);
                }

                if (bestLevel.IsEmpty)
                {
                    book.Remove(bestLevel.Price);
                }
            }
        }
    }

    public PriceLevel? GetBestBid()
    {
        lock (_bookLock)
        {
            return _bids.Count > 0 ? _bids.First().Value : null;
        }
    }

    public PriceLevel? GetBestAsk()
    {
        lock (_bookLock)
        {
            return _asks.Count > 0 ? _asks.First().Value : null;
        }
    }

    public (decimal BidPrice, decimal BidQty, decimal AskPrice, decimal AskQty) GetTopOfBook()
    {
        lock (_bookLock)
        {
            var bestBid = _bids.Count > 0 ? _bids.First().Value : null;
            var bestAsk = _asks.Count > 0 ? _asks.First().Value : null;

            return (
                bestBid?.Price ?? 0,
                bestBid?.TotalQuantity ?? 0,
                bestAsk?.Price ?? 0,
                bestAsk?.TotalQuantity ?? 0
            );
        }
    }

    /// <summary>
    /// Retorna todos os OrderIds no book (para recovery)
    /// </summary>
    public IEnumerable<long> GetAllOrderIds()
    {
        return _orderIndex.Keys;
    }
}
