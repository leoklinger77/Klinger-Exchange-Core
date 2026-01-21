using KlingerExchange.EventStore.StructModels;
using Serilog;
using ZeroFormatter;

namespace KlingerExchange.EventStore;

/// <summary>
/// Reads events from the EventStore for replay/recovery.
/// </summary>
public sealed class EventStoreReader : IDisposable {
    private readonly ILogger _log = Log.ForContext<EventStoreReader>();
    private FileStream? _fileStream;

    public EventStoreReader(string filePath) {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"Event file not found.: {filePath}");

        _fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        _log.Information("EventStoreReader Open: {FilePath}", filePath);
    }

    /// <summary>
    /// Reads all events from the file sequentially.
    /// </summary>
    public IEnumerable<(EventHeader Header, object Event)> ReadAll() {
        if (_fileStream == null)
            yield break;

        _fileStream.Position = 0;
        long eventsRead = 0;

        while (_fileStream.Position < _fileStream.Length) {
            EventHeader header;
            object? eventData = null;

            // Read header
            var headerSize = GetHeaderSize();
            var headerBuffer = new byte[headerSize];
            var bytesRead = _fileStream.Read(headerBuffer, 0, headerSize);

            if (bytesRead < headerSize)
                break; // End of file

            try {
                header = ZeroFormatterSerializer.Deserialize<EventHeader>(headerBuffer);

                // Check if we hit uninitialized memory (all zeros)
                if (header.EventType == 0 || header.SequenceNumber == 0) {
                    _log.Debug("Reached end of valid events at position {Position}", _fileStream.Position);
                    break;
                }

                // Validate payload size
                if (header.PayloadSize <= 0 || header.PayloadSize > 1024 * 1024) // Max 1MB per event
                {
                    _log.Warning("Invalid PayloadSize {Size} at position {Position}", header.PayloadSize, _fileStream.Position);
                    break;
                }

                // Read payload
                var payloadBuffer = new byte[header.PayloadSize];
                bytesRead = _fileStream.Read(payloadBuffer, 0, header.PayloadSize);

                if (bytesRead < header.PayloadSize) {
                    _log.Warning("Payload incompleto no evento {SeqNum}", header.SequenceNumber);
                    break;
                }

                // Deserialize specific event
                eventData = header.EventType switch {
                    EventType.OrderAccepted => ZeroFormatterSerializer.Deserialize<OrderAcceptedEvent>(payloadBuffer),
                    EventType.OrderFilled => ZeroFormatterSerializer.Deserialize<OrderFilledEvent>(payloadBuffer),
                    EventType.OrderPartiallyFilled => ZeroFormatterSerializer.Deserialize<OrderPartiallyFilledEvent>(payloadBuffer),
                    EventType.OrderCancelled => ZeroFormatterSerializer.Deserialize<OrderCancelledEvent>(payloadBuffer),
                    EventType.Trade => ZeroFormatterSerializer.Deserialize<TradeEvent>(payloadBuffer),
                    _ => throw new InvalidOperationException($"Unknown EventType: {header.EventType}")
                };

                eventsRead++;
            } catch (Exception ex) {
                _log.Debug(ex, "Stopped reading at position {Position} (likely end of valid data)", _fileStream.Position);
                yield break;
            }

            if (eventData != null)
                yield return (header, eventData);
        }

        _log.Information("Total number of events read: {Count}", eventsRead);
    }

    /// <summary>
    /// Calculates the header size (fixed for ZeroFormatter structs)
    /// </summary>
    private int GetHeaderSize() {
        // EventHeader: long(8) + byte(1) + long(8) + int(4) = ~21 bytes + overhead ZeroFormatter
        // Vamos Read a larger block to ensure
        return 128; // Oversized for safety, ZeroFormatter has variable overhead.
    }

    public void Dispose() {
        _fileStream?.Dispose();
        _fileStream = null;
    }
}
