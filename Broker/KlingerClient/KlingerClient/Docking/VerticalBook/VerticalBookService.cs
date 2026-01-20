using KlingerClient.Services;
using System.Collections.Concurrent;
using System.Reactive.Linq;

namespace KlingerClient.Docking.VerticalBook;

/// <summary>
/// Service that manages the vertical book state and business logic
/// </summary>
public sealed class VerticalBookService : IDisposable
{
    private readonly MarketDataService _marketData;
    private IDisposable? _subscription;
    private readonly SynchronizationContext? _uiContext;

    // Book structure: symbolIndex -> (price -> quantity)
    private readonly ConcurrentDictionary<short, SortedDictionary<decimal, long>> _bids = new();
    private readonly ConcurrentDictionary<short, SortedDictionary<decimal, long>> _asks = new();

    // Working orders: symbolIndex -> (price -> (side, quantity))
    private readonly ConcurrentDictionary<short, ConcurrentDictionary<decimal, (Side side, int qty)>> _workingOrders = new();

    // Last traded price per symbol
    private readonly ConcurrentDictionary<short, decimal> _lastTradedPrice = new();
    
    // Tick size per symbol
    private readonly ConcurrentDictionary<short, decimal> _tickSizes = new();

    private readonly Dictionary<short, string> _symbolNames = new();
    private long _messagesReceived = 0;
    private short _currentSymbol = 0;
    private const int DEPTH = 50; // Show more depth for scrolling

    public event EventHandler? BookUpdated;
    public event EventHandler? SymbolsInitialized;

