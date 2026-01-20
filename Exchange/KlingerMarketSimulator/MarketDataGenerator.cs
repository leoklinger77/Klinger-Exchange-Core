using QuickFix;
using QuickFix.FIX41;
using Serilog;
using System.Collections.Concurrent;

namespace KlingerSimulator;

public class MarketDataGenerator
{
    private readonly ILogger _log = Log.ForContext<MarketDataGenerator>();
    private readonly MarketSimulatorApplication _app;
    private readonly MarketDataListener _marketDataListener;
    private readonly ThreadLocal<Random> _random = new(() => new Random(Guid.NewGuid().GetHashCode()));
    private long _orderSequence;
    private CancellationTokenSource? _cts;
    private Task? _generatorTask;
    
    private readonly ConcurrentDictionary<string, decimal> _lastPrices = new();
    private readonly ConcurrentDictionary<string, InstrumentSpec> _instrumentSpecs = new();
    private string[] _hotSymbols = Array.Empty<string>();
    
    public MarketDataGenerator(MarketSimulatorApplication app, MarketDataListener marketDataListener)
    {
        _app = app;
        _marketDataListener = marketDataListener;
        
        // Initialize with safe defaults for fallback
        var symbols = new[] { "PETR4", "VALE3", "ITUB4", "BBDC4" }; // Minimal fallback
        foreach (var symbol in symbols)
        {
            _lastPrices[symbol] = 10m;
            _instrumentSpecs[symbol] = new InstrumentSpec(symbol, 100, 10m, 0.01m);
        }
    }

    public void Start()
    {
        _log.Information("Market data generator started - fetching instruments from API");
        _cts = new CancellationTokenSource();
        
        // Single optimized task instead of multiple timers
        _generatorTask = Task.Run(() => GenerateOrdersLoop(_cts.Token));
    }

    public void Stop()
    {
        _cts?.Cancel();
        _generatorTask?.Wait(TimeSpan.FromSeconds(5));
        _log.Information("Market data generator stopped");
    }

