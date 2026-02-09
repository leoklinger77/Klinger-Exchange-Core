using QuickFix.FIX41;
using System.Collections.Concurrent;

namespace KlingerExchange.Matching.Services;

public sealed class RejectPool {
    private readonly ConcurrentBag<Reject> _pool = new();
    private int _rentCount;
    private int _returnCount;

    public Reject Rent() {
        Interlocked.Increment(ref _rentCount);

        if (_pool.TryTake(out var msg))
            return msg;

        // Create new if pool is empty
        return new Reject(
            new QuickFix.Fields.RefSeqNum(0)
        );
    }

    public void Return(Reject msg) {
        Interlocked.Increment(ref _returnCount);
        _pool.Add(msg);
    }

    public (int Rented, int Returned, int PoolSize) GetStats() {
        return (_rentCount, _returnCount, _pool.Count);
    }
}
