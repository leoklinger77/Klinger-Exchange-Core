using KlingerExchange.Matching.Domain;
using KlingerExchange.Matching.Engine;
using Serilog;

namespace KlingerExchange.EventStore;

/// <summary>
/// Reconstrói o estado do OrderBook a partir dos eventos
/// </summary>
public sealed class OrderBookRebuilder
{
    private readonly ILogger _log = Log.ForContext<OrderBookRebuilder>();
    private readonly IOrderBookRepository _repository;
    private long _eventsProcessed;
    private long _ordersRebuilt;

    public OrderBookRebuilder(IOrderBookRepository repository)
    {
        _repository = repository;
    }

    /// <summary>
    /// Aplica eventos para reconstruir o estado do OrderBook
    /// </summary>
    public void Replay(IEnumerable<(EventHeader Header, object Event)> events)
    {
        _log.Information("Iniciando replay de eventos...");
        var sw = System.Diagnostics.Stopwatch.StartNew();

        foreach (var (header, eventData) in events)
        {
            try
            {
                ApplyEvent(header.EventType, eventData);
                _eventsProcessed++;

                if (_eventsProcessed % 10000 == 0)
                {
                    _log.Information("Processados {Count} eventos...", _eventsProcessed);
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, "Erro aplicando evento {SeqNum} do tipo {Type}", 
                    header.SequenceNumber, header.EventType);
                throw;
            }
        }

        sw.Stop();
        _log.Information("Replay concluído: {Events} eventos em {Elapsed}ms ({Rate} eventos/seg)", 
            _eventsProcessed, sw.ElapsedMilliseconds, _eventsProcessed * 1000.0 / sw.ElapsedMilliseconds);
    }

    private void ApplyEvent(EventType eventType, object eventData)
    {
        switch (eventType)
        {
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
                // Trade é derivado, não altera o estado do book diretamente
                break;

            default:
                _log.Warning("EventType desconhecido: {Type}", eventType);
                break;
        }
    }

    private void ApplyOrderAccepted(OrderAcceptedEvent evt)
    {
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
        _ordersRebuilt++;
    }

    private void ApplyOrderFilled(OrderFilledEvent evt)
    {
        // Ordem foi totalmente preenchida, deve ter sido removida do book
        // Evento informativo apenas
    }

    private void ApplyOrderPartiallyFilled(OrderPartiallyFilledEvent evt)
    {
        // Ordem parcialmente preenchida, UpdateOrderFill já foi aplicado via Trade events
        // Evento informativo apenas
    }

    private void ApplyOrderCancelled(OrderCancelledEvent evt)
    {
        if (_repository.TryGetBook(evt.Symbol, out var book) && book != null)
        {
            book.RemoveOrder(evt.OrderId);
        }
    }

    public long EventsProcessed => _eventsProcessed;
    public long OrdersRebuilt => _ordersRebuilt;
}
