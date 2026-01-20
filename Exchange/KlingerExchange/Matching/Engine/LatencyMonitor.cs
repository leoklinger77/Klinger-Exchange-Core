using System.Collections.Concurrent;
using System.Diagnostics;

namespace KlingerExchange.Matching.Engine;

public sealed class DetailedLatencyMetrics
{
    public long ParseTimeNs { get; init; }
    public long MatchTimeNs { get; init; }
    public long ReportBuildTimeNs { get; init; }
    public long SendTimeNs { get; init; }
    public long TotalTimeNs { get; init; }
    public string MsgType { get; init; } = string.Empty;
    public int FillCount { get; init; }

    public override string ToString()
    {
        return $"[{MsgType}] Total={FormatNs(TotalTimeNs)} | Parse={FormatNs(ParseTimeNs)} Match={FormatNs(MatchTimeNs)} Report={FormatNs(ReportBuildTimeNs)} Send={FormatNs(SendTimeNs)} | Fills={FillCount}";
    }

    private static string FormatNs(long ns)
    {
        if (ns >= 1_000_000) return $"{ns / 1_000_000.0:F2}ms";
        if (ns >= 1_000) return $"{ns / 1_000.0:F1}μs";
        return $"{ns}ns";
    }
}

public sealed class LatencyMonitor
{
    private readonly ConcurrentQueue<long> _recentLatencies;
    private readonly int _windowSize;
    private long _totalOrders;
    private long _slowOrders; // > 1ms
    private long _minNs = long.MaxValue;
    private long _maxNs;
    private long _sumNs;

    public LatencyMonitor(int windowSize = 1000)
    {
        _recentLatencies = new ConcurrentQueue<long>();
        _windowSize = windowSize;
    }

    public void RecordLatency(long totalTimeNs)
    {
        Interlocked.Increment(ref _totalOrders);
        Interlocked.Add(ref _sumNs, totalTimeNs);

        if (totalTimeNs > 1_000_000)
            Interlocked.Increment(ref _slowOrders);

        // Update min
        long currentMin;
        do
        {
            currentMin = _minNs;
            if (totalTimeNs >= currentMin) break;
        } while (Interlocked.CompareExchange(ref _minNs, totalTimeNs, currentMin) != currentMin);

        // Update max
        long currentMax;
        do
        {
            currentMax = _maxNs;
            if (totalTimeNs <= currentMax) break;
        } while (Interlocked.CompareExchange(ref _maxNs, totalTimeNs, currentMax) != currentMax);

        // Add to sliding window
        _recentLatencies.Enqueue(totalTimeNs);
        while (_recentLatencies.Count > _windowSize)
        {
            _recentLatencies.TryDequeue(out _);
        }
    }

    public LatencyStats GetStats()
    {
        var total = Interlocked.Read(ref _totalOrders);
        var slow = Interlocked.Read(ref _slowOrders);
        var sum = Interlocked.Read(ref _sumNs);
        var min = Interlocked.Read(ref _minNs);
        var max = Interlocked.Read(ref _maxNs);

        var avgNs = total > 0 ? sum / total : 0;

        // Calculate percentiles from recent window
        var snapshot = _recentLatencies.ToArray();
        Array.Sort(snapshot);

        var p50 = GetPercentile(snapshot, 0.50);
        var p95 = GetPercentile(snapshot, 0.95);
        var p99 = GetPercentile(snapshot, 0.99);

        return new LatencyStats
        {
            TotalOrders = total,
            SlowOrders = slow,
            SlowOrderPercent = total > 0 ? slow * 100.0 / total : 0,
            MinNs = min == long.MaxValue ? 0 : min,
            MaxNs = max,
            AvgNs = avgNs,
            P50Ns = p50,
            P95Ns = p95,
            P99Ns = p99
        };
    }

    private static long GetPercentile(long[] sortedValues, double percentile)
    {
        if (sortedValues.Length == 0) return 0;
        var index = (int)Math.Ceiling(percentile * sortedValues.Length) - 1;
        if (index < 0) index = 0;
        if (index >= sortedValues.Length) index = sortedValues.Length - 1;
        return sortedValues[index];
    }
}

public sealed class LatencyStats
{
    public long TotalOrders { get; init; }
    public long SlowOrders { get; init; }
    public double SlowOrderPercent { get; init; }
    public long MinNs { get; init; }
    public long MaxNs { get; init; }
    public long AvgNs { get; init; }
    public long P50Ns { get; init; }
    public long P95Ns { get; init; }
    public long P99Ns { get; init; }

    public override string ToString()
    {
        return $"Orders={TotalOrders} Slow={SlowOrders}({SlowOrderPercent:F1}%) | " +
               $"Min={FormatNs(MinNs)} Avg={FormatNs(AvgNs)} P50={FormatNs(P50Ns)} P95={FormatNs(P95Ns)} P99={FormatNs(P99Ns)} Max={FormatNs(MaxNs)}";
    }

    private static string FormatNs(long ns)
    {
        if (ns >= 1_000_000) return $"{ns / 1_000_000.0:F2}ms";
        if (ns >= 1_000) return $"{ns / 1_000.0:F1}μs";
        return $"{ns}ns";
    }
}
