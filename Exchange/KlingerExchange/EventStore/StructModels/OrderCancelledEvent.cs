using ZeroFormatter;

namespace KlingerExchange.EventStore.StructModels;

[ZeroFormattable]
public struct OrderCancelledEvent {
    [Index(0)]
    public long OrderId { get; set; }

    [Index(1)]
    public string ClOrdId { get; set; }

    [Index(2)]
    public string Symbol { get; set; }

    [Index(3)]
    public long TimestampTicks { get; set; }

    public OrderCancelledEvent(long orderId, string clOrdId, string symbol, long timestampTicks) {
        OrderId = orderId;
        ClOrdId = clOrdId;
        Symbol = symbol;
        TimestampTicks = timestampTicks;
    }
}