    private async Task GenerateOrdersLoop(CancellationToken ct)
    {
        // Fetch instruments from REST API
        string[] symbols = Array.Empty<string>();
        
        while (symbols.Length == 0 && !ct.IsCancellationRequested)
        {
            try
            {
                using var client = new HttpClient();
                client.Timeout = TimeSpan.FromSeconds(5);
                var response = await client.GetStringAsync("http://localhost:5000/instruments", ct);
                
                // Parse JSON to get symbols and trading params from config object
                var doc = System.Text.Json.JsonDocument.Parse(response);
                var instruments = doc.RootElement.GetProperty("instruments").EnumerateArray()
                    .Select(e => new InstrumentSpec(
                        Symbol: e.GetProperty("symbol").GetString()!,
                        LotSize: e.TryGetProperty("lotSize", out var lot) ? lot.GetInt32() : 100,
                        ReferencePrice: e.TryGetProperty("referencePrice", out var rp) ? rp.GetDecimal() : 10m,
                        TickSize: e.TryGetProperty("tickSize", out var ts) ? ts.GetDecimal() : 0.01m
                    ))
                    .ToArray();

                symbols = instruments.Select(i => i.Symbol).ToArray();
                UpdateHotSymbols(symbols, 5);

                // Initialize prices and specs from API reference prices
                foreach (var instrument in instruments)
                {
                    _instrumentSpecs[instrument.Symbol] = instrument;
                    _lastPrices[instrument.Symbol] = instrument.ReferencePrice > 0m ? instrument.ReferencePrice : 10m;
                }
                
                _log.Information("Fetched {Count} instruments from API, starting order generation", symbols.Length);
            }
            catch (Exception ex)
            {
                _log.Warning(ex, "Failed to fetch instruments from API, retrying in 2 seconds...");
                await Task.Delay(2000, ct);
            }
        }
        
        if (ct.IsCancellationRequested) return;
        
        int loopCount = 0;
        while (!ct.IsCancellationRequested)
        {
            try
            {
                if (!_app.IsConnected || _app.SessionId == null)
                {
                    loopCount++;
                    if (loopCount % 50 == 0) // Log every 5 seconds (50 * 100ms)
                        _log.Warning("Waiting for FIX connection... IsConnected={Connected}, SessionId={Session}", 
                            _app.IsConnected, _app.SessionId?.ToString() ?? "null");
                    await Task.Delay(100, ct);
                    continue;
                }

                var rnd = _random.Value!;
                var sessionId = _app.SessionId;
                
                // HIGH POWER MODE: Use ALL symbols, not just hot symbols
                var activeSymbols = symbols;
                
                // Generate orders for ALL symbols each cycle
                foreach (var symbol in activeSymbols)
                {
                    // 2-4 crossed pairs per symbol = maximum fills
                    var pairsPerSymbol = rnd.Next(2, 5);
                    for (int p = 0; p < pairsPerSymbol; p++)
                    {
                        GenerateCrossedPair(rnd, sessionId, symbol);
                    }
                }
                
                loopCount++;
                if (loopCount % 50 == 0) // Log every 2.5 seconds
                    _log.Information("Generated {Loops} batches, symbols={SymbolCount}, ~{OrdersPerBatch} orders/batch",
                        loopCount, activeSymbols.Length, activeSymbols.Length * 6); // ~3 pairs * 2 orders

                // Fast interval for maximum throughput
                await Task.Delay(50, ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _log.Error(ex, "Error generating orders");
                await Task.Delay(3000, ct);
            }
        }
    }

    private void GenerateBuyOrder(Random rnd, SessionID sessionId, string[] symbols)
    {
        var symbol = symbols[rnd.Next(symbols.Length)];
        var spec = GetInstrumentSpec(symbol);
        var quantity = GetRandomQuantity(rnd, spec);

        var lastPrice = GetLastPriceForSymbol(symbol, spec, rnd);
        
        // Buyers can be above or below market for realistic price discovery
        // 30% aggressive (above market), 40% at market, 30% passive (below market)
        var roll = rnd.Next(100);
        decimal price;
        
        if (roll < 30)
        {
            // Aggressive: pay above market (+0.1% to +1%)
            var premium = 0.001m + (decimal)rnd.NextDouble() * 0.009m;
            price = RoundToTick(lastPrice * (1m + premium), spec.TickSize);
        }
        else if (roll < 70)
        {
            // At market: very tight spread (-0.1% to +0.1%)
            var variation = ((decimal)rnd.NextDouble() - 0.5m) * 0.002m;
            price = RoundToTick(lastPrice * (1m + variation), spec.TickSize);
        }
        else
        {
            // Passive: bid below market (-0.1% to -1%)
            var discount = 0.001m + (decimal)rnd.NextDouble() * 0.009m;
            price = RoundToTick(lastPrice * (1m - discount), spec.TickSize);
        }

        SendOrder(symbol, QuickFix.Fields.Side.BUY, quantity, price, sessionId);
    }

    private void GenerateSellOrder(Random rnd, SessionID sessionId, string[] symbols)
    {
        var symbol = symbols[rnd.Next(symbols.Length)];
        var spec = GetInstrumentSpec(symbol);
        var quantity = GetRandomQuantity(rnd, spec);

        var lastPrice = GetLastPriceForSymbol(symbol, spec, rnd);
        
        // Sellers can be above or below market for realistic price discovery
        // 30% aggressive (below market), 40% at market, 30% passive (above market)
        var roll = rnd.Next(100);
        decimal price;
        
        if (roll < 30)
        {
            // Aggressive: sell below market (-0.1% to -1%)
            var discount = 0.001m + (decimal)rnd.NextDouble() * 0.009m;
            price = RoundToTick(lastPrice * (1m - discount), spec.TickSize);
        }
        else if (roll < 70)
        {
            // At market: very tight spread (-0.1% to +0.1%)
            var variation = ((decimal)rnd.NextDouble() - 0.5m) * 0.002m;
            price = RoundToTick(lastPrice * (1m + variation), spec.TickSize);
        }
        else
        {
            // Passive: ask above market (+0.1% to +1%)
            var premium = 0.001m + (decimal)rnd.NextDouble() * 0.009m;
            price = RoundToTick(lastPrice * (1m + premium), spec.TickSize);
        }

        SendOrder(symbol, QuickFix.Fields.Side.SELL, quantity, price, sessionId);
    }

    private void GenerateCrossedPair(Random rnd, SessionID sessionId, string symbol)
    {
        var spec = GetInstrumentSpec(symbol);
        var quantity = GetRandomQuantity(rnd, spec);
        var lastPrice = GetLastPriceForSymbol(symbol, spec, rnd);

        // Random market direction: 50% bullish, 50% bearish
        var isBullish = rnd.Next(100) < 50;
        
        decimal buyPrice, sellPrice;
        
        if (isBullish)
        {
            // Bullish: price drifts UP - buy aggressive, sell passive
            // Buy pays 0.5% to 2% above last, sell at last or slightly below
            var buyPremium = 0.005m + (decimal)rnd.NextDouble() * 0.015m;  // +0.5% to +2%
            var sellDiscount = (decimal)rnd.NextDouble() * 0.005m;          // 0% to -0.5%
            
            buyPrice = RoundToTick(lastPrice * (1m + buyPremium), spec.TickSize);
            sellPrice = RoundToTick(lastPrice * (1m - sellDiscount), spec.TickSize);
        }
        else
        {
            // Bearish: price drifts DOWN - sell aggressive, buy passive
            // Sell accepts 0.5% to 2% below last, buy at last or slightly above
            var sellDiscount = 0.005m + (decimal)rnd.NextDouble() * 0.015m; // -0.5% to -2%
            var buyPremium = (decimal)rnd.NextDouble() * 0.005m;             // 0% to +0.5%
            
            buyPrice = RoundToTick(lastPrice * (1m + buyPremium), spec.TickSize);
            sellPrice = RoundToTick(lastPrice * (1m - sellDiscount), spec.TickSize);
        }
        
        // Ensure minimum price
        if (sellPrice <= 0)
            sellPrice = RoundToTick(Math.Max(spec.TickSize, lastPrice * 0.95m), spec.TickSize);

        SendOrder(symbol, QuickFix.Fields.Side.BUY, quantity, buyPrice, sessionId);
        SendOrder(symbol, QuickFix.Fields.Side.SELL, quantity, sellPrice, sessionId);
    }

    private decimal GetLastPriceForSymbol(string symbol, InstrumentSpec spec, Random rnd)
    {
        // Get or initialize reference price (ensure consistency per symbol)
        if (!_lastPrices.TryGetValue(symbol, out var lastPrice))
        {
            lastPrice = spec.ReferencePrice > 0m ? spec.ReferencePrice : 10m;
            _lastPrices[symbol] = lastPrice;
        }

        // Try to get last traded price from market data
        var marketPrice = _marketDataListener.GetLastPrice(symbol);
        if (marketPrice.HasValue)
        {
            lastPrice = marketPrice.Value;
            _lastPrices[symbol] = lastPrice; // Update cache
        }

        return lastPrice;
    }

    private InstrumentSpec GetInstrumentSpec(string symbol)
    {
        return _instrumentSpecs.TryGetValue(symbol, out var spec)
            ? spec
            : new InstrumentSpec(symbol, 100, 10m, 0.01m);
    }

    private static decimal RoundToTick(decimal price, decimal tickSize)
    {
        if (tickSize <= 0)
            return Math.Round(price, 2);

        var ticks = Math.Round(price / tickSize, MidpointRounding.AwayFromZero);
        return ticks * tickSize;
    }

    private static decimal GetRandomQuantity(Random rnd, InstrumentSpec spec)
    {
        var lotSize = spec.LotSize > 0 ? spec.LotSize : 100;
        var lots = rnd.Next(1, 21); // 1 to 20 lots
        return lotSize * lots;
    }

    private void UpdateHotSymbols(string[] symbols, int maxCount)
    {
        if (symbols.Length == 0)
        {
            _hotSymbols = Array.Empty<string>();
            return;
        }

        _hotSymbols = symbols
            .Take(Math.Min(maxCount, symbols.Length))
            .ToArray();
    }

    private sealed record InstrumentSpec(string Symbol, int LotSize, decimal ReferencePrice, decimal TickSize);

    private void SendOrder(string symbol, char side, decimal quantity, decimal price, SessionID sessionId)
    {
        var clOrdId = $"SIM{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}{Interlocked.Increment(ref _orderSequence):x}";
        
        var order = new NewOrderSingle();
        order.ClOrdID = new QuickFix.Fields.ClOrdID(clOrdId);
        order.HandlInst = new QuickFix.Fields.HandlInst('1');
        order.Symbol = new QuickFix.Fields.Symbol(symbol);
        order.Side = new QuickFix.Fields.Side(side);
        order.OrdType = new QuickFix.Fields.OrdType(QuickFix.Fields.OrdType.LIMIT);
        order.Price = new QuickFix.Fields.Price(price);
        order.OrderQty = new QuickFix.Fields.OrderQty(quantity);

        Session.SendToTarget(order, sessionId);
    }
}
