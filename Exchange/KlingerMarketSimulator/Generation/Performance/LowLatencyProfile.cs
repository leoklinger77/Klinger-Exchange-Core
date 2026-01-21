namespace KlingerSimulator.Generation.Performance;

/// <summary>
/// Single-threaded execution for minimal latency.
/// Best for low-latency scenarios where predictability matters.
/// </summary>
public sealed class LowLatencyProfile : IPerformanceProfile
{
    public void Run(int count, Action<int> action)
    {
        for (int i = 0; i < count; i++)
            action(i);
    }
}
