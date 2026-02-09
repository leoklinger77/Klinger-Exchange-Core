using QuickFix.FIX41;

namespace KlingerExchange.Matching.Services;

public static class MessageBuilders {
    private static readonly OrderCancelRejectPool _cancelRejectPool = new();
    private static readonly RejectPool _rejectPool = new();

    public static OrderCancelReject BuildCancelRejectPooled(string orderId, string clOrdId, string origClOrdId, char ordStatus, string rejectReason) {
        var reject = _cancelRejectPool.Rent();

        reject.OrderID = new QuickFix.Fields.OrderID(orderId);
        reject.ClOrdID = new QuickFix.Fields.ClOrdID(clOrdId);
        reject.OrigClOrdID = new QuickFix.Fields.OrigClOrdID(origClOrdId);
        reject.OrdStatus = new QuickFix.Fields.OrdStatus(ordStatus);
        reject.CxlRejReason = new QuickFix.Fields.CxlRejReason(QuickFix.Fields.CxlRejReason.UNKNOWN_ORDER);
        reject.Text = new QuickFix.Fields.Text(rejectReason);

        return reject;
    }

    public static OrderCancelReject BuildReplaceRejectPooled(string orderId, string clOrdId, string origClOrdId, char ordStatus, string rejectReason) {
        var reject = _cancelRejectPool.Rent();

        reject.OrderID = new QuickFix.Fields.OrderID(orderId);
        reject.ClOrdID = new QuickFix.Fields.ClOrdID(clOrdId);
        reject.OrigClOrdID = new QuickFix.Fields.OrigClOrdID(origClOrdId);
        reject.OrdStatus = new QuickFix.Fields.OrdStatus(ordStatus);
        reject.CxlRejReason = new QuickFix.Fields.CxlRejReason(QuickFix.Fields.CxlRejReason.UNKNOWN_ORDER);
        reject.Text = new QuickFix.Fields.Text(rejectReason);

        return reject;
    }

    public static Reject BuildRejectPooled(int refSeqNum, string rejectText) {
        var reject = _rejectPool.Rent();

        reject.RefSeqNum = new QuickFix.Fields.RefSeqNum((ulong)refSeqNum);
        reject.Text = new QuickFix.Fields.Text(rejectText);

        return reject;
    }

    public static void ReturnCancelRejectToPool(OrderCancelReject msg) {
        _cancelRejectPool.Return(msg);
    }

    public static void ReturnRejectToPool(Reject msg) {
        _rejectPool.Return(msg);
    }
}
