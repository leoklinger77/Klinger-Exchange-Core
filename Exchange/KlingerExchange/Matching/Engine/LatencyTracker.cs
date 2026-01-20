using System.Diagnostics;

namespace KlingerExchange.Matching.Engine;

public readonly struct LatencyMetrics
{
    public long ParseTimeNs { get; init; }
    public long BookInsertTimeNs { get; init; }
    public long MatchTimeNs { get; init; }
    public long ReportBuildTimeNs { get; init; }
    public long TotalTimeNs { get; init; }

    public LatencyMetrics(long parseTimeNs, long bookInsertTimeNs, long matchTimeNs, long reportBuildTimeNs, long totalTimeNs)
    {
        ParseTimeNs = parseTimeNs;
        BookInsertTimeNs = bookInsertTimeNs;
        MatchTimeNs = matchTimeNs;
        ReportBuildTimeNs = reportBuildTimeNs;
        TotalTimeNs = totalTimeNs;
    }

    public override string ToString()
    {
        return $"Parse={ParseTimeNs}ns, Insert={BookInsertTimeNs}ns, Match={MatchTimeNs}ns, Report={ReportBuildTimeNs}ns, Total={TotalTimeNs}ns";
    }
}

public static class LatencyTracker
{
    private const long WarningThresholdNs = 1_000_000; // 1ms

    public static long TicksToNanoseconds(long ticks)
    {
        return ticks * 100; // 1 tick = 100ns
    }

    public static bool IsSlowPath(long totalTimeNs)
    {
        return totalTimeNs > WarningThresholdNs;
    }

    public static long MeasureNanoseconds(Action action)
    {
        var sw = Stopwatch.StartNew();
        action();
        sw.Stop();
        return TicksToNanoseconds(sw.ElapsedTicks);
    }
}
