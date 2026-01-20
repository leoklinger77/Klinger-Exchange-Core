using KlingerClient.Services;
using System.Collections.Concurrent;
using System.Reactive.Linq;

namespace KlingerClient.Docking.Marketwatch;

/// <summary>
/// Represents a quote for display in the marketwatch
/// </summary>
public sealed class MarketQuote
{
    public short SymbolIndex { get; set; }
    public string Symbol { get; set; } = string.Empty;
    public decimal LastPrice { get; set; }
    public decimal Change { get; set; }
    public decimal ChangePercent { get; set; }
    public decimal ReferencePrice { get; set; }
    public long Volume { get; set; }
    public long TradeCount { get; set; }
    public DateTime LastUpdate { get; set; }
}

/// <summary>
/// Service that manages marketwatch data
/// </summary>
public sealed class MarketwatchService : IDisposable
{
    private readonly MarketDataService _marketData;
    private IDisposable? _subscription;
    private readonly SynchronizationContext? _uiContext;
    private long _lastUiUpdateTicks;
    private const int UiUpdateIntervalMs = 50;

    // Watched symbols with their quotes
    private readonly ConcurrentDictionary<short, MarketQuote> _quotes = new();
    
    // Symbol metadata
    private readonly Dictionary<short, InstrumentInfo> _instruments = new();
    private readonly Dictionary<short, string> _symbolNames = new();

    public event EventHandler? QuotesUpdated;
    public event EventHandler? SymbolsInitialized;

    public IReadOnlyDictionary<short, string> SymbolNames => _symbolNames;
    public IReadOnlyDictionary<short, InstrumentInfo> Instruments => _instruments;
    public IReadOnlyCollection<MarketQuote> WatchedQuotes => _quotes.Values.ToList();

    public MarketwatchService(MarketDataService marketData)
    {
        _marketData = marketData;
        _uiContext = SynchronizationContext.Current;
        InitializeSymbols();
        SubscribeToMarketData();
    }

    private void InitializeSymbols()
    {
        // Subscribe to instrument list
        _marketData.InstrumentListStream
            .Take(1)
            .Subscribe(instruments =>
            {
                foreach (var instrument in instruments)
                {
                    _symbolNames[instrument.SymbolIndex] = instrument.Symbol;
                    _instruments[instrument.SymbolIndex] = instrument;
                }
                RaiseSymbolsInitialized();
            });

        // If already have instruments
        if (_marketData.Instruments != null)
        {
            foreach (var instrument in _marketData.Instruments)
            {
                _symbolNames[instrument.SymbolIndex] = instrument.Symbol;
                _instruments[instrument.SymbolIndex] = instrument;
            }
            RaiseSymbolsInitialized();
        }
    }

    private void SubscribeToMarketData()
    {
        _subscription = _marketData.TradeStream
            .Buffer(TimeSpan.FromMilliseconds(50)) // Near real-time updates
            .Where(trades => trades.Any())
            .Subscribe(trades =>
            {
                bool updated = false;
                
                foreach (var trade in trades)
                {
                    // Only update if symbol is being watched
                    if (_quotes.TryGetValue(trade.SymbolIndex, out var quote))
                    {
                        quote.LastPrice = trade.Price;
                        quote.Volume += trade.Quantity;
                        quote.TradeCount++;
                        quote.LastUpdate = DateTime.Now;
                        
                        // Calculate change from reference
                        if (quote.ReferencePrice > 0)
                        {
                            quote.Change = quote.LastPrice - quote.ReferencePrice;
                            quote.ChangePercent = (quote.Change / quote.ReferencePrice) * 100m;
                        }
                        
                        updated = true;
                    }
                }
                
                if (updated)
                    RaiseQuotesUpdated();
            });
    }

    private void RaiseQuotesUpdated()
    {
        var now = Environment.TickCount64;
        if (now - _lastUiUpdateTicks < UiUpdateIntervalMs)
            return;

        _lastUiUpdateTicks = now;

        if (_uiContext != null)
            _uiContext.Post(_ => QuotesUpdated?.Invoke(this, EventArgs.Empty), null);
        else
            QuotesUpdated?.Invoke(this, EventArgs.Empty);
    }

    private void RaiseSymbolsInitialized()
    {
        if (_uiContext != null)
            _uiContext.Post(_ => SymbolsInitialized?.Invoke(this, EventArgs.Empty), null);
        else
            SymbolsInitialized?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Add a symbol to the watchlist
    /// </summary>
    public bool AddSymbol(short symbolIndex)
    {
        if (_quotes.ContainsKey(symbolIndex))
            return false; // Already watching

        if (!_instruments.TryGetValue(symbolIndex, out var instrument))
            return false; // Unknown symbol

        var quote = new MarketQuote
        {
            SymbolIndex = symbolIndex,
            Symbol = instrument.Symbol,
            ReferencePrice = instrument.ReferencePrice > 0 ? instrument.ReferencePrice : instrument.PreviousClose,
            LastPrice = instrument.ReferencePrice > 0 ? instrument.ReferencePrice : instrument.PreviousClose,
            LastUpdate = DateTime.Now
        };

        _quotes[symbolIndex] = quote;
        QuotesUpdated?.Invoke(this, EventArgs.Empty);
        return true;
    }

    /// <summary>
    /// Remove a symbol from the watchlist
    /// </summary>
    public bool RemoveSymbol(short symbolIndex)
    {
        if (_quotes.TryRemove(symbolIndex, out _))
        {
            QuotesUpdated?.Invoke(this, EventArgs.Empty);
            return true;
        }
        return false;
    }

    /// <summary>
    /// Check if a symbol is being watched
    /// </summary>
    public bool IsWatching(short symbolIndex) => _quotes.ContainsKey(symbolIndex);

    /// <summary>
    /// Get all available symbols not yet in watchlist
    /// </summary>
    public IEnumerable<(short Index, string Symbol)> GetAvailableSymbols()
    {
        return _symbolNames
            .Where(kvp => !_quotes.ContainsKey(kvp.Key))
            .Select(kvp => (kvp.Key, kvp.Value))
            .OrderBy(x => x.Value);
    }

    public void Dispose()
    {
        _subscription?.Dispose();
    }
}
