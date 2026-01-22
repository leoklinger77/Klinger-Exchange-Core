using QuickFix;
using QuickFix.FIX41;
using Serilog;
using System.Collections.Concurrent;
using KlingerSimulator.Generation;
using KlingerSimulator.Generation.Strategy;
using KlingerSimulator.Generation.Randoms;
using KlingerSimulator.Generation.Performance;
using KlingerSimulator.UI;
using System.Text.Json;
using KlingerSimulator.Generation.Models;

namespace KlingerSimulator;

public class MarketDataGenerator {
    private readonly ILogger _log = Log.ForContext<MarketDataGenerator>();
    private readonly MarketSimulatorApplication _app;
    private readonly MarketDataListener _marketDataListener;
    private long _orderSequence = 0;
    private CancellationTokenSource? _cts;
    private Task? _generatorTask;
    private InteractiveConsole? _console;

    private readonly ConcurrentDictionary<string, decimal> _lastPrices = new();
    private readonly ConcurrentDictionary<string, InstrumentSpec> _instrumentSpecs = new();
    private string[] _symbols = Array.Empty<string>();
        
    private long _totalOrdersSent = 0;
    private long _totalPairsGenerated = 0;
    private DateTime _lastStatsUpdate = DateTime.UtcNow;
    private long _ordersSinceLastUpdate = 0;

    public MarketDataGenerator(MarketSimulatorApplication app, MarketDataListener marketDataListener) {
        _app = app;
        _marketDataListener = marketDataListener;

        // Initialize with safe defaults for fallback
        var defaultSymbols = new[] { "PETR4", "VALE3", "ITUB4", "BBDC4" };
        foreach (var symbol in defaultSymbols) {
            _lastPrices[symbol] = 10m;
            _instrumentSpecs[symbol] = new InstrumentSpec(symbol, 100, 10m, 0.01m);
        }
    }

    public void Start(InteractiveConsole? console = null) {
        _console = console;
        _log.Information("Market data generator started - fetching instruments from API");
        _console?.LogMessage("Market data generator starting...");

        _cts = new CancellationTokenSource();

        _generatorTask = Task.Run(() => GenerateOrdersLoop(_cts.Token));
    }

    public void Stop() {
        _cts?.Cancel();
        _generatorTask?.Wait(TimeSpan.FromSeconds(5));
        _log.Information("Market data generator stopped");
    }

