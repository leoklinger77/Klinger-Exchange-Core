using ZeroFormatter;

namespace KlingerExchange.EventStore.StructModels;

[ZeroFormattable]
public struct OrderPartiallyFilledEvent {
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

    public OrderPartiallyFilledEvent(long orderId, string clOrdId, decimal fillQty, decimal leavesQty, long timestampTicks) {
        OrderId = orderId;
        ClOrdId = clOrdId;
        FillQty = fillQty;
        LeavesQty = leavesQty;
        TimestampTicks = timestampTicks;
    }
}
