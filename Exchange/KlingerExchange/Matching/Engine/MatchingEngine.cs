using KlingerExchange.EventStore;
using KlingerExchange.EventStore.StructModels;
using KlingerExchange.MarketData.Core;
using KlingerExchange.Matching.Domain;
using KlingerExchange.Matching.Domain.Enums;
using KlingerExchange.Matching.Domain.Struct;
using KlingerExchange.Matching.Engine.Repository;
using KlingerExchange.Matching.Engine.Latency;
using Serilog;
using System.Collections.Concurrent;

namespace KlingerExchange.Matching.Engine;

public sealed class MatchingEngine {
    private static readonly ILogger Log = Serilog.Log.ForContext<MatchingEngine>();

    private readonly IOrderBookRepository _repository;
    private long _nextOrderId;
    private readonly ConcurrentDictionary<long, (Order Order, string Symbol)> _orderCache;
    private RingBuffer? _marketDataBuffer;
    private SymbolMapper? _symbolMapper;
    private uint _sequenceNumber = 0;
    private long _droppedMessages = 0;
    private LatencyMonitor? _latencyMonitor;

    // EventStore para persistência durável
    private EventStoreWriter? _eventStore;
    private bool _eventStoreEnabled;
    private readonly ConcurrentQueue<PendingEvent> _eventQueue;
    private readonly ManualResetEventSlim _eventSignal;
    private Thread? _eventDispatchThread;
    private volatile bool _eventDispatchRunning;

    private readonly struct PendingEvent {
        public EventType EventType { get; init; }
        public long OrderId { get; init; }
        public long CounterOrderId { get; init; }
        public string ClOrdId { get; init; }
        public string Symbol { get; init; }
        public byte Side { get; init; }
        public decimal Price { get; init; }
        public decimal Quantity { get; init; }
        public decimal LeavesQty { get; init; }
        public long TimestampTicks { get; init; }
    }

    public MatchingEngine(IOrderBookRepository repository) {
        _repository = repository;
        _nextOrderId = 1;
        _orderCache = new ConcurrentDictionary<long, (Order, string)>();
        _eventStoreEnabled = false;
        _eventQueue = new ConcurrentQueue<PendingEvent>();
        _eventSignal = new ManualResetEventSlim(false);
    }

    public void SetMarketDataPublisher(RingBuffer buffer, SymbolMapper symbolMapper) {
        _marketDataBuffer = buffer;
        _symbolMapper = symbolMapper;
    }

    public void SetLatencyMonitor(LatencyMonitor latencyMonitor) {
        _latencyMonitor = latencyMonitor;
    }

    public void EnableEventStore(string baseDirectory) {
        _eventStore = new EventStoreWriter(baseDirectory);
        _eventStoreEnabled = true;

        StartEventDispatcher();

        TryRecoverFromEventStore(baseDirectory);
    }

    private void StartEventDispatcher() {
        if (_eventDispatchThread != null)
            return;

        _eventDispatchRunning = true;
        _eventDispatchThread = new Thread(EventDispatchLoop) {
            Name = "EventStore-Dispatch",
            IsBackground = true,
            Priority = ThreadPriority.AboveNormal
        };
        _eventDispatchThread.Start();
    }

    private void EventDispatchLoop() {
        while (_eventDispatchRunning) {
            _eventSignal.Wait(1);
            _eventSignal.Reset();

            while (_eventQueue.TryDequeue(out var evt)) {
                DispatchEvent(evt);
            }
        }

        // Drain remaining events on shutdown
        while (_eventQueue.TryDequeue(out var evt)) {
            DispatchEvent(evt);
        }
    }

