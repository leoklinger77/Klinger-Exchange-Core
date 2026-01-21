using ZeroFormatter;

namespace KlingerExchange.EventStore.StructModels;

[ZeroFormattable]
public struct OrderAcceptedEvent {
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

    public OrderAcceptedEvent(long orderId, string clOrdId, string symbol, byte side, decimal price, decimal quantity, long timestampTicks) {
        OrderId = orderId;
        ClOrdId = clOrdId;
        Symbol = symbol;
        Side = side;
        Price = price;
        Quantity = quantity;
        TimestampTicks = timestampTicks;
    }
}
