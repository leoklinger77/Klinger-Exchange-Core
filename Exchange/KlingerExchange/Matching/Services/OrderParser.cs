using DomainSide = KlingerExchange.Matching.Domain.Side;
using FixSide = QuickFix.Fields.Side;

namespace KlingerExchange.Matching.Services;

public static class OrderParser
{
    public static (string ClOrdId, string Symbol, DomainSide Side, decimal Price, decimal Quantity) ParseNewOrderSingle(QuickFix.FIX41.NewOrderSingle message)
    {
        var clOrdId = message.ClOrdID.Value;
        var symbol = message.Symbol.Value;
        var side = message.Side.Value == FixSide.BUY ? DomainSide.Buy : DomainSide.Sell;
        var price = message.Price.Value;
        var quantity = message.OrderQty.Value;

        return (clOrdId, symbol, side, price, quantity);
    }

    public static (string origClOrdId, long orderId) ParseOrderCancelRequest(QuickFix.FIX41.OrderCancelRequest message)
    {
        var origClOrdId = message.OrigClOrdID.Value;
        var orderIdStr = message.OrderID.Value;
        var orderId = long.Parse(orderIdStr);

        return (origClOrdId, orderId);
    }
}
