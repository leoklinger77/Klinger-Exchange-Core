using KlingerExchange.Config;
using KlingerExchange.Matching.Domain;
using KlingerExchange.Matching.Domain.Enums;
using KlingerExchange.Matching.Domain.Events;
using KlingerExchange.Matching.Domain.Struct;
using KlingerExchange.Matching.Engine;
using KlingerExchange.Matching.Engine.Instrument;
using KlingerExchange.Matching.Engine.Latency;
using KlingerExchange.Matching.Engine.Repository;
using KlingerExchange.Matching.Metrics;
using KlingerExchange.Matching.Services;
using KlingerExchange.Matching.Validation;
using KlingerExchange.Matching.Validation.Rules;
using KlingerExchange.Warmup;
using QuickFix;
using QuickFix.Fields;
using Serilog;
using Serilog.Events;
using System.Diagnostics;

namespace KlingerExchange {
    public class ExchangeApplication : IApplication {
        private readonly ILogger _log = Log.ForContext<ExchangeApplication>();
        private readonly ExchangeConfig _exchangeConfig = ExchangeConfig.LoadConfig();
        private readonly MatchingEngine _matchingEngine;
        private readonly Dictionary<string, long> _clOrdIdToOrderId;
        private readonly LatencyMonitor _latencyMonitor;
        private readonly MetricsEventBus _metricsEventBus;
        private readonly OrderMetricsCollector _metricsCollector;
        private readonly ExecutionReportDispatcher _reportDispatcher;
        private OrderValidator _orderValidator = null!;
        private InstrumentValidationCache _instrumentCache = null!;
        private long _messageCount;
        private long _lastStatsTick;
        private bool _threadAffinityConfigured;

        public ExchangeApplication() {
            var repository = new OrderBookRepository();
            _matchingEngine = new MatchingEngine(repository);
            _clOrdIdToOrderId = new Dictionary<string, long>();
            _latencyMonitor = new LatencyMonitor(1000);
            _messageCount = 0;

            // Inject latency monitor into matching engine
            _matchingEngine.SetLatencyMonitor(_latencyMonitor);

            // Async logging via reactive stream
            _metricsEventBus = new MetricsEventBus(OnMetricsEvent);
            _metricsCollector = new OrderMetricsCollector(_metricsEventBus);

            // Async FIX report dispatch (removes SendToTarget from hot path)
            _reportDispatcher = new ExecutionReportDispatcher();

            if (_exchangeConfig.EnableEventStore) {
                var eventStoreDir = _exchangeConfig.EventStoreDir;
                _matchingEngine.EnableEventStore(eventStoreDir);
            } else {
                _log.Information("EventStore disabled");
            }
        }

        public void InitializeValidation() {
            // Initialize order validation - AFTER stores are loaded
            _instrumentCache = new InstrumentValidationCache();
            var rules = new IOrderValidationRule[]
            {
                new TradingSessionValidator(),
                new InstrumentStatusValidator(),
                new PriceBandValidator(),
                new OrderTypeValidator(),
                new QuantityLimitsValidator()
            };
            _orderValidator = new OrderValidator(_instrumentCache, rules);
            _log.Information("OrderValidator initialized with {Count} rules", _orderValidator.RuleCount);

            // JIT Warm-up: force compilation of the entire hot path
            var warmup = new ExchangeWarmup(
                _matchingEngine,
                _orderValidator,
                _instrumentCache,
                _latencyMonitor,
                _metricsCollector);
            warmup.Execute();
        }

        public void InjectMarketDataPublisher(MarketData.Core.RingBuffer buffer, MarketData.Core.SymbolMapper symbolMapper) {
            _matchingEngine.SetMarketDataPublisher(buffer, symbolMapper);
            _log.Information("Market data publisher injected into MatchingEngine");
        }

        public void Dispose() {
            _log.Information("Disposing ExchangeApplication...");
            _reportDispatcher.Dispose();
            _matchingEngine.Dispose();
            _log.Information("ExchangeApplication disposed");
        }

        public void FromAdmin(Message message, SessionID sessionID) {
            // Removed logging for performance
        }

        public void FromApp(Message message, SessionID sessionID) {
            var msgType = message.Header.GetString(Tags.MsgType);
            _metricsCollector.StartOrder(msgType);

            try {
                switch (msgType) {
                    case "D": // NEW_ORDER_SINGLE
                        HandleNewOrderSingle(message, sessionID);
                        break;

                    case "F": // ORDER_CANCEL_REQUEST
                        HandleOrderCancelRequest(message, sessionID);
                        break;

                    default:
                        break;
                }
            } finally {
                _metricsCollector.PublishMetrics();
            }
        }

        public void OnCreate(SessionID sessionID) {
            _log.Information("{Callback} {SessionId}", nameof(OnCreate), sessionID);
        }

