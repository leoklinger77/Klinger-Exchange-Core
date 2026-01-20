using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace KlingerExchange.MarketData.Core;

/// <summary>
/// Lock-free SPSC (Single Producer Single Consumer) ring buffer for TradeMessages.
/// Uses unsafe pinned memory for zero-copy operations.
/// Target: Write ~5-10ns, Read ~3ns.
/// </summary>
public unsafe class RingBuffer : IDisposable
{
    private readonly TradeMessage[] _buffer;
    private readonly GCHandle _handle;
    private readonly TradeMessage* _ptr;
    private readonly int _mask;
    
    // Interlocked operations provide memory barriers
    private long _writePos = 0;
    private long _readPos = 0;
    
    public const int DefaultSize = 65536; // 64K messages = 4MB buffer
    
    public RingBuffer(int size = DefaultSize)
    {
        if ((size & (size - 1)) != 0)
            throw new ArgumentException("Size must be power of 2", nameof(size));
        
        _buffer = GC.AllocateArray<TradeMessage>(size, pinned: true);
        _handle = GCHandle.Alloc(_buffer, GCHandleType.Pinned);
        _ptr = (TradeMessage*)_handle.AddrOfPinnedObject();
        _mask = size - 1;
    }
    
    /// <summary>Try to write a message to the buffer (non-blocking)</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryWrite(in TradeMessage message)
    {
        var currentWrite = _writePos;
        var currentRead = _readPos;
        
        // Check if buffer is full (leave one slot empty to distinguish full from empty)
        if (currentWrite - currentRead >= _mask)
            return false;
        
        // Write directly to pinned memory (zero-copy)
        var index = (int)(currentWrite & _mask);
        _ptr[index] = message;
        
        // Advance write position with memory barrier
        Interlocked.Increment(ref _writePos);
        return true;
    }
    
    /// <summary>Try to read a message from the buffer (non-blocking)</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryRead(out TradeMessage message)
    {
        var currentRead = _readPos;
        var currentWrite = _writePos;
        
        // Check if buffer is empty
        if (currentRead >= currentWrite)
        {
            message = default;
            return false;
        }
        
        // Read directly from pinned memory (zero-copy)
        var index = (int)(currentRead & _mask);
        message = _ptr[index];
        
        // Advance read position with memory barrier
        Interlocked.Increment(ref _readPos);
        return true;
    }
    
    /// <summary>Get number of messages available to read</summary>
    public long Available => _writePos - _readPos;
    
    /// <summary>Get buffer capacity</summary>
    public int Capacity => _mask + 1;
    
    /// <summary>Check if buffer is empty</summary>
    public bool IsEmpty => _readPos >= _writePos;
    
    /// <summary>Check if buffer is full</summary>
    public bool IsFull => (_writePos - _readPos) >= _mask;
    
    public void Dispose()
    {
        if (_handle.IsAllocated)
            _handle.Free();
    }
}