    private void DispatchEvent(in PendingEvent evt) {
        if (!_eventStoreEnabled || _eventStore == null)
            return;

        switch (evt.EventType) {
            case EventType.OrderAccepted: {
                    var orderEvent = new OrderAcceptedEvent(
                        evt.OrderId,
                        evt.ClOrdId,
                        evt.Symbol,
                        evt.Side,
                        evt.Price,
                        evt.Quantity,
                        evt.TimestampTicks
                    );
                    _eventStore.Append(EventType.OrderAccepted, orderEvent);
                    break;
                }
            case EventType.Trade: {
                    var tradeEvent = new TradeEvent(
                        evt.OrderId,
                        evt.CounterOrderId,
                        evt.Symbol,
                        evt.Price,
                        evt.Quantity,
                        evt.TimestampTicks
                    );
                    _eventStore.Append(EventType.Trade, tradeEvent);
                    break;
                }
            case EventType.OrderFilled: {
                    var filledEvent = new OrderFilledEvent(
                        evt.OrderId,
                        evt.ClOrdId,
                        evt.Quantity,
                        evt.TimestampTicks
                    );
                    _eventStore.Append(EventType.OrderFilled, filledEvent);
                    break;
                }
            case EventType.OrderPartiallyFilled: {
                    var partialEvent = new OrderPartiallyFilledEvent(
                        evt.OrderId,
                        evt.ClOrdId,
                        evt.Quantity,
                        evt.LeavesQty,
                        evt.TimestampTicks
                    );
                    _eventStore.Append(EventType.OrderPartiallyFilled, partialEvent);
                    break;
                }
            case EventType.OrderCancelled: {
                    var cancelEvent = new OrderCancelledEvent(
                        evt.OrderId,
                        evt.ClOrdId,
                        evt.Symbol,
                        evt.TimestampTicks
                    );
                    _eventStore.Append(EventType.OrderCancelled, cancelEvent);
                    break;
                }
        }
    }

    private void EnqueueEvent(in PendingEvent evt) {
        if (!_eventStoreEnabled || _eventStore == null)
            return;

        _eventQueue.Enqueue(evt);
        // Don't signal here - batch signal at end of order
    }

    private void SignalEventDispatcher() {
        if (_eventStoreEnabled)
            _eventSignal.Set();
    }

    public EventStoreMetrics? GetEventStoreMetrics() {
        return _eventStore?.GetMetrics();
    }

    private void TryRecoverFromEventStore(string baseDirectory) {
        try {
            var eventFiles = Directory.GetFiles(baseDirectory, "events_*.dat")
                                      .OrderBy(f => f)
                                      .ToList();

            if (eventFiles.Count == 0) {
                Log.Information("No event files found. Starting with clean state");
                return;
            }

            Log.Information("{Count} event files found. Starting recovery...", eventFiles.Count);
            var sw = System.Diagnostics.Stopwatch.StartNew();

            var rebuilder = new OrderBookRebuilder(_repository);
            long totalEvents = 0;
            var progressTimer = System.Diagnostics.Stopwatch.StartNew();

            foreach (var eventFile in eventFiles) {
                Log.Information("Reading file: {File}", Path.GetFileName(eventFile));
                
                var fileInfo = new FileInfo(eventFile);
                Log.Information("File size: {SizeMB:F2} MB", fileInfo.Length / (1024.0 * 1024.0));

                using var reader = new EventStoreReader(eventFile);
                
                // ✅ STREAMING: Process events without loading all into memory
                long fileEvents = 0;
                var batch = new List<(EventHeader, object)>(10000); // Process in 10K batches
                
                foreach (var evt in reader.ReadAll()) {
                    batch.Add(evt);
                    fileEvents++;
                    totalEvents++;
                    
                    // Process batch when full
                    if (batch.Count >= 10000) {
                        rebuilder.Replay(batch);
                        batch.Clear();
                        
                        // Log progress every 5 seconds
                        if (progressTimer.ElapsedMilliseconds > 5000) {
                            var rate = totalEvents * 1000.0 / sw.ElapsedMilliseconds;
                            Log.Information("⏳ Progress: {TotalEvents:N0} events processed ({Rate:F0} events/sec)",
                                totalEvents, rate);
                            progressTimer.Restart();
                        }
                    }
                }
                
                // Process remaining events
                if (batch.Count > 0) {
                    rebuilder.Replay(batch);
                    batch.Clear();
                }

                Log.Information("File {File} processed: {Count:N0} events",
                    Path.GetFileName(eventFile), fileEvents);
            }

            sw.Stop();

            var maxOrderId = FindMaxOrderId();
            if (maxOrderId > 0) {
                _nextOrderId = maxOrderId + 1;
                Log.Information("Next OrderId adjusted to: {OrderId}", _nextOrderId);
            }

            Log.Information("✅ RECOVERY COMPLETE: {TotalEvents:N0} events reprocessed in {Elapsed}ms ({Rate:F0} events/sec)",
                totalEvents, sw.ElapsedMilliseconds, totalEvents * 1000.0 / Math.Max(1, sw.ElapsedMilliseconds));
        } catch (Exception ex) {
            Log.Error(ex, "❌ Error during recovery. Starting with a clean state.");
        }
    }

