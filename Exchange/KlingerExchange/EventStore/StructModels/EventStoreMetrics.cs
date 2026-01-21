namespace KlingerExchange.EventStore.StructModels;

/// <summary>
/// Métricas do EventStore
/// </summary>
public readonly struct EventStoreMetrics {
    public long EventsWritten { get; init; }
    public long EventsFlushed { get; init; }
    public long EventsDropped { get; init; }
    public double BufferUtilization { get; init; }
    public int BufferUsed { get; init; }
    public int BufferCapacity { get; init; }
    public long BytesWritten { get; init; }

    public int EventsPending => (int)(EventsWritten - EventsFlushed);
    public bool IsHealthy => EventsDropped == 0 && BufferUtilization < 0.90;
}