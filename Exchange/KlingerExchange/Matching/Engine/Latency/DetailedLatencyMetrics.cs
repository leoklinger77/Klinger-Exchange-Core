namespace KlingerExchange.Matching.Engine.Latency;

public sealed class DetailedLatencyMetrics {
    public long ParseTimeNs { get; init; }
    public long MatchTimeNs { get; init; }
    public long ReportBuildTimeNs { get; init; }
    public long SendTimeNs { get; init; }
    public long TotalTimeNs { get; init; }
    public string MsgType { get; init; } = string.Empty;
    public int FillCount { get; init; }

    public override string ToString() {
        return $"[{MsgType}] Total={FormatNs(TotalTimeNs)} | Parse={FormatNs(ParseTimeNs)} Match={FormatNs(MatchTimeNs)} Report={FormatNs(ReportBuildTimeNs)} Send={FormatNs(SendTimeNs)} | Fills={FillCount}";
    }

    private static string FormatNs(long ns) {
        if (ns >= 1_000_000) return $"{ns / 1_000_000.0:F2}ms";
        if (ns >= 1_000) return $"{ns / 1_000.0:F1}μs";
        return $"{ns}ns";
    }
}