    private async Task GenerateOrdersLoop(CancellationToken ct) {
        while (_symbols.Length == 0 && !ct.IsCancellationRequested) {
            try {
                _console?.LogMessage("Connecting to API...");
                using var client = new HttpClient();
                client.Timeout = TimeSpan.FromSeconds(5);
                var response = await client.GetStringAsync("http://localhost:5000/instruments", ct);

                var doc = JsonDocument.Parse(response);
                var instruments = doc.RootElement.GetProperty("instruments").EnumerateArray()
                    .Select(e => new InstrumentSpec(
                        Symbol: e.GetProperty("symbol").GetString()!,
                        LotSize: e.TryGetProperty("lotSize", out var lot) ? lot.GetInt32() : 100,
                        ReferencePrice: e.TryGetProperty("referencePrice", out var rp) ? rp.GetDecimal() : 10m,
                        TickSize: e.TryGetProperty("tickSize", out var ts) ? ts.GetDecimal() : 0.01m
                    ))
                    .ToArray();

                _symbols = [.. instruments.Select(i => i.Symbol)];
                                
                foreach (var instrument in instruments) {
                    _instrumentSpecs[instrument.Symbol] = instrument;
                    _lastPrices[instrument.Symbol] = instrument.ReferencePrice > 0m ? instrument.ReferencePrice : 10m;
                }

                _log.Information("Fetched {Count} instruments from API, starting order generation", _symbols.Length);
                _console?.LogMessage($"Loaded {_symbols.Length} instruments");
            } catch (Exception ex) {
                _log.Warning(ex, "Failed to fetch instruments from API, retrying in 2 seconds...");
                _console?.LogMessage("Failed to connect to API, retrying...");
                await Task.Delay(2000, ct);
            }
        }

        if (ct.IsCancellationRequested) return;

        _console?.LogMessage("Generator ready!");

        var context = new OrderGenerationContext {
            Symbols = _symbols,
            GetLastPrice = GetLastPriceForSymbol,
            GetInstrumentSpec = GetInstrumentSpec,
            OrderSequence = _orderSequence
        };

        var generator = new Generator<OrderRequest[]>(
            new CrossedPairStrategy(context),
            new XorShiftRandom((ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()),
            new LowLatencyProfile()
        );

        int loopCount = 0;
        while (!ct.IsCancellationRequested) {
            try {
                if (_console?.IsPaused == true) {
                    await Task.Delay(100, ct);
                    continue;
                }

                if (!_app.IsConnected || _app.SessionId == null) {
                    loopCount++;
                    if (loopCount % 50 == 0)
                    {
                        _log.Warning("Waiting for FIX connection... IsConnected={Connected}, SessionId={Session}",
                            _app.IsConnected, _app.SessionId?.ToString() ?? "null");
                        _console?.LogMessage("Waiting for FIX connection...");
                    }
                    await Task.Delay(100, ct);
                    continue;
                }

                var sessionId = _app.SessionId;

                var intensity = _console?.Intensity ?? 50;
                var baseOrdersPerBatch = _symbols.Length * 3;
                var ordersPerBatch = (int)(baseOrdersPerBatch * (intensity / 100.0));
                ordersPerBatch = Math.Max(1, ordersPerBatch);

                generator.Generate(ordersPerBatch, orderPair => {
                    foreach (var order in orderPair) {
                        SendOrder(order, sessionId);
                        Interlocked.Increment(ref _totalOrdersSent);
                        Interlocked.Increment(ref _ordersSinceLastUpdate);
                    }
                    Interlocked.Increment(ref _totalPairsGenerated);
                });

                var now = DateTime.UtcNow;
                if ((now - _lastStatsUpdate).TotalSeconds >= 1.0) {
                    var elapsed = (now - _lastStatsUpdate).TotalSeconds;
                    var ordersPerSecond = (int)(_ordersSinceLastUpdate / elapsed);

                    _console?.UpdateStats(ordersPerSecond, _totalOrdersSent, _totalPairsGenerated, _app.IsConnected);

                    _ordersSinceLastUpdate = 0;
                    _lastStatsUpdate = now;
                }

                loopCount++;
                if (loopCount % 100 == 0)
                {
                    _console?.LogMessage($"Generated {_totalPairsGenerated:N0} pairs, {_totalOrdersSent:N0} orders");
                }

                var baseDelay = 50;
                var delay = Math.Max(10, baseDelay - (int)(intensity * 0.4)); // 50ms at 0%, 10ms at 100%
                await Task.Delay(delay, ct);
            } catch (OperationCanceledException) {
                break;
            } catch (Exception ex) {
                _log.Error(ex, "Error generating orders");
                _console?.LogMessage($"Error: {ex.Message}");
                await Task.Delay(3000, ct);
            }
        }
    }

    private decimal GetLastPriceForSymbol(string symbol) {
        var spec = GetInstrumentSpec(symbol);

        if (!_lastPrices.TryGetValue(symbol, out var lastPrice)) {
            lastPrice = spec.ReferencePrice > 0m ? spec.ReferencePrice : 10m;
            _lastPrices[symbol] = lastPrice;
        }

        var marketPrice = _marketDataListener.GetLastPrice(symbol);
        if (marketPrice.HasValue) {
            lastPrice = marketPrice.Value;
            _lastPrices[symbol] = lastPrice;
        }

        return lastPrice;
    }

    private InstrumentSpec GetInstrumentSpec(string symbol) {
        return _instrumentSpecs.TryGetValue(symbol, out var spec)
            ? spec
            : new InstrumentSpec(symbol, 100, 10m, 0.01m);
    }

    private void SendOrder(OrderRequest order, SessionID sessionId) {
        var fixOrder = new NewOrderSingle();
        fixOrder.ClOrdID = new QuickFix.Fields.ClOrdID(order.ClOrdId);
        fixOrder.HandlInst = new QuickFix.Fields.HandlInst('1');
        fixOrder.Symbol = new QuickFix.Fields.Symbol(order.Symbol);
        fixOrder.Side = new QuickFix.Fields.Side(order.Side);
        fixOrder.OrdType = new QuickFix.Fields.OrdType(QuickFix.Fields.OrdType.LIMIT);
        fixOrder.Price = new QuickFix.Fields.Price(order.Price);
        fixOrder.OrderQty = new QuickFix.Fields.OrderQty(order.Quantity);

        Session.SendToTarget(fixOrder, sessionId);
    }
}
