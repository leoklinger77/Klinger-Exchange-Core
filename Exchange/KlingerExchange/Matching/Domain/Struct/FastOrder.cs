namespace KlingerExchange.Matching.Domain.Struct;

public readonly struct FastOrder {
    public readonly long OrderId;
    public readonly decimal LeavesQty;

    public FastOrder(long orderId, decimal leavesQty) {
        OrderId = orderId;
        LeavesQty = leavesQty;
    }
}
