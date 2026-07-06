using KlingerExchange.EventStore.StructModels;
using KlingerExchange.Matching.Domain.Enums;
using KlingerExchange.Matching.Domain.Struct;
using KlingerExchange.Matching.Engine.Repository;
using Serilog;

namespace KlingerExchange.EventStore;

public sealed class OrderBookRebuilder {
    private readonly ILogger _log = Log.ForContext<OrderBookRebuilder>();
    private readonly IOrderBookRepository _repository;
    private readonly Dictionary<long, string> _orderSymbolMap = new();
    private long _eventsProcessed;
    private long _ordersRebuilt;
    private long _tradesApplied;

    public OrderBookRebuilder(IOrderBookRepository repository) {
        _repository = repository;
    }

    public void Replay(IEnumerable<(EventHeader Header, object Event)> events) {
        _log.Information("Starting event replay...");
        var sw = System.Diagnostics.Stopwatch.StartNew();

        foreach (var (header, eventData) in events) {
            try {
                ApplyEvent(header.EventType, eventData);
                _eventsProcessed++;

                if (_eventsProcessed % 10000 == 0) {
                    _log.Information("Processed {Count} events...", _eventsProcessed);
                }
            } catch (Exception ex) {
                _log.Error(ex, "Error applying event {SeqNum} of type {Type}",
                    header.SequenceNumber, header.EventType);
                throw;
            }
        }

        sw.Stop();
        _log.Information("Replay completed: {Events} events in {Elapsed}ms ({Rate} events/sec)",
            _eventsProcessed, sw.ElapsedMilliseconds, _eventsProcessed * 1000.0 / sw.ElapsedMilliseconds);
    }

    private void ApplyEvent(EventType eventType, object eventData) {
        switch (eventType) {
            case EventType.OrderAccepted:
                ApplyOrderAccepted((OrderAcceptedEvent)eventData);
                break;

            case EventType.OrderFilled:
                ApplyOrderFilled((OrderFilledEvent)eventData);
                break;

            case EventType.OrderPartiallyFilled:
                ApplyOrderPartiallyFilled((OrderPartiallyFilledEvent)eventData);
                break;

            case EventType.OrderCancelled:
                ApplyOrderCancelled((OrderCancelledEvent)eventData);
                break;

            case EventType.Trade:
                ApplyTrade((TradeEvent)eventData);
                break;

            default:
                _log.Warning("EventType desconhecido: {Type}", eventType);
                break;
        }
    }

    private void ApplyOrderAccepted(OrderAcceptedEvent evt) {
        var book = _repository.GetOrCreateBook(evt.Symbol);
        var side = evt.Side == 1 ? Side.Buy : Side.Sell;

        var order = new Order(
            evt.OrderId,
            evt.ClOrdId,
            evt.Symbol,
            side,
            evt.Price,
            evt.Quantity
        );

        book.AddOrder(order);
        _orderSymbolMap[evt.OrderId] = evt.Symbol;
        _ordersRebuilt++;
    }

    private void ApplyTrade(TradeEvent evt) {
        var book = _repository.GetOrCreateBook(evt.Symbol);

        // Apply fill to both participating orders in the book
        book.ApplyFillToOrder(evt.BuyOrderId, evt.Quantity);
        book.ApplyFillToOrder(evt.SellOrderId, evt.Quantity);

        _tradesApplied++;
    }

    private void ApplyOrderFilled(OrderFilledEvent evt) {
        // Safety net: remove fully filled order from book if still present
        // Trade events should have already reduced it, but ensure consistency
        if (_orderSymbolMap.TryGetValue(evt.OrderId, out var symbol)) {
            if (_repository.TryGetBook(symbol, out var book) && book != null) {
                book.RemoveOrder(evt.OrderId);
            }
            _orderSymbolMap.Remove(evt.OrderId);
        }
    }

    private void ApplyOrderPartiallyFilled(OrderPartiallyFilledEvent evt) {
        // Trade events handle the actual fill reduction in the book.
        // This event is informational for the aggressor order.
    }

    private void ApplyOrderCancelled(OrderCancelledEvent evt) {
        if (_repository.TryGetBook(evt.Symbol, out var book) && book != null) {
            book.RemoveOrder(evt.OrderId);
        }
        _orderSymbolMap.Remove(evt.OrderId);
    }

    public long EventsProcessed => _eventsProcessed;
    public long OrdersRebuilt => _ordersRebuilt;
    public long TradesApplied => _tradesApplied;
}