    private long FindMaxOrderId() {
        long maxOrderId = 0;

        foreach (var book in _repository.GetAllBooks()) {
            foreach (var orderId in book.GetAllOrderIds()) {
                if (orderId > maxOrderId)
                    maxOrderId = orderId;
            }
        }

        return maxOrderId;
    }

    public (Order Order, List<Fill> Fills) ProcessNewOrder(string clOrdId, string symbol, Side side, decimal price, decimal quantity) {
        var nowTicks = DateTime.UtcNow.Ticks;
        var orderId = Interlocked.Increment(ref _nextOrderId);
        var order = new Order(orderId, clOrdId, symbol, side, price, quantity, nowTicks);
        _orderCache[orderId] = (order, symbol);
        var book = _repository.GetOrCreateBook(symbol);
        var fills = new List<Fill>(4);  // Pre-allocate for common case

        // The accepted order event persists (non-blocking)
        EnqueueEvent(new PendingEvent {
            EventType = EventType.OrderAccepted,
            OrderId = orderId,
            ClOrdId = clOrdId,
            Symbol = symbol,
            Side = (byte)(side == Side.Buy ? 1 : 2),
            Price = price,
            Quantity = quantity,
            TimestampTicks = nowTicks
        });

        // Try to match against opposite side (single lock, no list scans)
        var remainingQty = quantity;
        var matches = new List<MatchResult>(4); // Pre-allocate
        book.MatchOrder(side, price, ref remainingQty, matches);

        foreach (var match in matches) {
            var fill = new Fill {
                BuyOrderId = side == Side.Buy ? orderId : match.CounterOrderId,
                SellOrderId = side == Side.Buy ? match.CounterOrderId : orderId,
                Price = match.Price,
                Quantity = match.Quantity,
                TimestampTicks = nowTicks
            };
            fills.Add(fill);

            // Trade event persists (non-blocking)
            EnqueueEvent(new PendingEvent {
                EventType = EventType.Trade,
                OrderId = fill.BuyOrderId,
                CounterOrderId = fill.SellOrderId,
                Symbol = symbol,
                Price = fill.Price,
                Quantity = fill.Quantity,
                TimestampTicks = nowTicks
            });

            // Publish to market data (inline, <50ns overhead)
            PublishTrade(symbol, fill);
        }

        // Update order with fills
        var filledQty = quantity - remainingQty;
        if (filledQty > 0) {
            order = order.WithFill(filledQty);

            // The fill event (complete or partial) persists.
            if (order.IsFilled) {
                EnqueueEvent(new PendingEvent {
                    EventType = EventType.OrderFilled,
                    OrderId = orderId,
                    ClOrdId = clOrdId,
                    Quantity = filledQty,
                    TimestampTicks = nowTicks
                });
            } else {
                EnqueueEvent(new PendingEvent {
                    EventType = EventType.OrderPartiallyFilled,
                    OrderId = orderId,
                    ClOrdId = clOrdId,
                    Quantity = filledQty,
                    LeavesQty = order.LeavesQty,
                    TimestampTicks = nowTicks
                });
            }
        }

        // Add remaining quantity to book if not fully filled
        if (remainingQty > 0) {
            book.AddOrder(order);
        }

        // Signal event dispatcher once at end of order processing
        SignalEventDispatcher();

        return (order, fills);
    }

