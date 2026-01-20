using System.Collections.Concurrent;
using System.Reactive.Linq;
using KlingerClient.Services;

namespace KlingerClient.Docking.OrderBook;

public sealed class OrderBookService : IDisposable
{
    private readonly MarketDataService _marketData;
    private IDisposable? _subscription;
    private readonly SynchronizationContext? _uiContext;
    private long _lastUiUpdateTicks;
    private const int UiUpdateIntervalMs = 100;

    private readonly ConcurrentDictionary<short, List<TradeMessage>> _trades = new();
    private readonly Dictionary<short, string> _symbolNames = new();
    private long _messagesReceived = 0;
    private short _currentSymbol = 0;

    public event EventHandler? TradesUpdated;
    public event EventHandler? SymbolsInitialized;

    public short CurrentSymbol
    {
        get => _currentSymbol;
        set
        {
            if (_currentSymbol != value)
            {
                _currentSymbol = value;
                TradesUpdated?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public long MessagesReceived => _messagesReceived;
    public IReadOnlyDictionary<short, string> SymbolNames => _symbolNames;

    public OrderBookService(MarketDataService marketData)
    {
        _marketData = marketData;
        _uiContext = SynchronizationContext.Current;
        InitializeSymbols();
        SubscribeToMarketData();
    }

    private void InitializeSymbols()
    {
        // Subscribe to instrument list from Exchange
        _marketData.InstrumentListStream
            .Take(1)
            .Subscribe(instruments =>
            {
                foreach (var instrument in instruments)
                {
                    _symbolNames[instrument.SymbolIndex] = instrument.Symbol;
                }

                RaiseSymbolsInitialized();
            });
        
        // If we already have instruments, initialize immediately
        if (_marketData.Instruments != null)
        {
            foreach (var instrument in _marketData.Instruments)
            {
                _symbolNames[instrument.SymbolIndex] = instrument.Symbol;
            }

            RaiseSymbolsInitialized();
        }
    }

    private void SubscribeToMarketData()
    {
        _subscription = _marketData.TradeStream
            .Buffer(TimeSpan.FromMilliseconds(300))
            .Where(trades => trades.Any())
            .Subscribe(
                onNext: trades =>
                {
                    bool shouldUpdate = false;

                    foreach (var trade in trades)
                    {
                        _messagesReceived++;

                        if (!string.IsNullOrWhiteSpace(trade.Symbol) && !_symbolNames.ContainsKey(trade.SymbolIndex))
                        {
                            _symbolNames[trade.SymbolIndex] = trade.Symbol;
                            SymbolsInitialized?.Invoke(this, EventArgs.Empty);
                        }

                        // Store trade
                        _trades.AddOrUpdate(
                            trade.SymbolIndex,
                            _ => new List<TradeMessage> { trade },
                            (_, list) =>
                            {
                                list.Insert(0, trade); // Newest first
                                if (list.Count > 100) list.RemoveAt(list.Count - 1);
                                return list;
                            });

                        // Check if we need to update for current symbol
                        if (trade.SymbolIndex == _currentSymbol)
                        {
                            shouldUpdate = true;
                        }
                    }

                    // Update UI once per batch
                    if (shouldUpdate)
                        RaiseTradesUpdated();
                },
                onError: ex => Console.WriteLine($"Market data error: {ex.Message}")
            );
    }

    private void RaiseTradesUpdated()
    {
        var now = Environment.TickCount64;
        if (now - _lastUiUpdateTicks < UiUpdateIntervalMs)
            return;

        _lastUiUpdateTicks = now;

        if (_uiContext != null)
            _uiContext.Post(_ => TradesUpdated?.Invoke(this, EventArgs.Empty), null);
        else
            TradesUpdated?.Invoke(this, EventArgs.Empty);
    }

    private void RaiseSymbolsInitialized()
    {
        if (_uiContext != null)
            _uiContext.Post(_ => SymbolsInitialized?.Invoke(this, EventArgs.Empty), null);
        else
            SymbolsInitialized?.Invoke(this, EventArgs.Empty);
    }

    public List<TradeMessage> GetTrades(short symbolIndex, int maxCount = 50)
    {
        if (_trades.TryGetValue(symbolIndex, out var tradeList))
        {
            lock (tradeList)
            {
                return tradeList.Take(maxCount).ToList();
            }
        }

        return new List<TradeMessage>();
    }

    public int GetTradeCount(short symbolIndex)
    {
        return _trades.TryGetValue(symbolIndex, out var tradeList) ? tradeList.Count : 0;
    }

    public void Dispose()
    {
        _subscription?.Dispose();
    }
}
