namespace KlingerSimulator.Generation.Performance;

/// <summary>
/// Controls HOW the generator executes (performance characteristics).
/// Does NOT control WHAT is generated.
/// </summary>
public interface IPerformanceProfile
{
    /// <summary>
    /// Execute action 'count' times using the profile's execution strategy.
    /// </summary>
    void Run(int count, Action<int> action);
}
