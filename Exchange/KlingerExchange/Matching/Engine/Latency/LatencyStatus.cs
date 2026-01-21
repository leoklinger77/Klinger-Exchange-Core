namespace KlingerExchange.Matching.Engine.Latency;

public sealed class LatencyStatus {
    public long TotalOrders { get; init; }
    public long SlowOrders { get; init; }
    public double SlowOrderPercent { get; init; }
    public long MinNs { get; init; }
    public long MaxNs { get; init; }
    public long AvgNs { get; init; }
    public long P50Ns { get; init; }
    public long P95Ns { get; init; }
    public long P99Ns { get; init; }
    public long CpuMigrations { get; init; }
    public long SampledMatches { get; init; }
    public int SampleRate { get; init; }

    public override string ToString() {
        return $"Orders={TotalOrders} Slow={SlowOrders}({SlowOrderPercent:F1}%) | " +
               $"Min={FormatNs(MinNs)} Avg={FormatNs(AvgNs)} P50={FormatNs(P50Ns)} P95={FormatNs(P95Ns)} P99={FormatNs(P99Ns)} Max={FormatNs(MaxNs)}";
    }

    public string GetBenchInfo() {
        var migrationPct = SampledMatches > 0 ? (CpuMigrations * 100.0 / SampledMatches) : 0;
        return $"BENCH: {SampledMatches} matches (1/{SampleRate}) | CPU migrations:{CpuMigrations}({migrationPct:F2}%)";
    }

    private static string FormatNs(long ns) {
        if (ns >= 1_000_000) return $"{ns / 1_000_000.0:F2}ms";
        if (ns >= 1_000) return $"{ns / 1_000.0:F1}μs";
        return $"{ns}ns";
    }
}