    public short CurrentSymbol
    {
        get => _currentSymbol;
        set
        {
            if (_currentSymbol != value)
            {
                _currentSymbol = value;
                BookUpdated?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public long MessagesReceived => _messagesReceived;
    public IReadOnlyDictionary<short, string> SymbolNames => _symbolNames;

    public VerticalBookService(MarketDataService marketData)
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
            .ObserveOn(SynchronizationContext.Current!)
            .Subscribe(instruments =>
            {
                foreach (var instrument in instruments)
                {
                    InitializeInstrument(instrument);
                }
                
                SymbolsInitialized?.Invoke(this, EventArgs.Empty);
            });
        
        // If we already have instruments, initialize immediately
        if (_marketData.Instruments != null)
        {
            foreach (var instrument in _marketData.Instruments)
            {
                InitializeInstrument(instrument);
            }
            
            SymbolsInitialized?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// Initialize an instrument with its reference price to show initial book
    /// </summary>
    private void InitializeInstrument(InstrumentInfo instrument)
    {
        var symbolIndex = instrument.SymbolIndex;
        var symbol = instrument.Symbol;
        var referencePrice = instrument.ReferencePrice > 0 ? instrument.ReferencePrice : instrument.PreviousClose;
        var tickSize = instrument.TickSize > 0 ? instrument.TickSize : 0.01m;
        
        _symbolNames[symbolIndex] = symbol;
        _tickSizes[symbolIndex] = tickSize;
        _bids[symbolIndex] = new SortedDictionary<decimal, long>(Comparer<decimal>.Create((a, b) => b.CompareTo(a)));
        _asks[symbolIndex] = new SortedDictionary<decimal, long>();
        _workingOrders[symbolIndex] = new ConcurrentDictionary<decimal, (Side, int)>();
        
        // Initialize book with reference price if available
        if (referencePrice > 0)
        {
            _lastTradedPrice[symbolIndex] = referencePrice;

            // Ensure reference price exists in book for LTP alignment
            _bids[symbolIndex][referencePrice] = 0;
            _asks[symbolIndex][referencePrice] = 0;
            
            // Create initial book levels around reference price
            for (int i = 1; i <= DEPTH; i++)
            {
                var bidPrice = Math.Round(referencePrice - i * tickSize, GetDecimalPlaces(tickSize));
                var askPrice = Math.Round(referencePrice + i * tickSize, GetDecimalPlaces(tickSize));
                
                // Simulated initial depth (will be replaced by real data)
                var qty = 100 * (DEPTH + 1 - i);
                
                _bids[symbolIndex][bidPrice] = qty;
                _asks[symbolIndex][askPrice] = qty;
            }
        }
    }

    /// <summary>
    /// Get number of decimal places for tick size
    /// </summary>
    private static int GetDecimalPlaces(decimal tickSize)
    {
        if (tickSize >= 1) return 0;
        if (tickSize >= 0.1m) return 1;
        if (tickSize >= 0.01m) return 2;
        if (tickSize >= 0.001m) return 3;
        if (tickSize >= 0.0001m) return 4;
        return 5;
    }

    private void SubscribeToMarketData()
    {
        _subscription = _marketData.TradeStream
            .Buffer(TimeSpan.FromMilliseconds(250))
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

                        _lastTradedPrice[trade.SymbolIndex] = trade.Price;

                        if (trade.SymbolIndex == _currentSymbol)
                        {
                            ProcessTradeBook(trade);
                            shouldUpdate = true;
                        }
                    }

                    if (shouldUpdate)
                        RaiseBookUpdated();
                },
                onError: ex => Console.WriteLine($"Market data error: {ex.Message}")
            );
    }

    private void ProcessTradeBook(TradeMessage trade)
    {
        var price = trade.Price;
        var qty = trade.Quantity;
        var symbolIdx = trade.SymbolIndex;

        if (!_bids.ContainsKey(symbolIdx) || !_asks.ContainsKey(symbolIdx))
            return;

        lock (_bids[symbolIdx])
        {
            if (_bids[symbolIdx].Count > 200)
                _bids[symbolIdx].Clear();

            for (int i = 1; i <= DEPTH; i++)
            {
                var bidPrice = Math.Round(price - i * 0.01m, 2);
                var bidQty = qty * (51 - i) / 3;
                _bids[symbolIdx][bidPrice] = bidQty;
            }
        }

        lock (_asks[symbolIdx])
        {
            if (_asks[symbolIdx].Count > 200)
                _asks[symbolIdx].Clear();

            for (int i = 1; i <= DEPTH; i++)
            {
                var askPrice = Math.Round(price + i * 0.01m, 2);
                var askQty = qty * (51 - i) / 3;
                _asks[symbolIdx][askPrice] = askQty;
            }
        }
    }

    private void RaiseBookUpdated()
    {
        if (_uiContext != null)
            _uiContext.Post(_ => BookUpdated?.Invoke(this, EventArgs.Empty), null);
        else
            BookUpdated?.Invoke(this, EventArgs.Empty);
    }

    public decimal? GetLastTradedPrice(short symbolIndex)
    {
        return _lastTradedPrice.TryGetValue(symbolIndex, out var price) ? price : null;
    }

    public decimal GetTickSize(short symbolIndex)
    {
        return _tickSizes.TryGetValue(symbolIndex, out var tickSize) ? tickSize : 0.01m;
    }

    public List<(decimal price, long bidQty, long askQty)> GetConsolidatedBook(short symbolIndex)
    {
        if (!_bids.TryGetValue(symbolIndex, out var bids) ||
            !_asks.TryGetValue(symbolIndex, out var asks))
        {
            return new List<(decimal, long, long)>();
        }

        // Get all unique prices from both sides
        var allPrices = new SortedSet<decimal>(Comparer<decimal>.Create((a, b) => b.CompareTo(a)));
        
        foreach (var bid in bids.Take(DEPTH))
            allPrices.Add(bid.Key);
        
        foreach (var ask in asks.Take(DEPTH))
            allPrices.Add(ask.Key);

        // Build consolidated book
        var result = new List<(decimal price, long bidQty, long askQty)>();
        
        foreach (var price in allPrices)
        {
            var bidQty = bids.TryGetValue(price, out var bq) ? bq : 0;
            var askQty = asks.TryGetValue(price, out var aq) ? aq : 0;
            result.Add((price, bidQty, askQty));
        }

        return result;
    }

    public (List<KeyValuePair<decimal, long>> bids, List<KeyValuePair<decimal, long>> asks) GetBookLevels(short symbolIndex)
    {
        if (!_bids.TryGetValue(symbolIndex, out var bids) ||
            !_asks.TryGetValue(symbolIndex, out var asks))
        {
            return (new List<KeyValuePair<decimal, long>>(), new List<KeyValuePair<decimal, long>>());
        }

        var bidLevels = bids.Take(DEPTH).ToList();
        var askLevels = asks.Take(DEPTH).Reverse().ToList();

        return (bidLevels, askLevels);
    }

    public (decimal bestBid, decimal bestAsk, decimal spread)? GetSpread(short symbolIndex)
    {
        var (bids, asks) = GetBookLevels(symbolIndex);

        if (bids.Any() && asks.Any())
        {
            var bestBid = bids.First().Key;
            var bestAsk = asks.Last().Key;
            var spread = bestAsk - bestBid;
            return (bestBid, bestAsk, spread);
        }

        return null;
    }

    public void Dispose()
    {
        _subscription?.Dispose();
    }

    // Working orders management
    public void AddWorkingOrder(short symbolIndex, decimal price, Side side, int qty)
    {
        if (_workingOrders.TryGetValue(symbolIndex, out var orders))
        {
            orders.AddOrUpdate(price, (side, qty), (_, existing) =>
            {
                // Aggregate quantities for same side at same price
                if (existing.side == side)
                    return (side, existing.qty + qty);
                return (side, qty);
            });
            BookUpdated?.Invoke(this, EventArgs.Empty);
        }
    }

    public void UpdateWorkingOrder(short symbolIndex, decimal price, Side side, int qtyChange)
    {
        if (_workingOrders.TryGetValue(symbolIndex, out var orders))
        {
            if (orders.TryGetValue(price, out var existing) && existing.side == side)
            {
                int newQty = existing.qty + qtyChange;
                if (newQty <= 0)
                {
                    orders.TryRemove(price, out _);
                }
                else
                {
                    orders[price] = (side, newQty);
                }
                BookUpdated?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public void RemoveWorkingOrder(short symbolIndex, decimal price)
    {
        if (_workingOrders.TryGetValue(symbolIndex, out var orders))
        {
            orders.TryRemove(price, out _);
            BookUpdated?.Invoke(this, EventArgs.Empty);
        }
    }

    public (int buyQty, int sellQty) GetWorkingQty(short symbolIndex, decimal price)
    {
        if (_workingOrders.TryGetValue(symbolIndex, out var orders) &&
            orders.TryGetValue(price, out var working))
        {
            return working.side == Side.Buy ? (working.qty, 0) : (0, working.qty);
        }
        return (0, 0);
    }
}
