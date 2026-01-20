using KlingerBroker.MarketData;
using KlingerBroker.Oms.Dtos;
using KlingerBroker.Oms.Service;
using Serilog;
using System.Linq;
using System.Text.Json;

namespace KlingerBroker.WebSocket;

/// <summary>
/// Bridges WebSocket server with OMS components (FIX router and market data)
/// </summary>
public sealed class OmsWebSocketBridge : IDisposable
{
    private readonly ILogger _logger = Log.ForContext<OmsWebSocketBridge>();
    private readonly WebSocketServer _wsServer;
    private readonly IOrderRouter _orderRouter;
    private readonly MarketDataService? _marketDataService;
    private IDisposable? _marketDataSubscription;

    public OmsWebSocketBridge(
        WebSocketServer wsServer,
        IOrderRouter orderRouter,
        MarketDataService? marketDataService = null)
    {
        _wsServer = wsServer;
        _orderRouter = orderRouter;
        _marketDataService = marketDataService;

        // Wire up events
        _wsServer.MessageReceived += OnWebSocketMessageReceived;
        _wsServer.ClientConnected += OnClientConnected;
        _orderRouter.ExecutionReportReceived += OnExecutionReportReceived;
        _orderRouter.StatusChanged += OnRouterStatusChanged;

        // Subscribe to market data if available
        if (_marketDataService != null)
        {
            _marketDataSubscription = _marketDataService.TradeStream.Subscribe(OnTradeReceived);
            
            // Subscribe to instrument list
            _marketDataService.InstrumentListStream.Subscribe(OnInstrumentListReceived);
            
            // Send instruments if already available
            if (_marketDataService.Instruments != null)
            {
                BroadcastInstruments(_marketDataService.Instruments);
            }
        }

        _logger.Information("OMS WebSocket bridge initialized");
    }

    private void OnClientConnected(object? sender, string clientId)
    {
        _logger.Information("New client connected: {ClientId}, sending instruments and status", clientId);
        
        // Send instruments to the new client
        if (_marketDataService?.Instruments != null)
        {
            BroadcastInstruments(_marketDataService.Instruments);
        }
        
        // Send FIX connection status
        _ = Task.Run(async () =>
        {
            try
            {
                await _wsServer.BroadcastAsync(new WebSocketMessage
                {
                    Type = "status",
                    Data = new WsStatusUpdate 
                    { 
                        Component = "FIX",
                        IsConnected = _orderRouter.IsConnected,
                        Details = _orderRouter.IsConnected ? "Connected to Exchange" : "Disconnected"
                    }
                }, clientId);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error sending initial status to client {ClientId}", clientId);
            }
        });
    }

