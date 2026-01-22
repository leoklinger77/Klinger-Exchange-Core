using KlingerExchange.EventStore.StructModels;
using Serilog;
using System.IO.MemoryMappedFiles;
using ZeroFormatter;

namespace KlingerExchange.EventStore;

/// <summary>
/// Container for an event + serialized payload
/// </summary>
internal struct EventContainer
{
    public EventHeader Header;
    public byte[] Payload;
}

/// <summary>
/// Optimized EventStore with Memory-Mapped Files and Group Commit
/// Architecture: Lock-free queue -> Background writer -> MMF with fsync
/// Target: <10µs latency, zero loss, 100K+ ops/sec
/// </summary>
public sealed class EventStoreWriter : IDisposable
{
    private readonly ILogger _log = Log.ForContext<EventStoreWriter>();
    private readonly string _baseDirectory;
    private readonly LockFreeRingBuffer<EventContainer> _eventQueue;
    private readonly Thread _writerThread;
    private readonly ManualResetEventSlim _shutdownEvent;
    private readonly ManualResetEventSlim _flushEvent;
    
    private MemoryMappedFile? _mmf;
    private MemoryMappedViewStream? _stream;
    private long _currentSequenceNumber;
    private long _lastFlushedSequence;
    private long _bytesWritten;
    private string? _currentFileName;
    private long _eventsDropped;
    
    private const long MMF_SIZE = 1024L * 1024 * 1024;          // 1GB pre-allocated
    private const int QUEUE_SIZE = 131072;                      // 128K events (doubled for safety)
    private const int BATCH_SIZE = 100;                         // Events per batch
    private const int FLUSH_INTERVAL_MS = 1;                    // Flush every 1ms (maximum durability)
    private const double WATERMARK_WARNING = 0.90;              // 90% capacity
    private const double WATERMARK_CRITICAL = 0.95;             // 95% capacity

    public EventStoreWriter(string baseDirectory)
    {
        _baseDirectory = baseDirectory;
        _eventQueue = new LockFreeRingBuffer<EventContainer>(QUEUE_SIZE);
        _shutdownEvent = new ManualResetEventSlim(false);
        _flushEvent = new ManualResetEventSlim(false);
        _currentSequenceNumber = 0;
        _lastFlushedSequence = -1;
        
        Directory.CreateDirectory(_baseDirectory);
        OpenOrCreateLogFile();

        // Start background writer thread
        _writerThread = new Thread(WriterThreadLoop)
        {
            Name = "EventStore-Writer",
            IsBackground = false, // Important: not background to ensure flush on shutdown
            Priority = ThreadPriority.Highest
        };
        _writerThread.Start();

        _log.Information("EventStore started in {Directory}", _baseDirectory);
    }

    /// <summary>
    /// Enqueues event for persistence (non-blocking, ~50ns)
    /// Returns sequence number or -1 if buffer is full
    /// </summary>
    public long Append<T>(EventType eventType, T eventData) where T : struct
    {
        var payload = ZeroFormatterSerializer.Serialize(eventData);
        
        var container = new EventContainer
        {
            Header = new EventHeader(
                Interlocked.Increment(ref _currentSequenceNumber),
                eventType,
                DateTimeOffset.UtcNow.Ticks,
                payload.Length
            ),
            Payload = payload
        };

        var sequenceId = _eventQueue.TryEnqueue(container);
        
        if (sequenceId < 0)
        {
            Interlocked.Increment(ref _eventsDropped);
            _log.Error("🔴 CRITICAL: EventStore buffer full! Event {SeqNum} lost. Total dropped: {Dropped}",
                container.Header.SequenceNumber, _eventsDropped);
            return -1;
        }

        return sequenceId;
    }

    /// <summary>
    /// Waits until the event is persisted to disk (durable)
    /// Target: ~5-10µs on average (amortized by batch)
    /// </summary>
    public bool WaitForPersistence(long sequenceId, int timeoutMs = 5000)
    {
        // Fast path: already persisted?
        if (Volatile.Read(ref _lastFlushedSequence) >= sequenceId)
            return true;

        // Wait for flush
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < timeoutMs)
        {
            if (Volatile.Read(ref _lastFlushedSequence) >= sequenceId)
                return true;

            // Signal that there are waiters (optimization)
            _flushEvent.Wait(1);
        }

