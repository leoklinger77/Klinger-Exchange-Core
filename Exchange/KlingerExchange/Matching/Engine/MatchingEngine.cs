using KlingerExchange.EventStore;
using KlingerExchange.Matching.Domain;
using Serilog;
using System.Collections.Concurrent;

namespace KlingerExchange.Matching.Engine;

public sealed class Fill
{
    public long BuyOrderId { get; init; }
    public long SellOrderId { get; init; }
    public decimal Price { get; init; }
    public decimal Quantity { get; init; }
    public long TimestampTicks { get; init; }
}

public sealed class MatchingEngine
{
    private static readonly ILogger Log = Serilog.Log.ForContext<MatchingEngine>();
    
    private readonly IOrderBookRepository _repository;
    private long _nextOrderId;
    private MarketData.Core.RingBuffer? _marketDataBuffer;
    private MarketData.Core.SymbolMapper? _symbolMapper;
    private uint _sequenceNumber = 0;
    private long _droppedMessages = 0;
    
    // EventStore para persistência durável
    private EventStoreWriter? _eventStore;
    private bool _eventStoreEnabled;
    private readonly ConcurrentQueue<PendingEvent> _eventQueue;
    private readonly ManualResetEventSlim _eventSignal;
    private Thread? _eventDispatchThread;
    private volatile bool _eventDispatchRunning;