    private void OnWebSocketMessageReceived(object? sender, MessageReceivedEventArgs e)
    {
        try
        {
            var msg = e.Message;
            _logger.Debug("Processing message type: {Type} from {ClientId}", msg.Type, e.ClientId);

            switch (msg.Type.ToLowerInvariant())
            {
                case "neworder":
                    HandleNewOrder(e.ClientId, msg.Data);
                    break;

                case "replaceorder":
                    HandleReplaceOrder(e.ClientId, msg.Data);
                    break;

                case "cancelorder":
                    HandleCancelOrder(e.ClientId, msg.Data);
                    break;

                case "ping":
                    HandlePing(e.ClientId);
                    break;

                default:
                    _logger.Warning("Unknown message type: {Type}", msg.Type);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error processing WebSocket message from {ClientId}", e.ClientId);
            SendError(e.ClientId, ex.Message);
        }
    }

    private void HandleNewOrder(string clientId, object data)
    {
        var json = JsonSerializer.Serialize(data);
        var request = JsonSerializer.Deserialize<WsNewOrderRequest>(json);

        if (request == null)
        {
            SendError(clientId, "Invalid new order request");
            return;
        }

        try
        {
            var side = request.Side.ToLowerInvariant() == "buy" ? Side.Buy : Side.Sell;
            var orderRequest = new NewOrderRequest(request.Symbol, side, request.Quantity, request.Price);
            
            var clOrdId = _orderRouter.SendNewOrder(orderRequest);

            _wsServer.SendToClientAsync(clientId, new WebSocketMessage
            {
                Type = "orderAck",
                Data = new WsOrderAck
                {
                    ClOrdId = clOrdId,
                    Status = "Accepted",
                    Message = "Order sent to exchange"
                }
            }).Wait();

            _logger.Information("Order accepted: {ClOrdId} from {ClientId}", clOrdId, clientId);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error sending new order from {ClientId}", clientId);
            SendError(clientId, ex.Message);
        }
    }

    private void HandleReplaceOrder(string clientId, object data)
    {
        var json = JsonSerializer.Serialize(data);
        var request = JsonSerializer.Deserialize<WsReplaceOrderRequest>(json);

        if (request == null)
        {
            SendError(clientId, "Invalid replace order request");
            return;
        }

        try
        {
            var replaceRequest = new ReplaceOrderRequest(request.OrigClOrdId, request.NewQuantity, request.NewPrice);
            var clOrdId = _orderRouter.ReplaceOrder(replaceRequest);

            _wsServer.SendToClientAsync(clientId, new WebSocketMessage
            {
                Type = "orderAck",
                Data = new WsOrderAck
                {
                    ClOrdId = clOrdId,
                    Status = "Accepted",
                    Message = "Replace request sent to exchange"
                }
            }).Wait();

            _logger.Information("Replace accepted: {ClOrdId} from {ClientId}", clOrdId, clientId);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error sending replace order from {ClientId}", clientId);
            SendError(clientId, ex.Message);
        }
    }

    private void HandleCancelOrder(string clientId, object data)
    {
        var json = JsonSerializer.Serialize(data);
        var request = JsonSerializer.Deserialize<WsCancelOrderRequest>(json);

        if (request == null)
        {
            SendError(clientId, "Invalid cancel order request");
            return;
        }

        try
        {
            var cancelRequest = new CancelOrderRequest(request.OrigClOrdId);
            var clOrdId = _orderRouter.CancelOrder(cancelRequest);

            _wsServer.SendToClientAsync(clientId, new WebSocketMessage
            {
                Type = "orderAck",
                Data = new WsOrderAck
                {
                    ClOrdId = clOrdId,
                    Status = "Accepted",
                    Message = "Cancel request sent to exchange"
                }
            }).Wait();

            _logger.Information("Cancel accepted: {ClOrdId} from {ClientId}", clOrdId, clientId);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error sending cancel order from {ClientId}", clientId);
            SendError(clientId, ex.Message);
        }
    }

    private void HandlePing(string clientId)
    {
        _wsServer.SendToClientAsync(clientId, new WebSocketMessage
        {
            Type = "pong",
            Data = new { timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() }
        }).Wait();
    }

    private void OnExecutionReportReceived(object? sender, ExecutionReportEvent e)
    {
        try
        {
            var wsReport = WsExecutionReport.FromDto(e);
            
            _wsServer.BroadcastAsync(new WebSocketMessage
            {
                Type = "executionReport",
                Data = wsReport
            }).Wait();

            _logger.Debug("Broadcasted execution report: {ClOrdId}", e.ClOrdId);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error broadcasting execution report");
        }
    }

    private void OnRouterStatusChanged(object? sender, RouterStatusEvent e)
    {
        try
        {
            _wsServer.BroadcastAsync(new WebSocketMessage
            {
                Type = "status",
                Data = new WsStatusUpdate
                {
                    Component = "FIX",
                    IsConnected = e.IsConnected,
                    Details = e.Details
                }
            }).Wait();

            _logger.Information("Broadcasted FIX status: {IsConnected}", e.IsConnected);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error broadcasting router status");
        }
    }

    private void OnTradeReceived(TradeMessage trade)
    {
        // Fire and forget - don't block the market data thread
        _ = Task.Run(async () =>
        {
            try
            {
                var symbol = _marketDataService?.Instruments?
                    .FirstOrDefault(i => i.SymbolIndex == trade.SymbolIndex)
                    .GetSymbol();

                var wsTrade = new WsTradeUpdate
                {
                    Symbol = symbol ?? trade.SymbolIndex.ToString(),
                    SymbolIndex = trade.SymbolIndex,
                    Sequence = trade.SequenceNumber,
                    TimestampNs = trade.TimestampNs,
                    Price = trade.Price,
                    Quantity = trade.Quantity,
                    BuyOrderId = trade.BuyOrderId,
                    SellOrderId = trade.SellOrderId
                };

                await _wsServer.BroadcastAsync(new WebSocketMessage
                {
                    Type = "trade",
                    Data = wsTrade
                });

                _logger.Information("Broadcasted trade: seq={Seq} symbol={Symbol} (idx={SymbolIdx}) qty={Qty}",
                    wsTrade.Sequence, wsTrade.Symbol ?? "?", wsTrade.SymbolIndex, wsTrade.Quantity);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error broadcasting trade");
            }
        });
    }

    private void OnInstrumentListReceived(InstrumentInfo[] instruments)
    {
        try
        {
            _logger.Information("Received instrument list from market data, broadcasting to clients");
            BroadcastInstruments(instruments);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error broadcasting instrument list");
        }
    }

    private void BroadcastInstruments(InstrumentInfo[] instruments)
    {
        var wsInstruments = instruments.Select(i => new WsInstrumentInfo
        {
            SymbolIndex = i.SymbolIndex,
            Symbol = i.GetSymbol(),
            Channel = i.Channel,
            TickSize = i.TickSize,
            LotSize = i.LotSize,
            ReferencePrice = i.ReferencePrice,
            PreviousClose = i.PreviousClose,
            IsFractional = i.IsFractional
        }).ToArray();

        _wsServer.BroadcastAsync(new WebSocketMessage
        {
            Type = "instrumentList",
            Data = wsInstruments
        }).Wait();

        _logger.Information("Broadcasted {Count} instruments to clients", instruments.Length);
    }

    private void SendError(string clientId, string message)
    {
        try
        {
            _wsServer.SendToClientAsync(clientId, new WebSocketMessage
            {
                Type = "error",
                Data = new { message }
            }).Wait();
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error sending error message to {ClientId}", clientId);
        }
    }

    public void Dispose()
    {
        _marketDataSubscription?.Dispose();
        _wsServer.MessageReceived -= OnWebSocketMessageReceived;
        _wsServer.ClientConnected -= OnClientConnected;
        _orderRouter.ExecutionReportReceived -= OnExecutionReportReceived;
        _orderRouter.StatusChanged -= OnRouterStatusChanged;

        _logger.Information("OMS WebSocket bridge disposed");
    }
}
