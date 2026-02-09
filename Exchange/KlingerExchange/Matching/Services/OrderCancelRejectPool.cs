using QuickFix.FIX41;
using System.Collections.Concurrent;

namespace KlingerExchange.Matching.Services;

public sealed class OrderCancelRejectPool {
    private readonly ConcurrentBag<OrderCancelReject> _pool = new();
    private int _rentCount;
    private int _returnCount;

    public OrderCancelReject Rent() {
        Interlocked.Increment(ref _rentCount);

        if (_pool.TryTake(out var msg))
            return msg;

        // Create new if pool is empty
        return new OrderCancelReject();
    }

    public void Return(OrderCancelReject msg) {
        Interlocked.Increment(ref _returnCount);
        _pool.Add(msg);
    }

    public (int Rented, int Returned, int PoolSize) GetStats() {
        return (_rentCount, _returnCount, _pool.Count);
    }
}
