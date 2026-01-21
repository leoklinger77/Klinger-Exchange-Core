using ZeroFormatter;

namespace KlingerExchange.EventStore.StructModels;

[ZeroFormattable]
public struct OrderFilledEvent {
    [Index(0)]
    public long OrderId { get; set; }

    [Index(1)]
    public string ClOrdId { get; set; }

    [Index(2)]
    public decimal FilledQty { get; set; }

    [Index(3)]
    public long TimestampTicks { get; set; }

    public OrderFilledEvent(long orderId, string clOrdId, decimal filledQty, long timestampTicks) {
        OrderId = orderId;
        ClOrdId = clOrdId;
        FilledQty = filledQty;
        TimestampTicks = timestampTicks;
    }
}
