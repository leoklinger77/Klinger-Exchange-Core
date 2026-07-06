using KlingerExchange.Matching.Domain.Struct;
using System.Runtime.CompilerServices;

namespace KlingerExchange.Matching.Domain;

/// <summary>
/// Price level optimized for fast first-order access.
/// Uses array-backed list instead of LinkedList for cache locality.
/// </summary>
public sealed class FastPriceLevel {
    private readonly List<FastOrder> _orders;
    public long PriceFixed { get; }
    public decimal TotalQuantity { get; private set; }

    public FastPriceLevel(long priceFixed) {
        PriceFixed = priceFixed;
        _orders = new List<FastOrder>(4); // Pre-allocate for common case
        TotalQuantity = 0;
    }

    public int OrderCount => _orders.Count;
    public bool IsEmpty => _orders.Count == 0;

    public decimal FirstOrderLeavesQty => _orders.Count > 0 ? _orders[0].LeavesQty : 0;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AddOrder(Order order) {
        _orders.Add(new FastOrder(order.OrderId, order.LeavesQty));
        TotalQuantity += order.LeavesQty;
    }

    public bool RemoveOrder(long orderId) {
        for (int i = 0; i < _orders.Count; i++) {
            if (_orders[i].OrderId == orderId) {
                TotalQuantity -= _orders[i].LeavesQty;
                _orders.RemoveAt(i);
                return true;
            }
        }
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool ApplyFillToFirst(decimal fillQty, out long originalOrderId, out bool removed) {
        if (_orders.Count == 0) {
            originalOrderId = 0;
            removed = false;
            return false;
        }

        var first = _orders[0];
        originalOrderId = first.OrderId;
        TotalQuantity -= fillQty;

        var newLeavesQty = first.LeavesQty - fillQty;

        if (newLeavesQty <= 0) {
            _orders.RemoveAt(0);
            removed = true;
        } else {
            _orders[0] = new FastOrder(first.OrderId, newLeavesQty);
            removed = false;
        }

        return true;
    }

    /// <summary>
    /// Applies a fill to a specific order by ID (used during EventStore recovery).
    /// Returns true if the order was found and updated.
    /// </summary>
    public bool ApplyFillToOrder(long orderId, decimal fillQty, out bool removed) {
        for (int i = 0; i < _orders.Count; i++) {
            if (_orders[i].OrderId == orderId) {
                TotalQuantity -= fillQty;
                var newLeavesQty = _orders[i].LeavesQty - fillQty;

                if (newLeavesQty <= 0) {
                    _orders.RemoveAt(i);
                    removed = true;
                } else {
                    _orders[i] = new FastOrder(orderId, newLeavesQty);
                    removed = false;
                }
                return true;
            }
        }
        removed = false;
        return false;
    }
}
