namespace KlingerExchange.EventStore.StructModels;

public readonly struct BufferMetrics
{
    public int Capacity { get; init; }
    public int Used { get; init; }
    public int Available { get; init; }
    public double Utilization { get; init; }
}
