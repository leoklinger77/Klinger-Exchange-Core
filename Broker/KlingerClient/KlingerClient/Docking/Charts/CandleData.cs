using System.Runtime.InteropServices;

namespace KlingerClient.Docking.Charts;

/// <summary>
/// Chart timeframe enumeration
/// </summary>
public enum ChartTimeframe
{
    S1 = 1,      // 1 second
    S5 = 5,      // 5 seconds
    S15 = 15,    // 15 seconds
    M1 = 60,     // 1 minute
    M5 = 300,    // 5 minutes
}

/// <summary>
/// Chart display type
/// </summary>
public enum ChartType
{
    Candlestick,
    Line,
    Area
}

/// <summary>
/// Candle data structure - optimized for memory layout
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 8)]
public struct CandleData
{
    public long TimestampTicks;  // DateTime.Ticks for the candle open
    public decimal Open;
    public decimal High;
    public decimal Low;
    public decimal Close;
    public long Volume;
    public int TradeCount;

    public readonly DateTime OpenTime => new(TimestampTicks, DateTimeKind.Utc);

    public readonly bool IsBullish => Close >= Open;

    public readonly decimal Range => High - Low;

    public readonly decimal Body => Math.Abs(Close - Open);

    public void Update(decimal price, long quantity)
    {
        if (High < price) High = price;
        if (Low > price) Low = price;
        Close = price;
        Volume += quantity;
        TradeCount++;
    }

    public static CandleData Create(long timestampTicks, decimal price, long quantity)
    {
        return new CandleData
        {
            TimestampTicks = timestampTicks,
            Open = price,
            High = price,
            Low = price,
            Close = price,
            Volume = quantity,
            TradeCount = 1
        };
    }
}

/// <summary>
/// Circular buffer for candles - lock-free for single producer
/// </summary>
public sealed class CandleBuffer
{
    private readonly CandleData[] _buffer;
    private readonly int _capacity;
    private int _head;  // Next write position
    private int _count;

    public int Count => _count;
    public int Capacity => _capacity;

    public CandleBuffer(int capacity)
    {
        _capacity = capacity;
        _buffer = new CandleData[capacity];
        _head = 0;
        _count = 0;
    }

    public void Add(in CandleData candle)
    {
        _buffer[_head] = candle;
        _head = (_head + 1) % _capacity;
        if (_count < _capacity) _count++;
    }

    public void UpdateLast(in CandleData candle)
    {
        if (_count == 0) return;
        int lastIndex = (_head - 1 + _capacity) % _capacity;
        _buffer[lastIndex] = candle;
    }

    public ref CandleData GetLastRef()
    {
        if (_count == 0) throw new InvalidOperationException("Buffer is empty");
        int lastIndex = (_head - 1 + _capacity) % _capacity;
        return ref _buffer[lastIndex];
    }

    public bool TryGetLast(out CandleData candle)
    {
        if (_count == 0)
        {
            candle = default;
            return false;
        }
        int lastIndex = (_head - 1 + _capacity) % _capacity;
        candle = _buffer[lastIndex];
        return true;
    }

    /// <summary>
    /// Get candles in chronological order (oldest first)
    /// </summary>
    public void CopyTo(Span<CandleData> destination)
    {
        if (_count == 0) return;

        int start = (_head - _count + _capacity) % _capacity;
        int toCopy = Math.Min(_count, destination.Length);

        for (int i = 0; i < toCopy; i++)
        {
            destination[i] = _buffer[(start + i) % _capacity];
        }
    }

    /// <summary>
    /// Get visible candles for rendering (returns actual count)
    /// </summary>
    public int GetVisibleCandles(Span<CandleData> destination, int offset, int count)
    {
        if (_count == 0) return 0;

        int available = Math.Max(0, _count - offset);
        int toCopy = Math.Min(available, count);
        
        int start = (_head - _count + _capacity) % _capacity;
        start = (start + offset) % _capacity;

        for (int i = 0; i < toCopy; i++)
        {
            destination[i] = _buffer[(start + i) % _capacity];
        }

        return toCopy;
    }

    public void Clear()
    {
        _head = 0;
        _count = 0;
    }
}
