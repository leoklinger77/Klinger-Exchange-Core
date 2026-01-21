using System.Collections.Concurrent;

namespace KlingerExchange.Matching.Engine.Latency;

public sealed class LatencyMonitor {
    private readonly ConcurrentQueue<long> _recentLatencies;
    private readonly int _windowSize;
    private long _totalOrders;
    private long _slowOrders; // > 1ms
    private long _minNs = long.MaxValue;
    private long _maxNs;
    private long _sumNs;

    public LatencyMonitor(int windowSize = 1000) {
        _recentLatencies = new ConcurrentQueue<long>();
        _windowSize = windowSize;
    }

    public void RecordLatency(long totalTimeNs) {
        Interlocked.Increment(ref _totalOrders);
        Interlocked.Add(ref _sumNs, totalTimeNs);

        if (totalTimeNs > 1_000_000)
            Interlocked.Increment(ref _slowOrders);

        // Update min
        long currentMin;
        do {
            currentMin = _minNs;
            if (totalTimeNs >= currentMin) break;
        } while (Interlocked.CompareExchange(ref _minNs, totalTimeNs, currentMin) != currentMin);

        // Update max
        long currentMax;
        do {
            currentMax = _maxNs;
            if (totalTimeNs <= currentMax) break;
        } while (Interlocked.CompareExchange(ref _maxNs, totalTimeNs, currentMax) != currentMax);

        // Add to sliding window
        _recentLatencies.Enqueue(totalTimeNs);
        while (_recentLatencies.Count > _windowSize) {
            _recentLatencies.TryDequeue(out _);
        }
    }

    public LatencyStatus GetStats() {
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

        return new LatencyStatus {
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

    private static long GetPercentile(long[] sortedValues, double percentile) {
        if (sortedValues.Length == 0) return 0;
        var index = (int)Math.Ceiling(percentile * sortedValues.Length) - 1;
        if (index < 0) index = 0;
        if (index >= sortedValues.Length) index = sortedValues.Length - 1;
        return sortedValues[index];
    }
}
