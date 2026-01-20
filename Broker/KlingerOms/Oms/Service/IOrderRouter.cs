using KlingerBroker.Oms.Dtos;
using QuickFix;
using QuickFix.Fields;
using Serilog;
using FixSide = QuickFix.Fields.Side;
using OrderSide = KlingerBroker.Oms.Dtos.Side;

namespace KlingerBroker.Oms.Service;

public interface IOrderRouter
{
    event EventHandler<ExecutionReportEvent>? ExecutionReportReceived;
    event EventHandler<RouterStatusEvent>? StatusChanged;

    bool IsConnected { get; }
    SessionID? SessionId { get; set; }

    string SendNewOrder(NewOrderRequest request);
    string ReplaceOrder(ReplaceOrderRequest request);
    string CancelOrder(CancelOrderRequest request);

    void OnExecutionReportFromFix(ExecutionReportEvent er);
    void SetConnected(bool connected, string? details = null);
}

public sealed class FixOrderRouter : IOrderRouter
{
    private static readonly ILogger Logger = Log.ForContext<FixOrderRouter>();
    private readonly SynchronizationContext? _ui;

    public event EventHandler<ExecutionReportEvent>? ExecutionReportReceived;
    public event EventHandler<RouterStatusEvent>? StatusChanged;

    public bool IsConnected { get; private set; }
    public SessionID? SessionId { get; set; }

    public FixOrderRouter(SynchronizationContext? uiContext = null)
    {
        _ui = uiContext ?? SynchronizationContext.Current;
    }

    public string SendNewOrder(NewOrderRequest request)
    {
        if (SessionId is null)
            throw new InvalidOperationException("FIX session not available.");

        var clOrdId = GenerateClOrdId();

        var msg = new QuickFix.FIX41.NewOrderSingle(
            new ClOrdID(clOrdId),
            new HandlInst('1'), // Automated
            new Symbol(request.Symbol),
            new FixSide(request.Side == OrderSide.Buy ? FixSide.BUY : FixSide.SELL),
            new OrdType(OrdType.LIMIT))
        {
            OrderQty = new OrderQty(request.Quantity),
            Price = new Price(request.Price)
        };

        Logger.Information("Sending NewOrderSingle: ClOrdID={ClOrdId} Symbol={Symbol} Side={Side} Qty={Qty} Px={Px}",
            clOrdId, request.Symbol, request.Side, request.Quantity, request.Price);

        Session.SendToTarget(msg, SessionId);
        return clOrdId;
    }

    public string ReplaceOrder(ReplaceOrderRequest request)
    {
        if (SessionId is null)
            throw new InvalidOperationException("FIX session not available.");

        var clOrdId = GenerateClOrdId();

        var msg = new QuickFix.FIX41.OrderCancelReplaceRequest(
            new OrigClOrdID(request.OrigClOrdId),
            new ClOrdID(clOrdId),
            new HandlInst('1'),
            new Symbol("N/A"), // ideally track original symbol
            new FixSide(FixSide.BUY), // ideally track original side
            new OrdType(OrdType.LIMIT));

        if (request.NewQuantity.HasValue)
            msg.OrderQty = new OrderQty(request.NewQuantity.Value);
        if (request.NewPrice.HasValue)
            msg.Price = new Price(request.NewPrice.Value);

        Logger.Information("Sending OrderCancelReplaceRequest: ClOrdID={ClOrdId} OrigClOrdID={OrigClOrdId}",
            clOrdId, request.OrigClOrdId);

        Session.SendToTarget(msg, SessionId);
        return clOrdId;
    }

    public string CancelOrder(CancelOrderRequest request)
    {
        if (SessionId is null)
            throw new InvalidOperationException("FIX session not available.");

        var clOrdId = GenerateClOrdId();

        var msg = new QuickFix.FIX41.OrderCancelRequest(
            new OrigClOrdID(request.OrigClOrdId),
            new ClOrdID(clOrdId),
            new Symbol("N/A"),
            new FixSide(FixSide.BUY));

        Logger.Information("Sending OrderCancelRequest: ClOrdID={ClOrdId} OrigClOrdID={OrigClOrdId}",
            clOrdId, request.OrigClOrdId);

        Session.SendToTarget(msg, SessionId);
        return clOrdId;
    }

    public void OnExecutionReportFromFix(ExecutionReportEvent er)
    {
        Logger.Information("ExecutionReport received: ClOrdID={ClOrdId} ExecType={ExecType} OrdStatus={OrdStatus}",
            er.ClOrdId, er.ExecType, er.OrdStatus);

        if (_ui is not null)
            _ui.Post(_ => ExecutionReportReceived?.Invoke(this, er), null);
        else
            ExecutionReportReceived?.Invoke(this, er);
    }

    public void SetConnected(bool connected, string? details = null)
    {
        IsConnected = connected;
        Logger.Information("Router connection status: {Connected} {Details}", connected, details);

        var evt = new RouterStatusEvent(connected, details);
        if (_ui is not null)
            _ui.Post(_ => StatusChanged?.Invoke(this, evt), null);
        else
            StatusChanged?.Invoke(this, evt);
    }

    private static string GenerateClOrdId() => DateTime.UtcNow.ToString("yyyyMMddHHmmssfff") + Guid.NewGuid().ToString("N")[..8];
}
