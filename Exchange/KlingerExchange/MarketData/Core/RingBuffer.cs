using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace KlingerExchange.MarketData.Core;

/// <summary>
/// Lock-free SPSC (Single Producer Single Consumer) ring buffer for TradeMessages.
/// Uses unsafe pinned memory for zero-copy operations.
/// Target: Write ~5-10ns, Read ~3ns.
/// </summary>
public unsafe class RingBuffer : IDisposable {
    private readonly TradeMessage[] _buffer;
    private readonly GCHandle _handle;
    private readonly TradeMessage* _ptr;
    private readonly int _mask;

    // Interlocked operations provide memory barriers
    private long _writePos = 0;
    private long _readPos = 0;

    public const int DefaultSize = 65536; // 64K messages = 4MB buffer

    public RingBuffer(int size = DefaultSize) {
        if ((size & (size - 1)) != 0)
            throw new ArgumentException("Size must be power of 2", nameof(size));

        _buffer = GC.AllocateArray<TradeMessage>(size, pinned: true);
        _handle = GCHandle.Alloc(_buffer, GCHandleType.Pinned);
        _ptr = (TradeMessage*)_handle.AddrOfPinnedObject();
        _mask = size - 1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryWrite(in TradeMessage message) {
        var currentWrite = _writePos;
        var currentRead = _readPos;

        if (currentWrite - currentRead >= _mask)
            return false;

        var index = (int)(currentWrite & _mask);
        _ptr[index] = message;

        Interlocked.Increment(ref _writePos);
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryRead(out TradeMessage message) {
        var currentRead = _readPos;
        var currentWrite = _writePos;

        if (currentRead >= currentWrite) {
            message = default;
            return false;
        }

        var index = (int)(currentRead & _mask);
        message = _ptr[index];

        Interlocked.Increment(ref _readPos);
        return true;
    }

    public long Available => _writePos - _readPos;

    public int Capacity => _mask + 1;

    public bool IsEmpty => _readPos >= _writePos;

    public bool IsFull => (_writePos - _readPos) >= _mask;

    public void Dispose() {
        if (_handle.IsAllocated)
            _handle.Free();
    }
}
