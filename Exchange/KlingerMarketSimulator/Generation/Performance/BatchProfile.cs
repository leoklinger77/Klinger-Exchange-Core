namespace KlingerSimulator.Generation.Performance;

/// <summary>
/// Batch execution for controlled throughput.
/// Processes items in batches, similar to how matching engines work.
/// </summary>
public sealed class BatchProfile : IPerformanceProfile
{
    private readonly int _batchSize;

    public BatchProfile(int batchSize)
    {
        _batchSize = batchSize;
    }

    public void Run(int count, Action<int> action)
    {
        for (int i = 0; i < count; i += _batchSize)
        {
            var end = Math.Min(i + _batchSize, count);
            for (int j = i; j < end; j++)
                action(j);
        }
    }
}
