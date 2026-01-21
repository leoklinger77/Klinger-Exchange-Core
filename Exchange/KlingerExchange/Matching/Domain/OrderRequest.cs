using KlingerExchange.Matching.Domain.Enums;

namespace KlingerExchange.Matching.Domain;

public sealed class OrderRequest {
    public string ClOrdId { get; init; } = string.Empty;
    public string Symbol { get; init; } = string.Empty;
    public Side Side { get; init; }
    public decimal Price { get; init; }
    public decimal Quantity { get; init; }
    public OrderType OrderType { get; init; }
    public long TimestampTicks { get; init; }

    public OrderRequest(string clOrdId, string symbol, Side side, decimal price, decimal quantity, OrderType orderType) {
        ClOrdId = clOrdId;
        Symbol = symbol;
        Side = side;
        Price = price;
        Quantity = quantity;
        OrderType = orderType;
        TimestampTicks = DateTimeOffset.UtcNow.Ticks;
    }
}