        public void OnLogon(SessionID sessionID) {
            _log.Information("{Callback} {SessionId}", nameof(OnLogon), sessionID);

            if (!_threadAffinityConfigured) {
                ConfigureThreadAffinity();
                _threadAffinityConfigured = true;
            }
        }

        public void OnLogout(SessionID sessionID) {
            _log.Information("{Callback} {SessionId}", nameof(OnLogout), sessionID);

            _matchingEngine.Dispose();
        }

        public void ToAdmin(Message message, SessionID sessionID) {
            // Removed logging for performance
        }

        public void ToApp(Message message, SessionID sessionID) {
            // Removed logging for performance - adds 3-5ms due to FIX serialization
        }

        private Message BuildBusinessReject(Message originalMsg, SessionID sessionID, ValidationResult validation) {
            var reject = new QuickFix.FIX41.ExecutionReport(
                new OrderID("0"),
                new ExecID($"REJ{DateTimeOffset.UtcNow.Ticks}"),
                new ExecTransType('0'),
                new QuickFix.Fields.ExecType('8'), // Rejected
                new OrdStatus('8'), // Rejected
                new Symbol(originalMsg.GetString(Tags.Symbol)),
                new QuickFix.Fields.Side(originalMsg.GetChar(Tags.Side)),
                new OrderQty(originalMsg.GetDecimal(Tags.OrderQty)),
                new LastShares(0),
                new LastPx(0),
                new LeavesQty(0),
                new CumQty(0),
                new AvgPx(0)
            );

            reject.SetField(new ClOrdID(originalMsg.GetString(Tags.ClOrdID)));
            reject.SetField(new Text(validation.RejectReason ?? "Business reject"));

            return reject;
        }

        private void HandleNewOrderSingle(Message message, SessionID sessionID) {
            var startTicks = Stopwatch.GetTimestamp();
            var newOrder = (QuickFix.FIX41.NewOrderSingle)message;
            var (clOrdId, symbol, side, price, quantity) = FastOrderParser.ParseNewOrderSingleFast(newOrder);
            var parseTicks = Stopwatch.GetTimestamp();

            var orderRequest = new OrderRequest(
                clOrdId: clOrdId,
                symbol: symbol,
                side: side,
                price: price,
                quantity: quantity,
                orderType: OrderType.Limit
            );
            var validation = _orderValidator.Validate(orderRequest);

            if (validation.Status == ValidationStatus.Rejected) {
                var rejectMsg = BuildBusinessReject(message, sessionID, validation);
                _reportDispatcher.EnqueueReport(rejectMsg, sessionID, returnToPool: false);
                _metricsCollector.CaptureOrderData(clOrdId, symbol, side, quantity, price, 0);
                _metricsCollector.RecordTiming(0, 0, 0, 0, 0);
                return;
            }

            var matchStartTicks = Stopwatch.GetTimestamp();
            Order order;
            List<Fill> fills;

            try {
                (order, fills) = _matchingEngine.ProcessNewOrder(clOrdId, symbol, side, price, quantity);
                _clOrdIdToOrderId[clOrdId] = order.OrderId;
            } catch (Exception ex) {
                _log.Fatal(ex, "FATAL ERROR in matching: {ClOrdId} {Symbol}", clOrdId, symbol);
                var rejectValidation = ValidationResult.Rejected("Internal matching error", 99);
                var rejectMsg = BuildBusinessReject(message, sessionID, rejectValidation);
                _reportDispatcher.EnqueueReport(rejectMsg, sessionID, returnToPool: false);
                _metricsCollector.CaptureOrderData(clOrdId, symbol, side, quantity, price, 0);
                _metricsCollector.RecordTiming(0, 0, 0, 0, 0);
                return;
            }

            var matchEndTicks = Stopwatch.GetTimestamp();

            if (fills.Count == 0 || order.LeavesQty > 0) {
                var newReport = ExecutionReportBuilder.BuildNewOrderReportPooled(order, clOrdId);
                _reportDispatcher.EnqueueReport(newReport, sessionID, returnToPool: true);
            }

            foreach (var fill in fills) {
                var updatedOrder = order.WithFill(fill.Quantity);
                var fillReport = ExecutionReportBuilder.BuildFillReportPooled(updatedOrder, clOrdId, fill);
                _reportDispatcher.EnqueueReport(fillReport, sessionID, returnToPool: true);
                order = updatedOrder;
            }

            var endTicks = Stopwatch.GetTimestamp();
            var parseNs = TicksToNs(parseTicks - startTicks);
            var matchNs = TicksToNs(matchEndTicks - matchStartTicks);
            var reportSendNs = TicksToNs(endTicks - matchEndTicks);

            _metricsCollector.CaptureOrderData(clOrdId, symbol, side, quantity, price, order.OrderId);
            _metricsCollector.RecordTiming(parseNs, matchNs, reportSendNs, 0, fills.Count);
        }

