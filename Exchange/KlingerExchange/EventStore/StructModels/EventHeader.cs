using ZeroFormatter;

namespace KlingerExchange.EventStore.StructModels;

/// <summary>
/// Base event - Common header for all events
/// Struct for maximum performance (stack allocated)
/// </summary>
[ZeroFormattable]
public struct EventHeader {
    [Index(0)]
    public long SequenceNumber { get; set; }

    [Index(1)]
    public EventType EventType { get; set; }

    [Index(2)]
    public long TimestampTicks { get; set; }

    [Index(3)]
    public int PayloadSize { get; set; }

    public EventHeader(long sequenceNumber, EventType eventType, long timestampTicks, int payloadSize) {
        SequenceNumber = sequenceNumber;
        EventType = eventType;
        TimestampTicks = timestampTicks;
        PayloadSize = payloadSize;
    }
}
