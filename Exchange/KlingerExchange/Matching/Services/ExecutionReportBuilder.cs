using KlingerExchange.Matching.Domain;
using KlingerExchange.Matching.Domain.Enums;
using KlingerExchange.Matching.Domain.Struct;
using QuickFix.FIX41;
using DomainSide = KlingerExchange.Matching.Domain.Enums.Side;
using FixSide = QuickFix.Fields.Side;

namespace KlingerExchange.Matching.Services;

public static class ExecutionReportBuilder {
    private static readonly ExecutionReportPool _pool = new();

    public static ExecutionReport BuildNewOrderReportPooled(Order order, string clOrdId) {
        var report = _pool.Rent();

        report.OrderID = new QuickFix.Fields.OrderID(order.OrderId.ToString());
        report.ExecID = new QuickFix.Fields.ExecID(Guid.NewGuid().ToString());
        report.ExecTransType = new QuickFix.Fields.ExecTransType(QuickFix.Fields.ExecTransType.NEW);
        report.ExecType = new QuickFix.Fields.ExecType(QuickFix.Fields.ExecType.NEW);
        report.OrdStatus = new QuickFix.Fields.OrdStatus(MapOrderStatus(order.Status));
        report.Symbol = new QuickFix.Fields.Symbol(order.Symbol);
        report.Side = new FixSide(MapSide(order.Side));
        report.OrderQty = new QuickFix.Fields.OrderQty(order.Quantity);
        report.ClOrdID = new QuickFix.Fields.ClOrdID(clOrdId);
        report.LeavesQty = new QuickFix.Fields.LeavesQty(order.LeavesQty);
        report.CumQty = new QuickFix.Fields.CumQty(order.FilledQty);
        report.AvgPx = new QuickFix.Fields.AvgPx(order.Price);
        report.LastShares = new QuickFix.Fields.LastShares(0);
        report.LastPx = new QuickFix.Fields.LastPx(0);

        return report;
    }

    public static ExecutionReport BuildFillReportPooled(Order order, string clOrdId, Fill fill) {
        var report = _pool.Rent();

        report.OrderID = new QuickFix.Fields.OrderID(order.OrderId.ToString());
        report.ExecID = new QuickFix.Fields.ExecID(Guid.NewGuid().ToString());
        report.ExecTransType = new QuickFix.Fields.ExecTransType(QuickFix.Fields.ExecTransType.NEW);
        report.ExecType = new QuickFix.Fields.ExecType(QuickFix.Fields.ExecType.FILL);
        report.OrdStatus = new QuickFix.Fields.OrdStatus(MapOrderStatus(order.Status));
        report.Symbol = new QuickFix.Fields.Symbol(order.Symbol);
        report.Side = new FixSide(MapSide(order.Side));
        report.OrderQty = new QuickFix.Fields.OrderQty(order.Quantity);
        report.ClOrdID = new QuickFix.Fields.ClOrdID(clOrdId);
        report.LeavesQty = new QuickFix.Fields.LeavesQty(order.LeavesQty);
        report.CumQty = new QuickFix.Fields.CumQty(order.FilledQty);
        report.AvgPx = new QuickFix.Fields.AvgPx(order.Price);
        report.LastShares = new QuickFix.Fields.LastShares(fill.Quantity);
        report.LastPx = new QuickFix.Fields.LastPx(fill.Price);

        return report;
    }

    public static ExecutionReport BuildCancelReportPooled(string symbol, long orderId, string clOrdId, string origClOrdId) {
        var report = _pool.Rent();

        report.OrderID = new QuickFix.Fields.OrderID(orderId.ToString());
        report.ExecID = new QuickFix.Fields.ExecID(Guid.NewGuid().ToString());
        report.ExecTransType = new QuickFix.Fields.ExecTransType(QuickFix.Fields.ExecTransType.NEW);
        report.ExecType = new QuickFix.Fields.ExecType(QuickFix.Fields.ExecType.CANCELED);
        report.OrdStatus = new QuickFix.Fields.OrdStatus(QuickFix.Fields.OrdStatus.CANCELED);
        report.Symbol = new QuickFix.Fields.Symbol(symbol);
        report.ClOrdID = new QuickFix.Fields.ClOrdID(clOrdId);
        report.OrigClOrdID = new QuickFix.Fields.OrigClOrdID(origClOrdId);
        report.Side = new FixSide(FixSide.BUY);
        report.OrderQty = new QuickFix.Fields.OrderQty(0);
        report.LastShares = new QuickFix.Fields.LastShares(0);
        report.LastPx = new QuickFix.Fields.LastPx(0);
        report.LeavesQty = new QuickFix.Fields.LeavesQty(0);
        report.CumQty = new QuickFix.Fields.CumQty(0);
        report.AvgPx = new QuickFix.Fields.AvgPx(0);

        return report;
    }

