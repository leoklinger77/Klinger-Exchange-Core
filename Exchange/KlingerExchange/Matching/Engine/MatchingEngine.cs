using KlingerExchange.EventStore;
using KlingerExchange.EventStore.StructModels;
using KlingerExchange.MarketData.Core;
using KlingerExchange.Matching.Domain;
using KlingerExchange.Matching.Domain.Enums;
using KlingerExchange.Matching.Domain.Struct;
using KlingerExchange.Matching.Engine.Repository;
using Serilog;
using System.Collections.Concurrent;

namespace KlingerExchange.Matching.Engine;

public sealed class MatchingEngine {
    private static readonly ILogger Log = Serilog.Log.ForContext<MatchingEngine>();

    private readonly IOrderBookRepository _repository;
    private long _nextOrderId;
    private RingBuffer? _marketDataBuffer;
    private SymbolMapper? _symbolMapper;
    private uint _sequenceNumber = 0;
    private long _droppedMessages = 0;

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
        _eventStoreEnabled = false;
        _eventQueue = new ConcurrentQueue<PendingEvent>();
        _eventSignal = new ManualResetEventSlim(false);
    }
        
    public void SetMarketDataPublisher(RingBuffer buffer, SymbolMapper symbolMapper) {
        _marketDataBuffer = buffer;
        _symbolMapper = symbolMapper;
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
                Log.Information("No event files found. Starting with a clean state");
                return;
            }

            Log.Information("{Count} event files found. Starting recovery...", eventFiles.Count);
            var sw = System.Diagnostics.Stopwatch.StartNew();

            var rebuilder = new OrderBookRebuilder(_repository);
            long totalEvents = 0;
                        
            foreach (var eventFile in eventFiles) {
                Log.Information("Reading file: {File}", Path.GetFileName(eventFile));

                using var reader = new EventStoreReader(eventFile);
                var events = reader.ReadAll().ToList();

                Log.Information("File {File} contains {Count} events",
                    Path.GetFileName(eventFile), events.Count);

                rebuilder.Replay(events);
                totalEvents += events.Count;
            }

            sw.Stop();

            var maxOrderId = FindMaxOrderId();
            if (maxOrderId > 0) {
                _nextOrderId = maxOrderId + 1;
                Log.Information("Next OrderId adjusted to: {OrderId}", _nextOrderId);
            }

            Log.Information("RECOVERY COMPLETE: {TotalEvents} events reprocessed in {Elapsed}ms ({Rate:F0} events/sec)",
                totalEvents, sw.ElapsedMilliseconds, totalEvents * 1000.0 / Math.Max(1, sw.ElapsedMilliseconds));
        } catch (Exception ex) {
            Log.Error(ex, "Error during recovery. Starting with a clean state.");
            // Since it's for study purposes, the application doesn't fail - it just starts with a clean state.
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

    public bool ProcessCancel(string symbol, long orderId) {
        if (!_repository.TryGetBook(symbol, out var book) || book == null)
            return false;

        var result = book.RemoveOrder(orderId);

        // Cancellation event persists (asynchronous)
        if (result) {
            EnqueueEvent(new PendingEvent {
                EventType = EventType.OrderCancelled,
                OrderId = orderId,
                ClOrdId = string.Empty, //TODO: We don't have a ClOrdId here; it would be necessary to maintain an index.
                Symbol = symbol,
                TimestampTicks = DateTime.UtcNow.Ticks
            });
            SignalEventDispatcher();
        }

        return result;
    }    
        
    private void PublishTrade(string symbol, Fill fill) {
        if (_marketDataBuffer == null || _symbolMapper == null)
            return;

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
