using KlingerClient.Services;
using Serilog;

namespace KlingerClient.Docking.Charts;

/// <summary>
/// Service for aggregating trades into candles in real-time
/// High-performance implementation with minimal allocations
/// </summary>
public sealed class ChartsService : IDisposable
{
    private readonly ILogger _logger = Log.ForContext<ChartsService>();
    private readonly MarketDataService _marketData;
    private readonly Dictionary<short, Dictionary<ChartTimeframe, CandleBuffer>> _candlesBySymbol = new();
    private readonly object _lock = new();
    
    private const int BUFFER_CAPACITY = 2000; // Max candles per timeframe

    public event EventHandler<CandleUpdateEventArgs>? CandleUpdated;
    public event EventHandler<CandleUpdateEventArgs>? NewCandleCreated;
    public event EventHandler? OnSymbolsUpdated;

    public short CurrentSymbol { get; set; }
    public ChartTimeframe CurrentTimeframe { get; set; } = ChartTimeframe.S5;
    
    public Dictionary<short, string> SymbolNames { get; } = new();

    public ChartsService(MarketDataService marketData)
    {
        _marketData = marketData;
        _marketData.TradeStream.Subscribe(OnTrade);
        _marketData.InstrumentListStream.Subscribe(OnInstrumentList);
        
        // Initialize from cached instruments
        if (_marketData.Instruments != null)
        {
            foreach (var inst in _marketData.Instruments)
            {
                SymbolNames[inst.SymbolIndex] = inst.Symbol;
            }
        }
    }

    private void OnInstrumentList(InstrumentInfo[] instruments)
    {
        lock (_lock)
        {
            SymbolNames.Clear();
            foreach (var inst in instruments)
            {
                SymbolNames[inst.SymbolIndex] = inst.Symbol;
            }
        }
        
        OnSymbolsUpdated?.Invoke(this, EventArgs.Empty);
    }

    private void OnTrade(TradeMessage trade)
    {
        var symbolIndex = trade.SymbolIndex;
        var price = trade.Price;
        var quantity = trade.Quantity;
        var timestampNs = trade.TimestampNs;

        // Convert nanoseconds to ticks (100ns units)
        var timestampTicks = timestampNs / 100;
        var tradeTime = new DateTime(timestampTicks, DateTimeKind.Utc);

        lock (_lock)
        {
            // Ensure symbol buffers exist
            if (!_candlesBySymbol.TryGetValue(symbolIndex, out var timeframeBuffers))
            {
                timeframeBuffers = new Dictionary<ChartTimeframe, CandleBuffer>();
                foreach (ChartTimeframe tf in Enum.GetValues<ChartTimeframe>())
                {
                    timeframeBuffers[tf] = new CandleBuffer(BUFFER_CAPACITY);
                }
                _candlesBySymbol[symbolIndex] = timeframeBuffers;
            }

            // Update all timeframes
            foreach (var (timeframe, buffer) in timeframeBuffers)
            {
                var periodSeconds = (int)timeframe;
                var candleOpenTicks = GetCandleOpenTicks(timestampTicks, periodSeconds);
                
                bool isNewCandle = false;
                
                if (buffer.TryGetLast(out var lastCandle))
                {
                    if (lastCandle.TimestampTicks == candleOpenTicks)
                    {
                        // Update existing candle
                        ref var candleRef = ref buffer.GetLastRef();
                        candleRef.Update(price, quantity);
                    }
                    else
                    {
                        // New candle period
                        var newCandle = CandleData.Create(candleOpenTicks, price, quantity);
                        buffer.Add(newCandle);
                        isNewCandle = true;
                    }
                }
                else
                {
                    // First candle
                    var newCandle = CandleData.Create(candleOpenTicks, price, quantity);
                    buffer.Add(newCandle);
                    isNewCandle = true;
                }

                // Only fire events for current symbol/timeframe to minimize overhead
                if (symbolIndex == CurrentSymbol && timeframe == CurrentTimeframe)
                {
                    var args = new CandleUpdateEventArgs(symbolIndex, timeframe);
                    if (isNewCandle)
                        NewCandleCreated?.Invoke(this, args);
                    else
                        CandleUpdated?.Invoke(this, args);
                }
            }
        }
    }

    private static long GetCandleOpenTicks(long timestampTicks, int periodSeconds)
    {
        // Align to period boundary
        var ticksPerSecond = TimeSpan.TicksPerSecond;
        var periodTicks = periodSeconds * ticksPerSecond;
        return (timestampTicks / periodTicks) * periodTicks;
    }

    public CandleBuffer? GetBuffer(short symbolIndex, ChartTimeframe timeframe)
    {
        lock (_lock)
        {
            if (_candlesBySymbol.TryGetValue(symbolIndex, out var timeframeBuffers))
            {
                if (timeframeBuffers.TryGetValue(timeframe, out var buffer))
                {
                    return buffer;
                }
            }
        }
        return null;
    }

    public int GetCandles(short symbolIndex, ChartTimeframe timeframe, Span<CandleData> destination, int offset = 0)
    {
        lock (_lock)
        {
            var buffer = GetBuffer(symbolIndex, timeframe);
            if (buffer == null) return 0;
            return buffer.GetVisibleCandles(destination, offset, destination.Length);
        }
    }

    public (decimal min, decimal max) GetPriceRange(short symbolIndex, ChartTimeframe timeframe, int candleCount, int offset = 0)
    {
        Span<CandleData> candles = stackalloc CandleData[Math.Min(candleCount, 500)];
        int count = GetCandles(symbolIndex, timeframe, candles, offset);
        
        if (count == 0) return (0, 0);

        decimal min = decimal.MaxValue;
        decimal max = decimal.MinValue;

        for (int i = 0; i < count; i++)
        {
            if (candles[i].Low < min) min = candles[i].Low;
            if (candles[i].High > max) max = candles[i].High;
        }

        return (min, max);
    }

    public void Dispose()
    {
        _candlesBySymbol.Clear();
    }
}

public sealed class CandleUpdateEventArgs : EventArgs
{
    public short SymbolIndex { get; }
    public ChartTimeframe Timeframe { get; }

    public CandleUpdateEventArgs(short symbolIndex, ChartTimeframe timeframe)
    {
        SymbolIndex = symbolIndex;
        Timeframe = timeframe;
    }
}
