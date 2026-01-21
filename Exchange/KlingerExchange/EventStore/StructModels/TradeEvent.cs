using ZeroFormatter;

namespace KlingerExchange.EventStore.StructModels;

[ZeroFormattable]
public struct TradeEvent {
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

    public TradeEvent(long buyOrderId, long sellOrderId, string symbol, decimal price, decimal quantity, long timestampTicks) {
        BuyOrderId = buyOrderId;
        SellOrderId = sellOrderId;
        Symbol = symbol;
        Price = price;
        Quantity = quantity;
        TimestampTicks = timestampTicks;
    }
}
