using KlingerClient.WebSocket;

namespace KlingerClient.Services;

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

/// <summary>
/// Order router that communicates via WebSocket
/// </summary>
public interface IOrderRouter
{
    event EventHandler<ExecutionReportEvent>? ExecutionReportReceived;
    event EventHandler<RouterStatusEvent>? StatusChanged;

    bool IsConnected { get; }

    string SendNewOrder(NewOrderRequest request);
    string ReplaceOrder(ReplaceOrderRequest request);
    string CancelOrder(CancelOrderRequest request);
}

public sealed class WebSocketOrderRouter : IOrderRouter, IDisposable
{
    private readonly WebSocketClient _wsClient;
    private readonly SynchronizationContext? _uiContext;

    public event EventHandler<ExecutionReportEvent>? ExecutionReportReceived;
    public event EventHandler<RouterStatusEvent>? StatusChanged;

    public bool IsConnected => _wsClient.IsConnected;

    public WebSocketOrderRouter(WebSocketClient wsClient, SynchronizationContext? uiContext = null)
    {
        _wsClient = wsClient;
        _uiContext = uiContext ?? SynchronizationContext.Current;

        _wsClient.MessageReceived += OnMessageReceived;
        _wsClient.ConnectionChanged += OnConnectionChanged;
    }

    public string SendNewOrder(NewOrderRequest request)
    {
        if (!IsConnected)
            throw new InvalidOperationException("Not connected to OMS");

        var message = new NewOrderMessage
        {
            Symbol = request.Symbol,
            Side = request.Side == Side.Buy ? "Buy" : "Sell",
            Quantity = request.Quantity,
            Price = request.Price
        };

        _wsClient.SendAsync("newOrder", message).Wait();
        
        // ClOrdId will come back in orderAck
        return $"pending_{Guid.NewGuid():N}";
    }

    public string ReplaceOrder(ReplaceOrderRequest request)
    {
        if (!IsConnected)
            throw new InvalidOperationException("Not connected to OMS");

        var message = new ReplaceOrderMessage
        {
            OrigClOrdId = request.OrigClOrdId,
            NewQuantity = request.NewQuantity,
            NewPrice = request.NewPrice
        };

        _wsClient.SendAsync("replaceOrder", message).Wait();
        return $"pending_{Guid.NewGuid():N}";
    }

    public string CancelOrder(CancelOrderRequest request)
    {
        if (!IsConnected)
            throw new InvalidOperationException("Not connected to OMS");

        var message = new CancelOrderMessage
        {
            OrigClOrdId = request.OrigClOrdId
        };

        _wsClient.SendAsync("cancelOrder", message).Wait();
        return $"pending_{Guid.NewGuid():N}";
    }

    private void OnMessageReceived(object? sender, WebSocketMessageEventArgs e)
    {
        switch (e.Type.ToLowerInvariant())
        {
            case "executionreport":
                HandleExecutionReport(e);
                break;

            case "status":
                HandleStatus(e);
                break;

            case "orderack":
                // Could log or handle acknowledgements
                break;

            case "error":
                HandleError(e);
                break;
        }
    }

    private void HandleExecutionReport(WebSocketMessageEventArgs e)
    {
        var report = e.GetData<ExecutionReportMessage>();
        if (report == null)
            return;

        var ev = new ExecutionReportEvent(
            report.ClOrdId,
            report.OrderId,
            report.Symbol,
            report.ExecType,
            report.OrdStatus,
            report.CumQty,
            report.LeavesQty,
            report.LastPx,
            report.LastQty,
            report.Text);

        RaiseOnUiThread(() => ExecutionReportReceived?.Invoke(this, ev));
    }

    private void HandleStatus(WebSocketMessageEventArgs e)
    {
        var status = e.GetData<StatusUpdateMessage>();
        if (status == null)
            return;

        var ev = new RouterStatusEvent(status.IsConnected, status.Details);
        RaiseOnUiThread(() => StatusChanged?.Invoke(this, ev));
    }

    private void HandleError(WebSocketMessageEventArgs e)
    {
        var error = e.GetData<ErrorMessage>();
        if (error != null)
        {
            var ev = new RouterStatusEvent(false, $"Error: {error.Message}");
            RaiseOnUiThread(() => StatusChanged?.Invoke(this, ev));
        }
    }

    private void OnConnectionChanged(object? sender, bool isConnected)
    {
        var ev = new RouterStatusEvent(isConnected, isConnected ? "Connected" : "Disconnected");
        RaiseOnUiThread(() => StatusChanged?.Invoke(this, ev));
    }

    private void RaiseOnUiThread(Action action)
    {
        if (_uiContext != null)
            _uiContext.Post(_ => action(), null);
        else
            action();
    }

    public void Dispose()
    {
        _wsClient.MessageReceived -= OnMessageReceived;
        _wsClient.ConnectionChanged -= OnConnectionChanged;
    }
}
