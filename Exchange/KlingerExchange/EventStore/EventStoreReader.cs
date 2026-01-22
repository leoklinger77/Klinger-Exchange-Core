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
    /// Reads all events from the file sequentially (streaming - no memory accumulation).
    /// </summary>
    public IEnumerable<(EventHeader Header, object Event)> ReadAll() {
        if (_fileStream == null)
            yield break;

        _fileStream.Position = 0;
        long eventsRead = 0;
        long bytesProcessed = 0;
        var fileSize = _fileStream.Length;

        // Check if file is empty or too small
        if (fileSize < 64) { // Minimum size for at least one event
            _log.Information("File is empty or too small ({Size} bytes), skipping", fileSize);
            yield break;
        }

        while (_fileStream.Position < fileSize) {
            EventHeader header;
            object? eventData = null;
            var positionBeforeRead = _fileStream.Position;

            // Check if we're near the end (less than minimum event size)
            var remainingBytes = fileSize - _fileStream.Position;
            if (remainingBytes < 32) { // Minimum realistic event size
                _log.Debug("Reached end of file with {Bytes} bytes remaining", remainingBytes);
                break;
            }

            // Read header
            // ZeroFormatter for EventHeader: long(8) + EventType(1) + long(8) + int(4) = 21 bytes
            var initialHeaderSize = 21;
            var headerBuffer = new byte[initialHeaderSize];
            var bytesRead = _fileStream.Read(headerBuffer, 0, initialHeaderSize);

            if (bytesRead < initialHeaderSize) {
                _log.Debug("Incomplete header at position {Position}, stopping", positionBeforeRead);
                break;
            }

            // Check if header is all zeros (uninitialized file)
            if (IsBufferZero(headerBuffer)) {
                _log.Information("Header is all zeros at position {Position:N0}, file not initialized yet",
                    positionBeforeRead);
                break;
            }

            try {
                _log.Debug("Deserializing header at position {Pos}. First 32 bytes: {Bytes}",
                    positionBeforeRead, BitConverter.ToString(headerBuffer));
                    
                header = ZeroFormatterSerializer.Deserialize<EventHeader>(headerBuffer);

                // Check if we hit uninitialized memory (all zeros)
                if (header.EventType == 0 || header.SequenceNumber == 0) {
                    _log.Information("Reached end of valid events at position {Position:N0} ({PercentRead:F1}% of file)",
                        positionBeforeRead, (positionBeforeRead * 100.0) / fileSize);
                    break;
                }
                
                _log.Debug("Read header: Seq={Seq} Type={Type} PayloadSize={Size}",
                    header.SequenceNumber, header.EventType, header.PayloadSize);

                // Validate event type
                if (header.EventType < EventType.OrderAccepted || header.EventType > EventType.Trade) {
                    _log.Warning("Invalid EventType {Type} at position {Position}, stopping",
                        header.EventType, positionBeforeRead);
                    break;
                }

                // Validate payload size with stricter bounds
                if (header.PayloadSize <= 0 || header.PayloadSize > 10 * 1024 * 1024) { // Max 10MB per event
                    _log.Warning("Invalid PayloadSize {Size} at position {Position}, stopping",
                        header.PayloadSize, positionBeforeRead);
                    break;
                }

                // Check if we have enough bytes for payload
                if (_fileStream.Position + header.PayloadSize > fileSize) {
                    _log.Warning("Payload extends beyond EOF at position {Position}, stopping", positionBeforeRead);
                    break;
                }

                // Read payload
                var payloadBuffer = new byte[header.PayloadSize];
                bytesRead = _fileStream.Read(payloadBuffer, 0, header.PayloadSize);

                if (bytesRead < header.PayloadSize) {
                    _log.Warning("Incomplete payload for event {SeqNum}, stopping", header.SequenceNumber);
                    break;
                }

                // Check if payload is all zeros (uninitialized/corrupted)
                if (IsBufferZero(payloadBuffer)) {
                    _log.Information("Payload is all zeros at position {Position:N0}, reached end of valid data",
                        positionBeforeRead);
                    break;
                }

                // Deserialize specific event with additional error handling
                try {
                    eventData = header.EventType switch {
                        EventType.OrderAccepted => ZeroFormatterSerializer.Deserialize<OrderAcceptedEvent>(payloadBuffer),
                        EventType.OrderFilled => ZeroFormatterSerializer.Deserialize<OrderFilledEvent>(payloadBuffer),
                        EventType.OrderPartiallyFilled => ZeroFormatterSerializer.Deserialize<OrderPartiallyFilledEvent>(payloadBuffer),
                        EventType.OrderCancelled => ZeroFormatterSerializer.Deserialize<OrderCancelledEvent>(payloadBuffer),
                        EventType.Trade => ZeroFormatterSerializer.Deserialize<TradeEvent>(payloadBuffer),
                        _ => throw new InvalidOperationException($"Unknown EventType: {header.EventType}")
                    };
                    
                    _log.Debug("Deserialized event {Seq} successfully", header.SequenceNumber);
                } catch (ArgumentOutOfRangeException ex) {
                    _log.Information("Corrupted event data at position {Position:N0}, stopping recovery here (likely old/invalid file). Error: {Error}",
                        positionBeforeRead, ex.Message);
                    _log.Debug(ex, "Deserialization error details");
                    yield break;
                } catch (Exception ex) {
                    _log.Warning(ex, "Unexpected error deserializing event {Type} at position {Position}, stopping",
                        header.EventType, positionBeforeRead);
                    yield break;
                }

                eventsRead++;
                bytesProcessed = _fileStream.Position;
                
                // Log progress for large files (every 100K events)
                if (eventsRead % 100000 == 0) {
                    var percentRead = (bytesProcessed * 100.0) / fileSize;
                    _log.Information("Progress: {EventCount:N0} events read ({PercentRead:F1}% of file)",
                        eventsRead, percentRead);
                }
            } catch (Exception ex) {
                _log.Warning(ex, "Error reading event at position {Position} after {Count} events, stopping",
                    positionBeforeRead, eventsRead);
                yield break;
            }

            if (eventData != null)
                yield return (header, eventData);
        }

        var percentComplete = (bytesProcessed * 100.0) / fileSize;
        _log.Information("✅ Total events read: {Count:N0} ({PercentRead:F1}% of file, {BytesMB:F2} MB)",
            eventsRead, percentComplete, bytesProcessed / (1024.0 * 1024.0));
    }

    /// <summary>
    /// Calculates the header size (fixed for ZeroFormatter structs)
    /// </summary>
    private int GetHeaderSize() {
        // EventHeader: long(8) + byte(1) + long(8) + int(4) = ~21 bytes + overhead ZeroFormatter
        // Vamos Read a larger block to ensure
        return 128; // Oversized for safety, ZeroFormatter has variable overhead.
    }

    /// <summary>
    /// Checks if a buffer is all zeros (uninitialized memory)
    /// </summary>
    private static bool IsBufferZero(byte[] buffer) {
        // Sample check - if first 32 bytes are zero, likely entire buffer is zero
        int sampleSize = Math.Min(32, buffer.Length);
        for (int i = 0; i < sampleSize; i++) {
            if (buffer[i] != 0)
                return false;
        }
        return true;
    }

    public void Dispose() {
        _fileStream?.Dispose();
        _fileStream = null;
    }
}
