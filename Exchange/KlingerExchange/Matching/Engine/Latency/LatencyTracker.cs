namespace KlingerExchange.Matching.Engine.Latency;

public readonly struct LatencyMetrics {
    public long ParseTimeNs { get; init; }
    public long BookInsertTimeNs { get; init; }
    public long MatchTimeNs { get; init; }
    public long ReportBuildTimeNs { get; init; }
    public long TotalTimeNs { get; init; }

    public LatencyMetrics(long parseTimeNs, long bookInsertTimeNs, long matchTimeNs, long reportBuildTimeNs, long totalTimeNs) {
        ParseTimeNs = parseTimeNs;
        BookInsertTimeNs = bookInsertTimeNs;
        MatchTimeNs = matchTimeNs;
        ReportBuildTimeNs = reportBuildTimeNs;
        TotalTimeNs = totalTimeNs;
    }

    public override string ToString() {
        return $"Parse={ParseTimeNs}ns, Insert={BookInsertTimeNs}ns, Match={MatchTimeNs}ns, Report={ReportBuildTimeNs}ns, Total={TotalTimeNs}ns";
    }
}
