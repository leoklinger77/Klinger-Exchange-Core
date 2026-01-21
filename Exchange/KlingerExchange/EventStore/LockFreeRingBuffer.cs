using KlingerExchange.EventStore.StructModels;
using System.Runtime.CompilerServices;

namespace KlingerExchange.EventStore;

/// <summary>
/// Lock-free ring buffer optimized for single producer, single consumer
/// Padding to avoid cache line false sharing
/// </summary>
/// <typeparam name="T">Item type (must be struct for performance)</typeparam>
public sealed class LockFreeRingBuffer<T> where T : struct
{
    private long _writePosition;
 
    private long _readPosition;

    private readonly T[] _buffer;
    private readonly int _bufferMask;

    public LockFreeRingBuffer(int capacity)
    {
        // Capacity must be power of 2 to use AND instead of MOD
        if (capacity <= 0 || (capacity & (capacity - 1)) != 0)
            throw new ArgumentException("Capacity must be a power of 2", nameof(capacity));

        _buffer = new T[capacity];
        _bufferMask = capacity - 1;
        _writePosition = 0;
        _readPosition = 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public long TryEnqueue(T item)
    {
        var writePos = Volatile.Read(ref _writePosition);
        var readPos = Volatile.Read(ref _readPosition);

        if (writePos - readPos >= _buffer.Length - 1)
            return -1; // Buffer full

        var index = (int)(writePos & _bufferMask);
        _buffer[index] = item;

        Volatile.Write(ref _writePosition, writePos + 1);

        return writePos;
    }

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

        // Update read position (release semantics)
        Volatile.Write(ref _readPosition, readPos + count);

        return count;
    }

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

    public bool WaitForSequence(long sequence, int timeoutMs = 5000)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        
        while (sw.ElapsedMilliseconds < timeoutMs)
        {
            var readPos = Volatile.Read(ref _readPosition);
            if (readPos > sequence)
                return true;

            // Optimized spin wait
            Thread.SpinWait(10);
        }

        return false;
    }

    public long WritePosition => Volatile.Read(ref _writePosition);
    public long ReadPosition => Volatile.Read(ref _readPosition);
    public int Count => (int)(WritePosition - ReadPosition);
}
