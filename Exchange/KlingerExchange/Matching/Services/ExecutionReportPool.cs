using QuickFix.FIX41;
using System.Collections.Concurrent;

namespace KlingerExchange.Matching.Services;

public sealed class ExecutionReportPool
{
    private readonly ConcurrentBag<ExecutionReport> _pool = new();
    private int _rentCount;
    private int _returnCount;

    public ExecutionReport Rent()
    {
        Interlocked.Increment(ref _rentCount);
        
        if (_pool.TryTake(out var report))
            return report;

        // Create new if pool is empty
        return new ExecutionReport(
            new QuickFix.Fields.OrderID(""),
            new QuickFix.Fields.ExecID(""),
            new QuickFix.Fields.ExecTransType('0'),
            new QuickFix.Fields.ExecType('0'),
            new QuickFix.Fields.OrdStatus('0'),
            new QuickFix.Fields.Symbol(""),
            new QuickFix.Fields.Side('1'),
            new QuickFix.Fields.OrderQty(0),
            new QuickFix.Fields.LastShares(0),
            new QuickFix.Fields.LastPx(0),
            new QuickFix.Fields.LeavesQty(0),
            new QuickFix.Fields.CumQty(0),
            new QuickFix.Fields.AvgPx(0)
        );
    }

    public void Return(ExecutionReport report)
    {
        Interlocked.Increment(ref _returnCount);
        
        // Don't clear - just return to pool
        // Fields will be overwritten on next Rent()
        _pool.Add(report);
    }

    public (int Rented, int Returned, int PoolSize) GetStats()
    {
        return (_rentCount, _returnCount, _pool.Count);
    }
}
