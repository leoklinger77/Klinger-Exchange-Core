using KlingerExchange.Matching.Domain;
using KlingerExchange.Matching.Domain.Enums;
using KlingerExchange.Matching.Domain.Struct;
using KlingerExchange.Matching.Engine;
using KlingerExchange.Matching.Engine.Instrument;
using KlingerExchange.Matching.Engine.Latency;
using KlingerExchange.Matching.Metrics;
using KlingerExchange.Matching.Services;
using KlingerExchange.Matching.Validation;
using Serilog;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace KlingerExchange.Warmup;

/// <summary>
/// JIT Warm-up service for the Exchange.
/// Executes dummy operations to force compilation of the entire hot path.
/// Eliminates latency of 30ms+ on true first-order transactions.
/// </summary>
public sealed class ExchangeWarmup {
    private readonly ILogger _log = Log.ForContext<ExchangeWarmup>();
    private readonly MatchingEngine _matchingEngine;
    private readonly OrderValidator _orderValidator;
    private readonly InstrumentValidationCache _instrumentCache;
    private readonly LatencyMonitor _latencyMonitor;
    private readonly OrderMetricsCollector _metricsCollector;

    // Warmup configuration
    private const int WARMUP_ITERATIONS = 200;
    private const int EXECUTION_REPORT_WARMUP_COUNT = 50;
    private const int METRICS_WARMUP_COUNT = 50;
    private const int LATENCY_MONITOR_WARMUP_COUNT = 100;

    public ExchangeWarmup(
        MatchingEngine matchingEngine,
        OrderValidator orderValidator,
        InstrumentValidationCache instrumentCache,
        LatencyMonitor latencyMonitor,
        OrderMetricsCollector metricsCollector) {
        _matchingEngine = matchingEngine;
        _orderValidator = orderValidator;
        _instrumentCache = instrumentCache;
        _latencyMonitor = latencyMonitor;
        _metricsCollector = metricsCollector;
    }


    public void Execute() {
        _log.Information("Starting JIT warm-up...");
        var sw = Stopwatch.StartNew();

        // Use symbols that exist in cache
        var symbols = GetWarmupSymbols();

        // Warm-up ExecutionReportBuilder (Creates and discards messages)
        WarmupExecutionReports(symbols[0]);

        // Warm-up MetricsCollector
        WarmupMetrics();

        // Execute multiple commands for warming matching + validation
        WarmupMatchingEngine(symbols);

        // GC Force to start clean
        ForceGarbageCollection();

        sw.Stop();
        _log.Information("JIT warm-up completed in {ElapsedMs}ms", sw.ElapsedMilliseconds);
    }

    private string[] GetWarmupSymbols() {
        return _instrumentCache.GetInstrument("PETR4") != null
            ? ["PETR4", "VALE3", "ITUB4"]
            : ["TEST1", "TEST2", "TEST3"];
    }

    private void WarmupMatchingEngine(string[] symbols) {
        for (int i = 0; i < WARMUP_ITERATIONS; i++) {
            var symbol = symbols[i % symbols.Length];
            var side = i % 2 == 0 ? Side.Buy : Side.Sell;
            var price = 10.00m + (i % 10) * 0.01m;
            var qty = 100m;

            var request = new OrderRequest(
                clOrdId: $"WARMUP{i}",
                symbol: symbol,
                side: side,
                price: price,
                quantity: qty,
                orderType: OrderType.Limit
            );
            _orderValidator.Validate(request);

            // Process order (heats up matching engine)
            try {
                var (order, fills) = _matchingEngine.ProcessNewOrder($"WARMUP{i}", symbol, side, price, qty);

                // Warm-up ExecutionReportBuilder with real order
                if (i < 10) {
                    var report = ExecutionReportBuilder.BuildNewOrderReportPooled(order, $"WARMUP{i}");
                    ExecutionReportBuilder.ReturnToPool(report);

                    foreach (var fill in fills) {
                        var fillReport = ExecutionReportBuilder.BuildFillReportPooled(order, $"WARMUP{i}", fill);
                        ExecutionReportBuilder.ReturnToPool(fillReport);
                    }
                }

                // Cancel to clear the book
                if (order.LeavesQty > 0)
                    _matchingEngine.ProcessCancel(symbol, order.OrderId);
            } catch {
                // Ignore errors during warm-up
            }
        }
    }

    private void WarmupExecutionReports(string symbol) {
        // Create dummy order to generate reports
        var dummyOrder = new Order(0, "WARMUP", symbol, Side.Buy, 10.0m, 100m, DateTime.UtcNow.Ticks);
        var dummyFill = new Fill {
            BuyOrderId = 0,
            SellOrderId = 1,
            Price = 10.0m,
            Quantity = 100m,
            TimestampTicks = DateTime.UtcNow.Ticks
        };

        // Heat up all builder methods
        for (int i = 0; i < EXECUTION_REPORT_WARMUP_COUNT; i++) {
            var newReport = ExecutionReportBuilder.BuildNewOrderReportPooled(dummyOrder, "WARMUP");
            ExecutionReportBuilder.ReturnToPool(newReport);

            var fillReport = ExecutionReportBuilder.BuildFillReportPooled(dummyOrder, "WARMUP", dummyFill);
            ExecutionReportBuilder.ReturnToPool(fillReport);

            var cancelReport = ExecutionReportBuilder.BuildCancelReportPooled(symbol, 0, "WARMUP", "ORIG");
            ExecutionReportBuilder.ReturnToPool(cancelReport);

            var rejectReport = ExecutionReportBuilder.BuildRejectReportPooled("WARMUP", symbol, "Test");
            ExecutionReportBuilder.ReturnToPool(rejectReport);
        }
    }

    private void WarmupMetrics() {
        for (int i = 0; i < METRICS_WARMUP_COUNT; i++) {
            _metricsCollector.StartOrder("D");
            _metricsCollector.CaptureOrderData("WARMUP", "TEST", Side.Buy, 100, 10.0m, 0);
            _metricsCollector.RecordTiming(1000, 2000, 500, 100, 1);
        }

        for (int i = 0; i < LATENCY_MONITOR_WARMUP_COUNT; i++) {
            _latencyMonitor.RecordLatency(100_000 + i * 1000);
        }

        PrepareHotPathMethods();
    }

    private void PrepareHotPathMethods() {
        var typesToPrepare = new[]
        {
            typeof(FastOrderParser),
            typeof(ExecutionReportBuilder),
            typeof(MatchingEngine),
            typeof(FastOrderBook),
            typeof(FastPriceLevel),
            typeof(OrderValidator),
            typeof(InstrumentValidationCache),
            typeof(ExchangeApplication)
        };

        foreach (var type in typesToPrepare) {
            foreach (var method in type.GetMethods(
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.Static)) {
                if (method.IsAbstract || method.ContainsGenericParameters)
                    continue;

                try {
                    RuntimeHelpers.PrepareMethod(method.MethodHandle);
                } catch { }
            }
        }
    }

    private static void ForceGarbageCollection() {
        GC.Collect(2, GCCollectionMode.Forced, true, true);
        GC.WaitForPendingFinalizers();
        GC.Collect(2, GCCollectionMode.Forced, true, true);
    }
}
