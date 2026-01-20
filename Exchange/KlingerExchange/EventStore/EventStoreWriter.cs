using System.IO.MemoryMappedFiles;
using ZeroFormatter;
using Serilog;

namespace KlingerExchange.EventStore;

/// <summary>
/// Métricas do EventStore
/// </summary>
public readonly struct EventStoreMetrics
{
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

/// <summary>
/// Container para um evento + payload serializado
/// </summary>
internal struct EventContainer
{
    public EventHeader Header;
    public byte[] Payload;
}

/// <summary>
/// EventStore otimizado com Memory-Mapped Files e Group Commit
/// Arquitetura: Lock-free queue -> Background writer -> MMF com fsync
/// Target: <10µs latência, zero loss, 100K+ ops/sec
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
    
    // Configurações otimizadas para low latency + durability
    private const long MMF_SIZE = 1024L * 1024 * 1024; // 1GB pre-allocated
    private const int QUEUE_SIZE = 131072; // 128K eventos (doubled for safety)
    private const int BATCH_SIZE = 100; // Eventos por batch
    private const int FLUSH_INTERVAL_MS = 1; // Flush a cada 1ms (máxima durabilidade)
    private const double WATERMARK_WARNING = 0.90; // 90% capacity
    private const double WATERMARK_CRITICAL = 0.95; // 95% capacity

    public EventStoreWriter(string baseDirectory)
    {
        _baseDirectory = baseDirectory;
        _eventQueue = new LockFreeRingBuffer<EventContainer>(QUEUE_SIZE);
        _shutdownEvent = new ManualResetEventSlim(false);
        _flushEvent = new ManualResetEventSlim(false);
        _currentSequenceNumber = 0;
        _lastFlushedSequence = -1;
        
        Directory.CreateDirectory(_baseDirectory);
        CreateNewLogFile();

        // Start background writer thread
        _writerThread = new Thread(WriterThreadLoop)
        {
            Name = "EventStore-Writer",
            IsBackground = false, // Importante: não é background para garantir flush no shutdown
            Priority = ThreadPriority.Highest
        };
        _writerThread.Start();

        _log.Information("EventStore iniciado em {Directory}", _baseDirectory);
    }

    /// <summary>
    /// Enfileira evento para persistência (non-blocking, ~50ns)
    /// Retorna sequence number ou -1 se buffer cheio
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
            _log.Error("🔴 CRITICAL: EventStore buffer cheio! Evento {SeqNum} perdido. Total dropped: {Dropped}",
                container.Header.SequenceNumber, _eventsDropped);
            return -1;
        }

        return sequenceId;
    }

    /// <summary>
    /// Aguarda até que o evento seja persistido no disco (durável)
    /// Target: ~5-10µs em média (amortizado pelo batch)
    /// </summary>
    public bool WaitForPersistence(long sequenceId, int timeoutMs = 5000)
    {
        // Fast path: já foi persistido?
        if (Volatile.Read(ref _lastFlushedSequence) >= sequenceId)
            return true;

        // Aguarda flush
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < timeoutMs)
        {
            if (Volatile.Read(ref _lastFlushedSequence) >= sequenceId)
                return true;

            // Sinaliza que há waiters (otimização)
            _flushEvent.Wait(1);
        }

        _log.Warning("Timeout aguardando persistência do evento {SequenceId}", sequenceId);
        return false;
    }

    /// <summary>
    /// Writer thread: consome eventos e faz group commit
    /// </summary>
    private void WriterThreadLoop()
    {
        var batch = new EventContainer[BATCH_SIZE];
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var watermarkCheckTimer = System.Diagnostics.Stopwatch.StartNew();

        _log.Information("Writer thread iniciado");

        while (!_shutdownEvent.IsSet)
        {
            try
            {
                // Tenta pegar um batch
                var count = _eventQueue.TryDequeueBatch(batch.AsSpan());

                // Se pegou eventos OU passou tempo limite, faz flush
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
                    // Sem eventos, aguarda um pouco
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
                _log.Error(ex, "Erro no writer thread");
            }
        }

        // Flush final no shutdown
        var finalBatch = new EventContainer[BATCH_SIZE];
        var finalCount = _eventQueue.TryDequeueBatch(finalBatch.AsSpan());
        if (finalCount > 0)
        {
            WriteBatch(finalBatch.AsSpan(0, finalCount));
            FlushToDisk();
        }

        _log.Information("Writer thread finalizado");
    }

    /// <summary>
    /// Escreve batch no MMF (ainda em memória)
    /// </summary>
    private void WriteBatch(Span<EventContainer> batch)
    {
        if (_stream == null)
            throw new InvalidOperationException("Stream não inicializado");

        foreach (var container in batch)
        {
            // Escreve header
            var headerBytes = ZeroFormatterSerializer.Serialize(container.Header);
            _stream.Write(headerBytes, 0, headerBytes.Length);

            // Escreve payload
            _stream.Write(container.Payload, 0, container.Payload.Length);

            _bytesWritten += headerBytes.Length + container.Payload.Length;

            // Rotaciona arquivo se necessário (evita arquivos muito grandes)
            if (_bytesWritten > MMF_SIZE * 0.9) // 90% do tamanho
            {
                FlushToDisk();
                CreateNewLogFile();
            }
        }

        // Atualiza última sequência escrita (ainda não durável)
        if (batch.Length > 0)
        {
            var lastSeq = batch[batch.Length - 1].Header.SequenceNumber;
            // Ainda não atualiza _lastFlushedSequence, só após fsync
        }
    }

    /// <summary>
    /// Força flush para disco (fsync) - Garante durabilidade
    /// </summary>
    private void FlushToDisk()
    {
        if (_stream == null)
            return;

        _stream.Flush(); // Força fsync no Windows

        // Agora podemos considerar como durável
        var writePos = _eventQueue.ReadPosition;
        Volatile.Write(ref _lastFlushedSequence, writePos);
    }

    /// <summary>
    /// Cria novo arquivo de log (rotação)
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

        _log.Information("Novo arquivo de log criado: {FileName}", _currentFileName);
    }
    
    /// <summary>
    /// Verifica o nível de uso do buffer e emite warnings
    /// </summary>
    private void CheckWatermark()
    {
        var metrics = _eventQueue.GetMetrics();
        var utilization = metrics.Utilization;
        
        if (utilization >= WATERMARK_CRITICAL)
        {
            _log.Error("🔴 CRITICAL: EventStore buffer em {Utilization:P1} ({Used}/{Capacity}). Risco de perda!",
                utilization, metrics.Used, metrics.Capacity);
        }
        else if (utilization >= WATERMARK_WARNING)
        {
            _log.Warning("⚠️ WARNING: EventStore buffer em {Utilization:P1} ({Used}/{Capacity})",
                utilization, metrics.Used, metrics.Capacity);
        }
    }
    
    /// <summary>
    /// Retorna métricas do EventStore
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
        _log.Information("Encerrando EventStore...");
        
        _shutdownEvent.Set();
        _writerThread.Join(TimeSpan.FromSeconds(10));

        _stream?.Dispose();
        _mmf?.Dispose();
        _shutdownEvent.Dispose();
        _flushEvent.Dispose();

        _log.Information("EventStore encerrado. Total de eventos: {Count}", _currentSequenceNumber);
    }

    public long CurrentSequence => Volatile.Read(ref _currentSequenceNumber);
    public long LastFlushedSequence => Volatile.Read(ref _lastFlushedSequence);
    public int QueuedEvents => _eventQueue.Count;
}
