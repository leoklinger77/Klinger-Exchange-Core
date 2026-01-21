using KlingerExchange.Matching.Domain.Enums;

namespace KlingerExchange.Matching.Domain.Struct;

public readonly struct Order {
    public long OrderId { get; init; }
    public string ClOrdId { get; init; }
    public string Symbol { get; init; }
    public Side Side { get; init; }
    public decimal Price { get; init; }
    public decimal Quantity { get; init; }
    public decimal FilledQty { get; init; }
    public OrderStatus Status { get; init; }
    public long TimestampTicks { get; init; }

    public Order(long orderId, string clOrdId, string symbol, Side side, decimal price, decimal quantity, long timestampTicks = 0) {
        OrderId = orderId;
        ClOrdId = clOrdId;
        Symbol = symbol;
        Side = side;
        Price = price;
        Quantity = quantity;
        FilledQty = 0;
        Status = OrderStatus.New;
        TimestampTicks = timestampTicks;
    }

    public decimal LeavesQty => Quantity - FilledQty;

    public bool IsFilled => FilledQty >= Quantity;

    public Order WithFill(decimal fillQty) {
        var newFilledQty = FilledQty + fillQty;
        var newStatus = newFilledQty >= Quantity ? OrderStatus.Filled : OrderStatus.PartiallyFilled;

        return new Order {
            OrderId = OrderId,
            ClOrdId = ClOrdId,
            Symbol = Symbol,
            Side = Side,
            Price = Price,
            Quantity = Quantity,
            FilledQty = newFilledQty,
            Status = newStatus,
            TimestampTicks = TimestampTicks
        };
    }

    public Order WithStatus(OrderStatus status) {
        return new Order {
            OrderId = OrderId,
            ClOrdId = ClOrdId,
            Symbol = Symbol,
            Side = Side,
            Price = Price,
            Quantity = Quantity,
            FilledQty = FilledQty,
            Status = status,
            TimestampTicks = TimestampTicks
        };
    }
}
