namespace KlingerSimulator.Generation.Performance;

/// <summary>
/// Parallel execution for maximum throughput.
/// Best for stress testing and high-volume scenarios.
/// </summary>
public sealed class ThroughputProfile : IPerformanceProfile
{
    public void Run(int count, Action<int> action)
    {
        Parallel.For(0, count, action);
    }
}
