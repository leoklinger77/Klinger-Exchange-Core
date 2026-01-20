using KlingerExchange.Matching.Engine;
using KlingerExchange.Matching.Events;
using KlingerExchange.Matching.Metrics;
using KlingerExchange.Matching.Services;
using KlingerExchange.Matching.Validation;
using KlingerExchange.Matching.Validation.Rules;
using KlingerExchange.Matching.Domain;
using QuickFix;
using Serilog;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Serilog.Events;

namespace KlingerExchange {
    public class OmsApplication : IApplication {
        private readonly ILogger _log = Log.ForContext<OmsApplication>();
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

        public OmsApplication() {
            var repository = new OrderBookRepository();
            _matchingEngine = new MatchingEngine(repository);
            _clOrdIdToOrderId = new Dictionary<string, long>();
            _latencyMonitor = new LatencyMonitor(1000);
            _messageCount = 0;

            // Async logging via reactive stream
            _metricsEventBus = new MetricsEventBus(OnMetricsEvent);
            _metricsCollector = new OrderMetricsCollector(_metricsEventBus);
            
            // Async FIX report dispatch (removes SendToTarget from hot path)
            _reportDispatcher = new ExecutionReportDispatcher();
            
            // EventStore sempre habilitado para durabilidade
            // Para desabilitar: defina KLINGER_EVENTSTORE=0
            var disableEventStore = Environment.GetEnvironmentVariable("KLINGER_EVENTSTORE") == "0";
            if (!disableEventStore)
            {
                var eventStoreDir = @"D:\Logs\EventStore";
                _matchingEngine.EnableEventStore(eventStoreDir);
                _log.Information("EventStore habilitado em: {Directory}", eventStoreDir);
            }
            else
            {
                _log.Information("EventStore desabilitado (KLINGER_EVENTSTORE=0)");
            }
        }

        /// <summary>Initialize validation after stores are loaded</summary>
        public void InitializeValidation()
        {
            // Inicializa validação de ordens - APÓS stores estarem carregados
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
            _log.Information("OrderValidator inicializado com {Count} regras", _orderValidator.RuleCount);
            
            // JIT Warm-up: força compilação de todo o hot path
            WarmupJit();
        }

        /// <summary>Inject market data dependencies (called from OmsAcceptors)</summary>
        public void InjectMarketDataPublisher(MarketData.Core.RingBuffer buffer, MarketData.Core.SymbolMapper symbolMapper)
        {
            _matchingEngine.SetMarketDataPublisher(buffer, symbolMapper);
            _log.Information("Market data publisher injected into MatchingEngine");
        }

        public void FromAdmin(Message message, SessionID sessionID) {
            // Removed logging for performance
        }

        public void FromApp(Message message, SessionID sessionID) {
            var msgType = message.Header.GetString(QuickFix.Fields.Tags.MsgType);
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
            }
            finally {
                _metricsCollector.PublishMetrics();
            }
        }
                
        public void OnCreate(SessionID sessionID) {
            _log.Information("{Callback} {SessionId}", nameof(OnCreate), sessionID);
        }

        public void OnLogon(SessionID sessionID) {
            _log.Information("{Callback} {SessionId}", nameof(OnLogon), sessionID);
        }

        public void OnLogout(SessionID sessionID) {
            _log.Information("{Callback} {SessionId}", nameof(OnLogout), sessionID);
            
            // Shutdown graceful do EventStore
            _matchingEngine.Dispose();
        }

        public void ToAdmin(Message message, SessionID sessionID) {
            // Removed logging for performance
        }

        public void ToApp(Message message, SessionID sessionID) {
            // Removed logging for performance - adds 3-5ms due to FIX serialization
        }

