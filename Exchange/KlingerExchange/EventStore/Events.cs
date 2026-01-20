using ZeroFormatter;

namespace KlingerExchange.EventStore;

/// <summary>
/// Tipo de evento persistido no EventStore
/// </summary>
public enum EventType : byte
{
    OrderAccepted = 1,
    OrderFilled = 2,
    OrderPartiallyFilled = 3,
    OrderCancelled = 4,
    Trade = 5
}

/// <summary>
/// Evento base - Header comum para todos os eventos
/// Struct para performance máxima (stack allocated)
/// </summary>
[ZeroFormattable]
public struct EventHeader
{
    [Index(0)]
    public long SequenceNumber { get; set; }
    
    [Index(1)]
    public EventType EventType { get; set; }
    
    [Index(2)]
    public long TimestampTicks { get; set; }
    
    [Index(3)]
    public int PayloadSize { get; set; }

    public EventHeader(long sequenceNumber, EventType eventType, long timestampTicks, int payloadSize)
    {
        SequenceNumber = sequenceNumber;
        EventType = eventType;
        TimestampTicks = timestampTicks;
        PayloadSize = payloadSize;
    }
}

/// <summary>
/// Evento: Nova ordem aceita no livro
/// </summary>
[ZeroFormattable]
public struct OrderAcceptedEvent
{
    [Index(0)]
    public long OrderId { get; set; }
    
    [Index(1)]
    public string ClOrdId { get; set; }
    
    [Index(2)]
    public string Symbol { get; set; }
    
    [Index(3)]
    public byte Side { get; set; } // 1=Buy, 2=Sell
    
    [Index(4)]
    public decimal Price { get; set; }
    
    [Index(5)]
    public decimal Quantity { get; set; }
    
    [Index(6)]
    public long TimestampTicks { get; set; }

    public OrderAcceptedEvent(long orderId, string clOrdId, string symbol, byte side, decimal price, decimal quantity, long timestampTicks)
    {
        OrderId = orderId;
        ClOrdId = clOrdId;
        Symbol = symbol;
        Side = side;
        Price = price;
        Quantity = quantity;
        TimestampTicks = timestampTicks;
    }
}

/// <summary>
/// Evento: Ordem totalmente executada
/// </summary>
[ZeroFormattable]
public struct OrderFilledEvent
{
    [Index(0)]
    public long OrderId { get; set; }
    
    [Index(1)]
    public string ClOrdId { get; set; }
    
    [Index(2)]
    public decimal FilledQty { get; set; }
    
    [Index(3)]
    public long TimestampTicks { get; set; }

    public OrderFilledEvent(long orderId, string clOrdId, decimal filledQty, long timestampTicks)
    {
        OrderId = orderId;
        ClOrdId = clOrdId;
        FilledQty = filledQty;
        TimestampTicks = timestampTicks;
    }
}

/// <summary>
/// Evento: Ordem parcialmente executada
/// </summary>
[ZeroFormattable]
public struct OrderPartiallyFilledEvent
{
    [Index(0)]
    public long OrderId { get; set; }
    
    [Index(1)]
    public string ClOrdId { get; set; }
    
    [Index(2)]
    public decimal FillQty { get; set; }
    
    [Index(3)]
    public decimal LeavesQty { get; set; }
    
    [Index(4)]
    public long TimestampTicks { get; set; }

    public OrderPartiallyFilledEvent(long orderId, string clOrdId, decimal fillQty, decimal leavesQty, long timestampTicks)
    {
        OrderId = orderId;
        ClOrdId = clOrdId;
        FillQty = fillQty;
        LeavesQty = leavesQty;
        TimestampTicks = timestampTicks;
    }
}

/// <summary>
/// Evento: Ordem cancelada
/// </summary>
[ZeroFormattable]
public struct OrderCancelledEvent
{
    [Index(0)]
    public long OrderId { get; set; }
    
    [Index(1)]
    public string ClOrdId { get; set; }
    
    [Index(2)]
    public string Symbol { get; set; }
    
    [Index(3)]
    public long TimestampTicks { get; set; }

    public OrderCancelledEvent(long orderId, string clOrdId, string symbol, long timestampTicks)
    {
        OrderId = orderId;
        ClOrdId = clOrdId;
        Symbol = symbol;
        TimestampTicks = timestampTicks;
    }
}

/// <summary>
/// Evento: Trade executado (match entre duas ordens)
/// </summary>
[ZeroFormattable]
public struct TradeEvent
{
    [Index(0)]
    public long BuyOrderId { get; set; }
    
    [Index(1)]
    public long SellOrderId { get; set; }
    
    [Index(2)]
    public string Symbol { get; set; }
    
    [Index(3)]
    public decimal Price { get; set; }
    
    [Index(4)]
    public decimal Quantity { get; set; }
    
    [Index(5)]
    public long TimestampTicks { get; set; }

    public TradeEvent(long buyOrderId, long sellOrderId, string symbol, decimal price, decimal quantity, long timestampTicks)
    {
        BuyOrderId = buyOrderId;
        SellOrderId = sellOrderId;
        Symbol = symbol;
        Price = price;
        Quantity = quantity;
        TimestampTicks = timestampTicks;
    }
}