    private readonly struct PendingEvent
    {
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

    public MatchingEngine(IOrderBookRepository repository)
    {
        _repository = repository;
        _nextOrderId = 1;
        _eventStoreEnabled = false;
        _eventQueue = new ConcurrentQueue<PendingEvent>();
        _eventSignal = new ManualResetEventSlim(false);
    }

    /// <summary>Inject market data dependencies for publishing trades</summary>
    public void SetMarketDataPublisher(MarketData.Core.RingBuffer buffer, MarketData.Core.SymbolMapper symbolMapper)
    {
        _marketDataBuffer = buffer;
        _symbolMapper = symbolMapper;
    }

    /// <summary>Enable event sourcing with durable persistence</summary>
    public void EnableEventStore(string baseDirectory)
    {
        _eventStore = new EventStoreWriter(baseDirectory);
        _eventStoreEnabled = true;
        Log.Information("EventStore habilitado em {Directory}", baseDirectory);

        StartEventDispatcher();
        
        // CRASH RECOVERY: Tenta carregar eventos anteriores
        TryRecoverFromEventStore(baseDirectory);
    }

    private void StartEventDispatcher()
    {
        if (_eventDispatchThread != null)
            return;

        _eventDispatchRunning = true;
        _eventDispatchThread = new Thread(EventDispatchLoop)
        {
            Name = "EventStore-Dispatch",
            IsBackground = true,
            Priority = ThreadPriority.AboveNormal
        };
        _eventDispatchThread.Start();
    }

    private void EventDispatchLoop()
    {
        while (_eventDispatchRunning)
        {
            _eventSignal.Wait(1);
            _eventSignal.Reset();

            while (_eventQueue.TryDequeue(out var evt))
            {
                DispatchEvent(evt);
            }
        }

        // Drain remaining events on shutdown
        while (_eventQueue.TryDequeue(out var evt))
        {
            DispatchEvent(evt);
        }
    }

    private void DispatchEvent(in PendingEvent evt)
    {
        if (!_eventStoreEnabled || _eventStore == null)
            return;

        switch (evt.EventType)
        {
            case EventType.OrderAccepted:
            {
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
            case EventType.Trade:
            {
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
            case EventType.OrderFilled:
            {
                var filledEvent = new OrderFilledEvent(
                    evt.OrderId,
                    evt.ClOrdId,
                    evt.Quantity,
                    evt.TimestampTicks
                );
                _eventStore.Append(EventType.OrderFilled, filledEvent);
                break;
            }
            case EventType.OrderPartiallyFilled:
            {
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
            case EventType.OrderCancelled:
            {
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

    private void EnqueueEvent(in PendingEvent evt)
    {
        if (!_eventStoreEnabled || _eventStore == null)
            return;

        _eventQueue.Enqueue(evt);
        // Don't signal here - batch signal at end of order
    }

    private void SignalEventDispatcher()
    {
        if (_eventStoreEnabled)
            _eventSignal.Set();
    }
    
    /// <summary>Retorna métricas do EventStore</summary>
    public EventStoreMetrics? GetEventStoreMetrics()
    {
        return _eventStore?.GetMetrics();
    }
    
    /// <summary>
    /// Reconstrói o estado do OrderBook a partir de eventos persistidos
    /// Chamado na inicialização para crash recovery
    /// </summary>
    private void TryRecoverFromEventStore(string baseDirectory)
    {
        try
        {
            var eventFiles = Directory.GetFiles(baseDirectory, "events_*.dat")
                                      .OrderBy(f => f)
                                      .ToList();
            
            if (eventFiles.Count == 0)
            {
                Log.Information("Nenhum arquivo de eventos encontrado. Iniciando com estado limpo.");
                return;
            }
            
            Log.Information("Encontrados {Count} arquivos de eventos. Iniciando recovery...", eventFiles.Count);
            var sw = System.Diagnostics.Stopwatch.StartNew();
            
            var rebuilder = new OrderBookRebuilder(_repository);
            long totalEvents = 0;
            
            // Lê e aplica eventos de todos os arquivos em ordem
            foreach (var eventFile in eventFiles)
            {
                Log.Information("Lendo arquivo: {File}", Path.GetFileName(eventFile));
                
                using var reader = new EventStoreReader(eventFile);
                var events = reader.ReadAll().ToList();
                
                Log.Information("Arquivo {File} contém {Count} eventos", 
                    Path.GetFileName(eventFile), events.Count);
                
                rebuilder.Replay(events);
                totalEvents += events.Count;
            }
            
            sw.Stop();
            
            // Ajusta _nextOrderId para próximo valor disponível
            var maxOrderId = FindMaxOrderId();
            if (maxOrderId > 0)
            {
                _nextOrderId = maxOrderId + 1;
                Log.Information("Próximo OrderId ajustado para: {OrderId}", _nextOrderId);
            }
            
            Log.Information("RECOVERY COMPLETO: {TotalEvents} eventos reprocessados em {Elapsed}ms ({Rate:F0} eventos/seg)",
                totalEvents, sw.ElapsedMilliseconds, totalEvents * 1000.0 / Math.Max(1, sw.ElapsedMilliseconds));
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Erro durante recovery. Iniciando com estado limpo.");
            // Não falha a aplicação - apenas inicia com estado limpo
        }
    }
    
    /// <summary>
    /// Encontra o maior OrderId nos order books para ajustar a sequência
    /// </summary>
    private long FindMaxOrderId()
    {
        long maxOrderId = 0;
        
        foreach (var book in _repository.GetAllBooks())
        {
            foreach (var orderId in book.GetAllOrderIds())
            {
                if (orderId > maxOrderId)
                    maxOrderId = orderId;
            }
        }
        
        return maxOrderId;
    }

    public (Order Order, List<Fill> Fills) ProcessNewOrder(string clOrdId, string symbol, Side side, decimal price, decimal quantity)
    {
        var nowTicks = DateTime.UtcNow.Ticks; // Single timestamp for entire operation
        var orderId = Interlocked.Increment(ref _nextOrderId);
        var order = new Order(orderId, clOrdId, symbol, side, price, quantity, nowTicks);
        var book = _repository.GetOrCreateBook(symbol);
        var fills = new List<Fill>(4); // Pre-allocate for common case

        // Persiste evento de ordem aceita (non-blocking)
        EnqueueEvent(new PendingEvent
        {
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

        foreach (var match in matches)
        {
            var fill = new Fill
            {
                BuyOrderId = side == Side.Buy ? orderId : match.CounterOrderId,
                SellOrderId = side == Side.Buy ? match.CounterOrderId : orderId,
                Price = match.Price,
                Quantity = match.Quantity,
                TimestampTicks = nowTicks
            };
            fills.Add(fill);

            // Persiste evento de trade (non-blocking)
            EnqueueEvent(new PendingEvent
            {
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
        if (filledQty > 0)
        {
            order = order.WithFill(filledQty);
            
            // Persiste evento de fill (completo ou parcial)
            if (order.IsFilled)
            {
                EnqueueEvent(new PendingEvent
                {
                    EventType = EventType.OrderFilled,
                    OrderId = orderId,
                    ClOrdId = clOrdId,
                    Quantity = filledQty,
                    TimestampTicks = nowTicks
                });
            }
            else
            {
                EnqueueEvent(new PendingEvent
                {
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
        if (remainingQty > 0)
        {
            book.AddOrder(order);
        }

        // Signal event dispatcher once at end of order processing
        SignalEventDispatcher();

        return (order, fills);
    }

    public bool ProcessCancel(string symbol, long orderId)
    {
        if (!_repository.TryGetBook(symbol, out var book) || book == null)
            return false;

        var result = book.RemoveOrder(orderId);
        
        // Persiste evento de cancelamento (assíncrono)
        if (result)
        {
            EnqueueEvent(new PendingEvent
            {
                EventType = EventType.OrderCancelled,
                OrderId = orderId,
                ClOrdId = string.Empty, // Não temos ClOrdId aqui, seria necessário manter um índice
                Symbol = symbol,
                TimestampTicks = DateTime.UtcNow.Ticks
            });
            SignalEventDispatcher();
        }

        return result;
    }

    public (decimal BidPrice, decimal BidQty, decimal AskPrice, decimal AskQty) GetTopOfBook(string symbol)
    {
        if (!_repository.TryGetBook(symbol, out var book) || book == null)
            return (0, 0, 0, 0);

        return book.GetTopOfBook();
    }

    /// <summary>Publish trade to market data stream (inline, target <50ns)</summary>
    private void PublishTrade(string symbol, Fill fill)
    {
        if (_marketDataBuffer == null || _symbolMapper == null)
            return;

        var message = new MarketData.Core.TradeMessage
        {
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
        if (!_marketDataBuffer.TryWrite(in message))
        {
            Interlocked.Increment(ref _droppedMessages);
        }
    }
    
    /// <summary>Get count of dropped messages (buffer overflow)</summary>
    public long GetDroppedMessages() => _droppedMessages;
    
    /// <summary>Shutdown event store gracefully</summary>
    public void Dispose()
    {
        _eventDispatchRunning = false;
        _eventSignal.Set();
        _eventDispatchThread?.Join(1000);
        _eventStore?.Dispose();
    }
}