        private void HandleOrderCancelRequest(Message message, SessionID sessionID) {
            var swParse = Stopwatch.StartNew();
            var cancelRequest = (QuickFix.FIX41.OrderCancelRequest)message;
            var (origClOrdId, orderId) = FastOrderParser.ParseOrderCancelRequestFast(cancelRequest);
            var clOrdId = cancelRequest.ClOrdID.Value;
            var symbol = cancelRequest.Symbol.Value;
            swParse.Stop();
            var parseNs = TicksToNs(swParse.ElapsedTicks);

            var swMatch = Stopwatch.StartNew();
            var (cancelled, side) = _matchingEngine.ProcessCancel(symbol, orderId);
            swMatch.Stop();
            var matchNs = TicksToNs(swMatch.ElapsedTicks);

            long reportNs = 0;
            long sendNs = 0;

            if (cancelled) {
                var swReport = Stopwatch.StartNew();
                var cancelReport = ExecutionReportBuilder.BuildCancelReportPooled(symbol, orderId, clOrdId, origClOrdId);
                swReport.Stop();
                reportNs = TicksToNs(swReport.ElapsedTicks);

                _reportDispatcher.EnqueueReport(cancelReport, sessionID, returnToPool: true);
            } else {
                var swReport = Stopwatch.StartNew();
                var rejectReport = ExecutionReportBuilder.BuildRejectReportPooled(clOrdId, symbol, "Order not found");
                swReport.Stop();
                reportNs = TicksToNs(swReport.ElapsedTicks);

                _reportDispatcher.EnqueueReport(rejectReport, sessionID, returnToPool: true);
            }

            _metricsCollector.CaptureOrderData(clOrdId, symbol, side, 0, 0, orderId);
            _metricsCollector.RecordTiming(parseNs, matchNs, reportNs, sendNs, 0);
        }

        private void OnMetricsEvent(OrderMetricsEvent evt) {
            try {
                // This runs on background thread - no impact on hot path
                _latencyMonitor.RecordLatency(evt.Metrics.TotalTimeNs);
                                
                if (evt.FillCount > 0 && _log.IsEnabled(LogEventLevel.Debug)) {

                    _log.Debug("{MsgType}: {Symbol} {Side} {Qty}@{Px} → {Fills} fills [{ClOrdId}]",
                        evt.MsgType, evt.Symbol, evt.Side, evt.Quantity, evt.Price, evt.FillCount, evt.ClOrdId);
                } else if (_log.IsEnabled(LogEventLevel.Debug)) {

                    _log.Debug("{MsgType}: {Symbol} {Side} {Qty}@{Px} → book [{ClOrdId}]",
                        evt.MsgType, evt.Symbol, evt.Side, evt.Quantity, evt.Price, evt.ClOrdId);
                }
                                
                if (evt.Metrics.TotalTimeNs > 100_000)
                    _log.Warning("SLOW: {Metrics}", evt.Metrics);

                var count = Interlocked.Increment(ref _messageCount);
                var now = Environment.TickCount64;
                if (now - _lastStatsTick >= 1000 || count % 100 == 0) {
                    _lastStatsTick = now;
                    var stats = _latencyMonitor.GetStats();
                    var esMetrics = _matchingEngine.GetEventStoreMetrics();

                    if (esMetrics.HasValue) {
                        var es = esMetrics.Value;
                        var bufferPct = es.BufferUtilization * 100;

                        _log.Information("STATS: {Stats} | EventStore: {Written}W/{Flushed}F/{Dropped}D (Buffer: {BufferPct:F1}%)",
                            stats, es.EventsWritten, es.EventsFlushed, es.EventsDropped, bufferPct);

                        if (!es.IsHealthy && Log.IsEnabled(LogEventLevel.Warning)) {
                            _log.Warning("EventStore unhealthy: Dropped={Dropped}, Buffer={BufferPct:F1}%",
                                es.EventsDropped, bufferPct);
                        }
                    } else {
                        _log.Information("STATS: {Stats}", stats);
                    }
                                        
                    if (stats.SampledMatches > 0) {
                        _log.Information("{BenchInfo}", stats.GetBenchInfo());
                    }
                }
            } catch (Exception ex) {
                _log.Error(ex, "Error processing metrics");
            }
        }

        private static long TicksToNs(long ticks) {
            return (long)(ticks * (1_000_000_000.0 / Stopwatch.Frequency));
        }

        // Set thread affinity to a specific CPU core to reduce context switches and CPU migrations
        // Supports both Windows and Linux for ultra-low latency
        // Uses Core 4 for matching thread (0-indexed)
        private void ConfigureThreadAffinity() {
            const int MATCHING_CORE = 4;
            bool success = KlingerShared.Threading.ThreadAffinityHelper.SetThreadAffinity(MATCHING_CORE);

            if (!success) {
                _log.Warning("Failed to configure thread affinity - continuing without CPU pinning");
            }
        }
    }
}