        /// <summary>
        /// Constrói FIX ExecutionReport com status Rejected
        /// Usado quando ordem falha nas regras de pré-validação
        /// </summary>
        private Message BuildBusinessReject(Message originalMsg, SessionID sessionID, ValidationResult validation)
        {
            var reject = new QuickFix.FIX41.ExecutionReport(
                new QuickFix.Fields.OrderID("0"),
                new QuickFix.Fields.ExecID($"REJ{DateTimeOffset.UtcNow.Ticks}"),
                new QuickFix.Fields.ExecTransType('0'),
                new QuickFix.Fields.ExecType('8'), // Rejected
                new QuickFix.Fields.OrdStatus('8'), // Rejected
                new QuickFix.Fields.Symbol(originalMsg.GetString(QuickFix.Fields.Tags.Symbol)),
                new QuickFix.Fields.Side(originalMsg.GetChar(QuickFix.Fields.Tags.Side)),
                new QuickFix.Fields.OrderQty(originalMsg.GetDecimal(QuickFix.Fields.Tags.OrderQty)),
                new QuickFix.Fields.LastShares(0),
                new QuickFix.Fields.LastPx(0),
                new QuickFix.Fields.LeavesQty(0),
                new QuickFix.Fields.CumQty(0),
                new QuickFix.Fields.AvgPx(0)
            );

            reject.SetField(new QuickFix.Fields.ClOrdID(originalMsg.GetString(QuickFix.Fields.Tags.ClOrdID)));
            reject.SetField(new QuickFix.Fields.Text(validation.RejectReason ?? "Business reject"));

            return reject;
        }

        private void HandleNewOrderSingle(Message message, SessionID sessionID) {
            var startTicks = Stopwatch.GetTimestamp();
            var newOrder = (QuickFix.FIX41.NewOrderSingle)message;
            var (clOrdId, symbol, side, price, quantity) = FastOrderParser.ParseNewOrderSingleFast(newOrder);
            var parseTicks = Stopwatch.GetTimestamp();

            // PRE-VALIDATION: Valida regras de negócio antes de matching
            var orderRequest = new OrderRequest(
                clOrdId: clOrdId,
                symbol: symbol,
                side: side,
                price: price,
                quantity: quantity,
                orderType: OrderType.Limit
            );
            var validation = _orderValidator.Validate(orderRequest);

            // Se rejeitado, envia BusinessReject (async)
            if (validation.Status == ValidationStatus.Rejected) {
                var rejectMsg = BuildBusinessReject(message, sessionID, validation);
                _reportDispatcher.EnqueueReport(rejectMsg, sessionID, returnToPool: false);
                _metricsCollector.CaptureOrderData(clOrdId, symbol, side.ToString(), quantity, price, 0);
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
                _log.Error(ex, "ERRO CRÍTICO no matching: {ClOrdId} {Symbol}", clOrdId, symbol);
                var rejectValidation = ValidationResult.Rejected("Internal matching error", 99);
                var rejectMsg = BuildBusinessReject(message, sessionID, rejectValidation);
                _reportDispatcher.EnqueueReport(rejectMsg, sessionID, returnToPool: false);
                _metricsCollector.CaptureOrderData(clOrdId, symbol, side.ToString(), quantity, price, 0);
                _metricsCollector.RecordTiming(0, 0, 0, 0, 0);
                return;
            }

            var matchEndTicks = Stopwatch.GetTimestamp();

            // ASYNC: Enqueue reports for background dispatch (~50ns vs ~100µs)
            // Send New acknowledgement if order enters book
            if (fills.Count == 0 || order.LeavesQty > 0) {
                var newReport = ExecutionReportBuilderV2.BuildNewOrderReportPooled(order, clOrdId);
                _reportDispatcher.EnqueueReport(newReport, sessionID, returnToPool: true);
            }

            // Send fill reports
            foreach (var fill in fills) {
                var updatedOrder = order.WithFill(fill.Quantity);
                var fillReport = ExecutionReportBuilderV2.BuildFillReportPooled(updatedOrder, clOrdId, fill);
                _reportDispatcher.EnqueueReport(fillReport, sessionID, returnToPool: true);
                order = updatedOrder;
            }

            var endTicks = Stopwatch.GetTimestamp();
            var parseNs = TicksToNs(parseTicks - startTicks);
            var matchNs = TicksToNs(matchEndTicks - matchStartTicks);
            var reportSendNs = TicksToNs(endTicks - matchEndTicks);

            _metricsCollector.CaptureOrderData(clOrdId, symbol, side.ToString(), quantity, price, order.OrderId);
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
            var cancelled = _matchingEngine.ProcessCancel(symbol, orderId);
            swMatch.Stop();
            var matchNs = TicksToNs(swMatch.ElapsedTicks);

            long reportNs = 0;
            long sendNs = 0;

            if (cancelled) {
                var swReport = Stopwatch.StartNew();
                var cancelReport = ExecutionReportBuilderV2.BuildCancelReportPooled(symbol, orderId, clOrdId, origClOrdId);
                swReport.Stop();
                reportNs = TicksToNs(swReport.ElapsedTicks);

                // ASYNC dispatch
                _reportDispatcher.EnqueueReport(cancelReport, sessionID, returnToPool: true);
            } else {
                var swReport = Stopwatch.StartNew();
                var rejectReport = ExecutionReportBuilderV2.BuildRejectReportPooled(clOrdId, symbol, "Order not found");
                swReport.Stop();
                reportNs = TicksToNs(swReport.ElapsedTicks);

                // ASYNC dispatch
                _reportDispatcher.EnqueueReport(rejectReport, sessionID, returnToPool: true);
            }

            _metricsCollector.CaptureOrderData(clOrdId, symbol, "Cancel", 0, 0, orderId);
            _metricsCollector.RecordTiming(parseNs, matchNs, reportNs, sendNs, 0);
        }