    public static ExecutionReport BuildRejectReportPooled(string clOrdId, string symbol, string rejectReason) {
        var report = _pool.Rent();

        report.OrderID = new QuickFix.Fields.OrderID("0");
        report.ExecID = new QuickFix.Fields.ExecID(Guid.NewGuid().ToString());
        report.ExecTransType = new QuickFix.Fields.ExecTransType(QuickFix.Fields.ExecTransType.NEW);
        report.ExecType = new QuickFix.Fields.ExecType(QuickFix.Fields.ExecType.REJECTED);
        report.OrdStatus = new QuickFix.Fields.OrdStatus(QuickFix.Fields.OrdStatus.REJECTED);
        report.Symbol = new QuickFix.Fields.Symbol(symbol);
        report.ClOrdID = new QuickFix.Fields.ClOrdID(clOrdId);
        report.Text = new QuickFix.Fields.Text(rejectReason);
        report.Side = new FixSide(FixSide.BUY);
        report.OrderQty = new QuickFix.Fields.OrderQty(0);
        report.LastShares = new QuickFix.Fields.LastShares(0);
        report.LastPx = new QuickFix.Fields.LastPx(0);
        report.LeavesQty = new QuickFix.Fields.LeavesQty(0);
        report.CumQty = new QuickFix.Fields.CumQty(0);
        report.AvgPx = new QuickFix.Fields.AvgPx(0);

        return report;
    }

    public static ExecutionReport BuildReplaceReportPooled(Order order, string clOrdId, string origClOrdId) {
        var report = _pool.Rent();

        report.OrderID = new QuickFix.Fields.OrderID(order.OrderId.ToString());
        report.ExecID = new QuickFix.Fields.ExecID(Guid.NewGuid().ToString());
        report.ExecTransType = new QuickFix.Fields.ExecTransType(QuickFix.Fields.ExecTransType.NEW);
        report.ExecType = new QuickFix.Fields.ExecType(QuickFix.Fields.ExecType.REPLACE);
        report.OrdStatus = new QuickFix.Fields.OrdStatus(MapOrderStatus(order.Status));
        report.Symbol = new QuickFix.Fields.Symbol(order.Symbol);
        report.Side = new FixSide(MapSide(order.Side));
        report.OrderQty = new QuickFix.Fields.OrderQty(order.Quantity);
        report.ClOrdID = new QuickFix.Fields.ClOrdID(clOrdId);
        report.OrigClOrdID = new QuickFix.Fields.OrigClOrdID(origClOrdId);
        report.LeavesQty = new QuickFix.Fields.LeavesQty(order.LeavesQty);
        report.CumQty = new QuickFix.Fields.CumQty(order.FilledQty);
        report.AvgPx = new QuickFix.Fields.AvgPx(order.Price);
        report.LastShares = new QuickFix.Fields.LastShares(0);
        report.LastPx = new QuickFix.Fields.LastPx(0);

        return report;
    }

    public static void ReturnToPool(ExecutionReport report) {
        _pool.Return(report);
    }

    private static char MapSide(DomainSide side) => side switch {
        DomainSide.Buy => FixSide.BUY,
        DomainSide.Sell => FixSide.SELL,
        _ => FixSide.BUY
    };

    private static char MapOrderStatus(OrderStatus status) => status switch {
        OrderStatus.New => QuickFix.Fields.OrdStatus.NEW,
        OrderStatus.PartiallyFilled => QuickFix.Fields.OrdStatus.PARTIALLY_FILLED,
        OrderStatus.Filled => QuickFix.Fields.OrdStatus.FILLED,
        OrderStatus.Canceled => QuickFix.Fields.OrdStatus.CANCELED,
        _ => QuickFix.Fields.OrdStatus.NEW
    };
}
