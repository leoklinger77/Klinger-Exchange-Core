using QuickFix.FIX41;
using DomainSide = KlingerExchange.Matching.Domain.Enums.Side;
using FixSide = QuickFix.Fields.Side;

namespace KlingerExchange.Matching.Services;

public static class FastOrderParser {
    public static (string ClOrdId, string Symbol, DomainSide Side, decimal Price, decimal Quantity) ParseNewOrderSingleFast(NewOrderSingle message) {
        var clOrdId = message.ClOrdID.Value;
        var symbol = message.Symbol.Value;

        var side = message.Side.Value == FixSide.BUY
            ? DomainSide.Buy
            : DomainSide.Sell;

        var price = message.Price.Value;
        var quantity = message.OrderQty.Value;

        return (clOrdId, symbol, side, price, quantity);
    }

    public static (string OrigClOrdId, long OrderId) ParseOrderCancelRequestFast(OrderCancelRequest message) {
        var origClOrdId = message.OrigClOrdID.Value;

        // Try to parse OrderID as long
        long orderId = 0;
        if (message.IsSetField(QuickFix.Fields.Tags.OrderID)) {
            var orderIdStr = message.OrderID.Value;
            _ = long.TryParse(orderIdStr, out orderId);
        }

        return (origClOrdId, orderId);
    }
}