        private void OnMetricsEvent(OrderMetricsEvent evt) {
            try {
                // This runs on background thread - no impact on hot path
                _latencyMonitor.RecordLatency(evt.Metrics.TotalTimeNs);

                // Log.Debug removed for performance (saves ~2ms per order)
                if (evt.FillCount > 0 && _log.IsEnabled(LogEventLevel.Debug))
                    _log.Debug("{MsgType}: {Symbol} {Side} {Qty}@{Px} → {Fills} fills [{ClOrdId}]",
                        evt.MsgType, evt.Symbol, evt.Side, evt.Quantity, evt.Price, evt.FillCount, evt.ClOrdId);
                else if (_log.IsEnabled(LogEventLevel.Debug))
                    _log.Debug("{MsgType}: {Symbol} {Side} {Qty}@{Px} → book [{ClOrdId}]",
                        evt.MsgType, evt.Symbol, evt.Side, evt.Quantity, evt.Price, evt.ClOrdId);

                // Log only truly slow orders (>1ms becomes >100µs threshold)
                if (evt.Metrics.TotalTimeNs > 100_000)
                    _log.Warning("SLOW: {Metrics}", evt.Metrics.ToString());

                // Log aggregate stats + EventStore health
                var count = Interlocked.Increment(ref _messageCount);
                var now = Environment.TickCount64;
                if (now - _lastStatsTick >= 1000 || count % 100 == 0) {
                    _lastStatsTick = now;
                    var stats = _latencyMonitor.GetStats();
                    var esMetrics = _matchingEngine.GetEventStoreMetrics();

                    if (esMetrics.HasValue) {
                        var es = esMetrics.Value;
                        var bufferPct = (es.BufferUtilization * 100).ToString("F1");
                        _log.Information("STATS: {Stats} | EventStore: {Written}W/{Flushed}F/{Dropped}D (Buffer: {BufferPct}%)",
                            stats.ToString(), es.EventsWritten, es.EventsFlushed, es.EventsDropped, bufferPct);

                        if (!es.IsHealthy) {
                            _log.Warning("EventStore unhealthy: Dropped={Dropped}, Buffer={BufferPct}%",
                                es.EventsDropped, bufferPct);
                        }
                    } else {
                        _log.Information("STATS: {Stats}", stats.ToString());
                    }
                }
            } catch (Exception ex) {
                _log.Error(ex, "Erro no processamento de métricas");
            }
        }

        private static long TicksToNs(long ticks) {
            return (long)(ticks * (1_000_000_000.0 / Stopwatch.Frequency));
        }
       
