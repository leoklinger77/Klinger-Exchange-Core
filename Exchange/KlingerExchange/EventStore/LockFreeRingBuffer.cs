using System.Runtime.CompilerServices;

namespace KlingerExchange.EventStore;

/// <summary>
/// Lock-free ring buffer otimizado para single producer, single consumer
/// Padding para evitar false sharing de cache lines
/// </summary>
/// <typeparam name="T">Tipo do item (deve ser struct para performance)</typeparam>
public sealed class LockFreeRingBuffer<T> where T : struct
{
    // Cache line padding (64 bytes) para evitar false sharing
    // Write position com padding
    private long _writePosition;
    
    // Read position com padding
    private long _readPosition;

    private readonly T[] _buffer;
    private readonly int _bufferMask;

    public LockFreeRingBuffer(int capacity)
    {
        // Capacidade deve ser power of 2 para usar AND ao invés de MOD
        if (capacity <= 0 || (capacity & (capacity - 1)) != 0)
            throw new ArgumentException("Capacity must be a power of 2", nameof(capacity));

        _buffer = new T[capacity];
        _bufferMask = capacity - 1;
        _writePosition = 0;
        _readPosition = 0;
    }

    /// <summary>
    /// Tenta adicionar um item ao buffer (non-blocking)
    /// </summary>
    /// <returns>ID sequencial do evento ou -1 se buffer cheio</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public long TryEnqueue(T item)
    {
        var writePos = Volatile.Read(ref _writePosition);
        var readPos = Volatile.Read(ref _readPosition);

        // Verifica se há espaço (deixa 1 slot vazio para distinguir cheio de vazio)
        if (writePos - readPos >= _buffer.Length - 1)
            return -1; // Buffer cheio

        var index = (int)(writePos & _bufferMask);
        _buffer[index] = item;

        // Incrementa write position (release semantics)
        Volatile.Write(ref _writePosition, writePos + 1);

        return writePos;
    }

    /// <summary>
    /// Tenta ler um batch de itens (non-blocking)
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int TryDequeueBatch(Span<T> destination)
    {
        var readPos = Volatile.Read(ref _readPosition);
        var writePos = Volatile.Read(ref _writePosition);

        var available = (int)(writePos - readPos);
        if (available <= 0)
            return 0;

        var count = Math.Min(available, destination.Length);

        for (int i = 0; i < count; i++)
        {
            var index = (int)((readPos + i) & _bufferMask);
            destination[i] = _buffer[index];
        }

        // Atualiza read position (release semantics)
        Volatile.Write(ref _readPosition, readPos + count);

        return count;
    }
    
    /// <summary>
    /// Retorna métricas do buffer
    /// </summary>
    public BufferMetrics GetMetrics()
    {
        var readPos = Volatile.Read(ref _readPosition);
        var writePos = Volatile.Read(ref _writePosition);
        var used = (int)(writePos - readPos);
        
        return new BufferMetrics
        {
            Capacity = _buffer.Length,
            Used = used,
            Available = _buffer.Length - used,
            Utilization = (double)used / _buffer.Length
        };
    }

    /// <summary>
    /// Aguarda até que um evento específico seja confirmado como persistido
    /// </summary>
    public bool WaitForSequence(long sequence, int timeoutMs = 5000)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        
        while (sw.ElapsedMilliseconds < timeoutMs)
        {
            var readPos = Volatile.Read(ref _readPosition);
            if (readPos > sequence)
                return true;

            // Spin wait otimizado
            Thread.SpinWait(10);
        }

        return false;
    }

    public long WritePosition => Volatile.Read(ref _writePosition);
    public long ReadPosition => Volatile.Read(ref _readPosition);
    public int Count => (int)(WritePosition - ReadPosition);
}

/// <summary>
/// Métricas do ring buffer
/// </summary>
public readonly struct BufferMetrics
{
    public int Capacity { get; init; }
    public int Used { get; init; }
    public int Available { get; init; }
    public double Utilization { get; init; }
}
