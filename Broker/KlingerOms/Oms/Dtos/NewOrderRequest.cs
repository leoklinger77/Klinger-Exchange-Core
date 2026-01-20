namespace KlingerBroker.Oms.Dtos {
    public enum Side { Buy, Sell }
    public sealed record NewOrderRequest(string Symbol, Side Side, int Quantity, decimal Price);
    public sealed record ReplaceOrderRequest(string OrigClOrdId, int? NewQuantity, decimal? NewPrice);
    public sealed record CancelOrderRequest(string OrigClOrdId);

    public sealed record RouterStatusEvent(bool IsConnected, string? Details);
    public sealed record ExecutionReportEvent(
        string ClOrdId,
        string? OrderId,
        string? Symbol,
        string ExecType,
        string OrdStatus,
        int CumQty,
        int LeavesQty,
        decimal? LastPx,
        int? LastQty,
        string? Text);

}