    public (bool success, Side side) ProcessCancel(string symbol, long orderId) {
        if (!_repository.TryGetBook(symbol, out var book) || book == null)
            return (false, Side.Buy);

        var (success, side) = book.RemoveOrder(orderId);

        // Cancellation event persists (asynchronous)
        if (success) {
            _orderCache.TryRemove(orderId, out _);
            
            EnqueueEvent(new PendingEvent {
                EventType = EventType.OrderCancelled,
                OrderId = orderId,
                ClOrdId = string.Empty,
                Symbol = symbol,
                TimestampTicks = DateTime.UtcNow.Ticks
            });
            SignalEventDispatcher();
        }

        return (success, side);
    }

    public (bool success, Order newOrder) ProcessReplace(long orderId, string symbol, string newClOrdId, decimal? newPrice, decimal? newQuantity) {
        if (!_repository.TryGetBook(symbol, out var book) || book == null)
            return (false, default);

        // Get old order from cache
        if (!_orderCache.TryGetValue(orderId, out var cached))
            return (false, default);

        var oldOrder = cached.Order;

        // Remove from book (preserves time priority if price unchanged)
        var (removed, _) = book.ReplaceOrder(orderId, newPrice, newQuantity);
        if (!removed)
            return (false, default);

        // Create new order with updated values
        var finalPrice = newPrice ?? oldOrder.Price;
        var finalQty = newQuantity ?? oldOrder.Quantity;
        var nowTicks = DateTime.UtcNow.Ticks;

        var newOrder = new Order(orderId, newClOrdId, symbol, oldOrder.Side, finalPrice, finalQty, nowTicks) {
            FilledQty = oldOrder.FilledQty,
            Status = oldOrder.Status
        };

        // Re-add to book with new price/quantity
        book.AddOrder(newOrder);
        _orderCache[orderId] = (newOrder, symbol);

        EnqueueEvent(new PendingEvent {
            EventType = EventType.OrderAccepted,
            OrderId = orderId,
            ClOrdId = newClOrdId,
            Symbol = symbol,
            Side = (byte)(newOrder.Side == Side.Buy ? 1 : 2),
            Price = finalPrice,
            Quantity = finalQty,
            TimestampTicks = nowTicks
        });
        SignalEventDispatcher();

        return (true, newOrder);
    }

    public (bool found, Order order) GetOrderInfo(long orderId) {
        if (_orderCache.TryGetValue(orderId, out var cached)) {
            return (true, cached.Order);
        }
        return (false, default);
    }

    private void PublishTrade(string symbol, Fill fill) {
        if (_marketDataBuffer == null || _symbolMapper == null) {
            if (_droppedMessages % 1000 == 0) {
                Log.Warning("Market data buffer not initialized! Dropped {Count} messages", _droppedMessages);
            }
            Interlocked.Increment(ref _droppedMessages);
            return;
        }

        var message = new MarketData.Core.TradeMessage {
            MessageType = MarketData.Core.TradeMessage.MSG_TYPE_TRADE,
            SymbolIndex = _symbolMapper.GetIndex(symbol),
            SequenceNumber = Interlocked.Increment(ref _sequenceNumber),
            TimestampNs = fill.TimestampTicks * 100, // Ticks to nanoseconds
            PriceFixed = MarketData.Core.TradeMessage.PriceToFixed(fill.Price),
            Quantity = (long)fill.Quantity,
            BuyOrderId = fill.BuyOrderId,
            SellOrderId = fill.SellOrderId
        };

        // Non-blocking write to ring buffer
        if (!_marketDataBuffer.TryWrite(in message)) {
            if (_droppedMessages % 100 == 0) {
                Log.Warning("Ring buffer full! Dropped {Count} messages", _droppedMessages);
            }
            Interlocked.Increment(ref _droppedMessages);
        }
    }

    public void Dispose() {
        _eventDispatchRunning = false;
        _eventSignal.Set();
        _eventDispatchThread?.Join(1000);
        _eventStore?.Dispose();
    }
}