        /// <summary>
        /// Warm-up JIT: executa ordens dummy para forçar compilação
        /// Elimina latência de 30ms+ na primeira ordem real
        /// </summary>
        private void WarmupJit() {
            _log.Information("Iniciando JIT warm-up...");
            var sw = System.Diagnostics.Stopwatch.StartNew();

            // Usa símbolo que existe no cache
            var symbols = _instrumentCache.GetInstrument("PETR4") != null
                ? new[] { "PETR4", "VALE3", "ITUB4" }
                : new[] { "TEST1", "TEST2", "TEST3" };

            // Warm-up ExecutionReportBuilder (cria e descarta mensagens)
            WarmupExecutionReports(symbols[0]);

            // Warm-up MetricsCollector
            WarmupMetrics();

            // Executa várias ordens para aquecer matching + validation
            for (int i = 0; i < 200; i++) {
                var symbol = symbols[i % symbols.Length];
                var side = i % 2 == 0 ? Side.Buy : Side.Sell;
                var price = 10.00m + (i % 10) * 0.01m;
                var qty = 100m;

                // Valida ordem (aquece validador)
                var request = new OrderRequest(
                    clOrdId: $"WARMUP{i}",
                    symbol: symbol,
                    side: side,
                    price: price,
                    quantity: qty,
                    orderType: OrderType.Limit
                );
                _orderValidator.Validate(request);

                // Processa ordem (aquece matching engine)
                try {
                    var (order, fills) = _matchingEngine.ProcessNewOrder($"WARMUP{i}", symbol, side, price, qty);

                    // Aquece ExecutionReportBuilder com ordem real
                    if (i < 10) {
                        var report = ExecutionReportBuilderV2.BuildNewOrderReportPooled(order, $"WARMUP{i}");
                        ExecutionReportBuilderV2.ReturnToPool(report);

                        foreach (var fill in fills) {
                            var fillReport = ExecutionReportBuilderV2.BuildFillReportPooled(order, $"WARMUP{i}", fill);
                            ExecutionReportBuilderV2.ReturnToPool(fillReport);
                        }
                    }

                    // Cancela para limpar o book
                    if (order.LeavesQty > 0)
                        _matchingEngine.ProcessCancel(symbol, order.OrderId);
                } catch {
                    // Ignora erros durante warm-up
                }
            }

            // Força GC para começar limpo
            GC.Collect(2, GCCollectionMode.Forced, true, true);
            GC.WaitForPendingFinalizers();
            GC.Collect(2, GCCollectionMode.Forced, true, true);

            sw.Stop();
            _log.Information("JIT warm-up concluído em {ElapsedMs}ms", sw.ElapsedMilliseconds);
        }

        private void WarmupExecutionReports(string symbol) {
            // Cria ordem dummy para gerar reports
            var dummyOrder = new Order(0, "WARMUP", symbol, Side.Buy, 10.0m, 100m, DateTime.UtcNow.Ticks);
            var dummyFill = new Fill {
                BuyOrderId = 0,
                SellOrderId = 1,
                Price = 10.0m,
                Quantity = 100m,
                TimestampTicks = DateTime.UtcNow.Ticks
            };

            // Aquece todos os métodos do builder
            for (int i = 0; i < 50; i++) {
                var newReport = ExecutionReportBuilderV2.BuildNewOrderReportPooled(dummyOrder, "WARMUP");
                ExecutionReportBuilderV2.ReturnToPool(newReport);

                var fillReport = ExecutionReportBuilderV2.BuildFillReportPooled(dummyOrder, "WARMUP", dummyFill);
                ExecutionReportBuilderV2.ReturnToPool(fillReport);

                var cancelReport = ExecutionReportBuilderV2.BuildCancelReportPooled(symbol, 0, "WARMUP", "ORIG");
                ExecutionReportBuilderV2.ReturnToPool(cancelReport);

                var rejectReport = ExecutionReportBuilderV2.BuildRejectReportPooled("WARMUP", symbol, "Test");
                ExecutionReportBuilderV2.ReturnToPool(rejectReport);
            }
        }

        private void WarmupMetrics() {
            // Aquece o coletor de métricas e latency monitor
            for (int i = 0; i < 50; i++) {
                _metricsCollector.StartOrder("D");
                _metricsCollector.CaptureOrderData("WARMUP", "TEST", "Buy", 100, 10.0m, 0);
                _metricsCollector.RecordTiming(1000, 2000, 500, 100, 1);
                // Não publica para evitar logs
            }

            // Aquece latency monitor
            for (int i = 0; i < 100; i++) {
                _latencyMonitor.RecordLatency(100_000 + i * 1000); // 100-200µs
            }

            // Força JIT dos métodos críticos via RuntimeHelpers
            PrepareHotPathMethods();
        }

        private void PrepareHotPathMethods() {
            // Força compilação JIT de métodos que só são chamados no primeiro request
            var typesToPrepare = new[]
            {
                typeof(FastOrderParser),
                typeof(ExecutionReportBuilderV2),
                typeof(MatchingEngine),
                typeof(FastOrderBook),
                typeof(FastPriceLevel),
                typeof(OrderValidator),
                typeof(InstrumentValidationCache),
                typeof(OmsApplication)
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
                    } catch {
                        // Ignora métodos que não podem ser preparados
                    }
                }
            }
        }
    }
}