        _log.Warning("Timeout waiting for event {SequenceId} persistence", sequenceId);
        return false;
    }

    /// <summary>
    /// Writer thread: consumes events and performs group commit
    /// </summary>
    private void WriterThreadLoop()
    {
        var batch = new EventContainer[BATCH_SIZE];
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var watermarkCheckTimer = System.Diagnostics.Stopwatch.StartNew();

        _log.Information("Writer thread started");

        while (!_shutdownEvent.IsSet)
        {
            try
            {
                // Try to get a batch
                var count = _eventQueue.TryDequeueBatch(batch.AsSpan());

                // If got events OR time limit passed, flush
                var shouldFlush = count > 0 && (count >= BATCH_SIZE || sw.ElapsedMilliseconds >= FLUSH_INTERVAL_MS);

                if (count > 0)
                {
                    WriteBatch(batch.AsSpan(0, count));
                }

                if (shouldFlush)
                {
                    FlushToDisk();
                    sw.Restart();
                    _flushEvent.Set(); // Notifica waiters
                    _flushEvent.Reset();
                }
                else if (count == 0)
                {
                    // No events, wait a bit
                    Thread.Sleep(1);
                }
                
                // Check watermark every 5 seconds (background only)
                if (watermarkCheckTimer.ElapsedMilliseconds > 5000)
                {
                    CheckWatermark();
                    watermarkCheckTimer.Restart();
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, "Error in writer thread");
            }
        }

        // Final flush on shutdown
        var finalBatch = new EventContainer[BATCH_SIZE];
        var finalCount = _eventQueue.TryDequeueBatch(finalBatch.AsSpan());
        if (finalCount > 0)
        {
            WriteBatch(finalBatch.AsSpan(0, finalCount));
            FlushToDisk();
        }

        _log.Information("Writer thread finished");
    }

    /// <summary>
    /// Writes batch to MMF (still in memory)
    /// </summary>
    private void WriteBatch(Span<EventContainer> batch)
    {
        if (_stream == null)
            throw new InvalidOperationException("Stream not initialized");

        foreach (var container in batch)
        {
            // Write header
            var headerBytes = ZeroFormatterSerializer.Serialize(container.Header);
            _stream.Write(headerBytes, 0, headerBytes.Length);

            // Write payload
            _stream.Write(container.Payload, 0, container.Payload.Length);

            _bytesWritten += headerBytes.Length + container.Payload.Length;

            // Rotate file if necessary (avoid very large files)
            if (_bytesWritten > MMF_SIZE * 0.9) // 90% of size
            {
                FlushToDisk();
                CreateNewLogFile();
            }
        }

        // Update last written sequence (not yet durable)
        if (batch.Length > 0)
        {
            var lastSeq = batch[batch.Length - 1].Header.SequenceNumber;
            // Does not update _lastFlushedSequence yet, only after fsync
        }
    }

    /// <summary>
    /// Forces flush to disk (fsync) - Ensures durability
    /// </summary>
    private void FlushToDisk()
    {
        if (_stream == null)
            return;

        _stream.Flush(); // Force fsync on Windows

        // Now we can consider it durable
        var writePos = _eventQueue.ReadPosition;
        Volatile.Write(ref _lastFlushedSequence, writePos);
    }

    /// <summary>
    /// Opens existing log file or creates new one
    /// </summary>
    private void OpenOrCreateLogFile()
    {
        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd");
        _currentFileName = Path.Combine(_baseDirectory, $"events_{timestamp}.dat");
        var metaFileName = Path.Combine(_baseDirectory, $"events_{timestamp}.meta");

        // Check if file exists and has metadata
        if (File.Exists(_currentFileName) && File.Exists(metaFileName))
        {
            try
            {
                // Read actual data size from metadata file
                var metaText = File.ReadAllText(metaFileName);
                if (long.TryParse(metaText, out long actualSize) && actualSize > 0)
                {
                    _bytesWritten = actualSize;
                    
                    // Ensure file is MMF_SIZE for memory mapping
                    using (var fs = new FileStream(_currentFileName, FileMode.Open, FileAccess.Write))
                    {
                        if (fs.Length != MMF_SIZE)
                        {
                            fs.SetLength(MMF_SIZE);
                        }
                    }
                    
                    _mmf = MemoryMappedFile.CreateFromFile(
                        _currentFileName,
                        FileMode.Open,
                        null,
                        MMF_SIZE,
                        MemoryMappedFileAccess.ReadWrite);

                    _stream = _mmf.CreateViewStream(0, MMF_SIZE, MemoryMappedFileAccess.ReadWrite);
                    _stream.Position = _bytesWritten; // Continue from where we left off
                    
                    _log.Information("Reopened existing log file: {FileName} ({Bytes:N0} bytes)",
                        _currentFileName, _bytesWritten);
                    return;
                }
            }
            catch (Exception ex)
            {
                _log.Warning(ex, "Failed to read metadata file, creating new log file");
            }
        }
        
        // Create new file
        CreateNewLogFile();
    }
    
    
    /// <summary>
    /// Creates new log file (rotation)
    /// </summary>
    private void CreateNewLogFile()
    {
        _stream?.Dispose();
        _mmf?.Dispose();

        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd");
        _currentFileName = Path.Combine(_baseDirectory, $"events_{timestamp}.dat");

        _mmf = MemoryMappedFile.CreateFromFile(
            _currentFileName,
            FileMode.Create,
            null,
            MMF_SIZE,
            MemoryMappedFileAccess.ReadWrite);

        _stream = _mmf.CreateViewStream(0, MMF_SIZE, MemoryMappedFileAccess.ReadWrite);
        _bytesWritten = 0;

        _log.Information("New log file created: {FileName}", _currentFileName);
    }
    
    /// <summary>
    /// Checks buffer usage level and emits warnings
    /// </summary>
    private void CheckWatermark()
    {
        var metrics = _eventQueue.GetMetrics();
        var utilization = metrics.Utilization;
        
        if (utilization >= WATERMARK_CRITICAL)
        {
            _log.Fatal("FATAL: EventStore buffer em {Utilization:P1} ({Used}/{Capacity}). Risco de perda!",
                utilization, metrics.Used, metrics.Capacity);
        }
        else if (utilization >= WATERMARK_WARNING)
        {
            _log.Warning("WARNING: EventStore buffer at {Utilization:P1} ({Used}/{Capacity})",
                utilization, metrics.Used, metrics.Capacity);
        }
    }
    
    /// <summary>
    /// Returns EventStore metrics
    /// </summary>
    public EventStoreMetrics GetMetrics()
    {
        var bufferMetrics = _eventQueue.GetMetrics();
        return new EventStoreMetrics
        {
            EventsWritten = _currentSequenceNumber,
            EventsFlushed = _lastFlushedSequence,
            EventsDropped = _eventsDropped,
            BufferUtilization = bufferMetrics.Utilization,
            BufferUsed = bufferMetrics.Used,
            BufferCapacity = bufferMetrics.Capacity,
            BytesWritten = _bytesWritten
        };
    }

    public void Dispose()
    {
        _log.Information("Shutting down EventStore...");
        
        _shutdownEvent.Set();
        _writerThread.Join(TimeSpan.FromSeconds(10));

        // Truncate file to actual written size before closing
        if (_stream != null && _bytesWritten > 0 && !string.IsNullOrEmpty(_currentFileName))
        {
            try
            {
                _stream.Dispose();
                _mmf?.Dispose();
                
                // Reopen file and truncate to actual size
                using (var fs = new FileStream(_currentFileName, FileMode.Open, FileAccess.Write))
                {
                    fs.SetLength(_bytesWritten);
                }
                
                // Save metadata file with actual size
                var timestamp = DateTime.UtcNow.ToString("yyyyMMdd");
                var metaFileName = Path.Combine(_baseDirectory, $"events_{timestamp}.meta");
                File.WriteAllText(metaFileName, _bytesWritten.ToString());
                
                _log.Information("File truncated to {Bytes:N0} bytes ({Events} events)",
                    _bytesWritten, _currentSequenceNumber);
            }
            catch (Exception ex)
            {
                _log.Error(ex, "Failed to truncate file");
            }
        }
        else
        {
            _stream?.Dispose();
            _mmf?.Dispose();
        }
        
        _shutdownEvent.Dispose();
        _flushEvent.Dispose();

        _log.Information("EventStore shutdown. Total events: {Count}", _currentSequenceNumber);
    }

    public long CurrentSequence => Volatile.Read(ref _currentSequenceNumber);
    public long LastFlushedSequence => Volatile.Read(ref _lastFlushedSequence);
    public int QueuedEvents => _eventQueue.Count;
}
