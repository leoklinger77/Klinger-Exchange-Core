namespace KlingerExchange.Matching.Domain;

public sealed class Fill {
    public long BuyOrderId { get; init; }
    public long SellOrderId { get; init; }
    public decimal Price { get; init; }
    public decimal Quantity { get; init; }
    public long TimestampTicks { get; init; }
}
