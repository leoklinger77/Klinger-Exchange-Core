using System.Collections.Concurrent;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Serilog;

namespace KlingerBroker.WebSocket;

/// <summary>
/// WebSocket server that manages client connections and message routing
/// </summary>
public sealed class WebSocketServer : IDisposable
{
    private readonly ILogger _logger = Log.ForContext<WebSocketServer>();
    private readonly HttpListener _listener;
    private readonly ConcurrentDictionary<string, ClientConnection> _clients = new();
    private readonly CancellationTokenSource _cts = new();
    private readonly string _url;
    private Task? _acceptTask;

    public event EventHandler<MessageReceivedEventArgs>? MessageReceived;
    public event EventHandler<string>? ClientConnected;
    
    public int ClientCount => _clients.Count;
    public bool IsRunning { get; private set; }

    public WebSocketServer(string url = "http://localhost:8080/")
    {
        _url = url;
        _listener = new HttpListener();
        _listener.Prefixes.Add(_url);
    }

    public void Start()
    {
        if (IsRunning)
            return;

        _listener.Start();
        IsRunning = true;
        _acceptTask = AcceptClientsAsync();
        
        _logger.Information("WebSocket server started at {Url}", _url);
    }

    public void Stop()
    {
        if (!IsRunning)
            return;

        IsRunning = false;
        _cts.Cancel();
        _listener.Stop();
        
        _logger.Information("WebSocket server stopped");
    }

    private async Task AcceptClientsAsync()
    {
        while (!_cts.Token.IsCancellationRequested)
        {
            try
            {
                var context = await _listener.GetContextAsync();
                
                if (context.Request.IsWebSocketRequest)
                {
                    _ = HandleWebSocketAsync(context);
                }
                else
                {
                    context.Response.StatusCode = 400;
                    context.Response.Close();
                }
            }
            catch (Exception ex) when (!_cts.Token.IsCancellationRequested)
            {
                _logger.Error(ex, "Error accepting WebSocket connection");
            }
        }
    }

    private async Task HandleWebSocketAsync(HttpListenerContext context)
    {
        WebSocketContext? wsContext = null;
        try
        {
            wsContext = await context.AcceptWebSocketAsync(null);
            var clientId = Guid.NewGuid().ToString();
            var client = new ClientConnection(clientId, wsContext.WebSocket);
            
            _clients.TryAdd(clientId, client);
            _logger.Information("Client connected: {ClientId} (Total: {Count})", clientId, _clients.Count);

            // Notify listeners that a new client connected
            ClientConnected?.Invoke(this, clientId);

            await BroadcastAsync(new WebSocketMessage
            {
                Type = "status",
                Data = new WsStatusUpdate
                {
                    Component = "WebSocket",
                    IsConnected = true,
                    Details = "Connected to OMS"
                }
            }, clientId);

            await ReceiveMessagesAsync(client);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error handling WebSocket connection");
        }
        finally
        {
            if (wsContext != null)
            {
                var clientId = _clients.FirstOrDefault(x => x.Value.WebSocket == wsContext.WebSocket).Key;
                if (clientId != null && _clients.TryRemove(clientId, out var client))
                {
                    await client.CloseAsync();
                    _logger.Information("Client disconnected: {ClientId} (Total: {Count})", clientId, _clients.Count);
                }
            }
        }
    }

    private async Task ReceiveMessagesAsync(ClientConnection client)
    {
        var buffer = new byte[8192];
        var messageBuilder = new StringBuilder();

        while (client.WebSocket.State == WebSocketState.Open && !_cts.Token.IsCancellationRequested)
        {
            try
            {
                var result = await client.WebSocket.ReceiveAsync(
                    new ArraySegment<byte>(buffer), 
                    _cts.Token);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await client.WebSocket.CloseAsync(
                        WebSocketCloseStatus.NormalClosure,
                        "Closing",
                        CancellationToken.None);
                    break;
                }

                var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                messageBuilder.Append(message);

                if (result.EndOfMessage)
                {
                    var fullMessage = messageBuilder.ToString();
                    messageBuilder.Clear();

                    ProcessMessage(client.ClientId, fullMessage);
                }
            }
            catch (Exception ex) when (!_cts.Token.IsCancellationRequested)
            {
                _logger.Error(ex, "Error receiving message from {ClientId}", client.ClientId);
                break;
            }
        }
    }

    private void ProcessMessage(string clientId, string message)
    {
        try
        {
            _logger.Debug("Received from {ClientId}: {Message}", clientId, message);
            
            var wsMessage = JsonSerializer.Deserialize<WebSocketMessage>(message);
            if (wsMessage != null)
            {
                MessageReceived?.Invoke(this, new MessageReceivedEventArgs(clientId, wsMessage));
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error processing message from {ClientId}: {Message}", clientId, message);
        }
    }

    public async Task BroadcastAsync(WebSocketMessage message, string? excludeClientId = null)
    {
        var json = JsonSerializer.Serialize(message);
        var bytes = Encoding.UTF8.GetBytes(json);

        var tasks = _clients.Values
            .Where(c => c.ClientId != excludeClientId && c.WebSocket.State == WebSocketState.Open)
            .Select(c => SendToClientAsync(c, bytes));

        await Task.WhenAll(tasks);
    }

    public async Task SendToClientAsync(string clientId, WebSocketMessage message)
    {
        if (_clients.TryGetValue(clientId, out var client))
        {
            var json = JsonSerializer.Serialize(message);
            var bytes = Encoding.UTF8.GetBytes(json);
            await SendToClientAsync(client, bytes);
        }
    }

    private async Task SendToClientAsync(ClientConnection client, byte[] data)
    {
        if (client.WebSocket.State != WebSocketState.Open)
            return;

        try
        {
            await client.Semaphore.WaitAsync();
            try
            {
                await client.WebSocket.SendAsync(
                    new ArraySegment<byte>(data),
                    WebSocketMessageType.Text,
                    true,
                    CancellationToken.None);
            }
            finally
            {
                client.Semaphore.Release();
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error sending to client {ClientId}", client.ClientId);
        }
    }

    public void Dispose()
    {
        Stop();
        _cts.Dispose();
        _listener.Close();
        
        foreach (var client in _clients.Values)
        {
            client.CloseAsync().Wait(TimeSpan.FromSeconds(1));
        }
        _clients.Clear();
    }

    private sealed class ClientConnection
    {
        public string ClientId { get; }
        public System.Net.WebSockets.WebSocket WebSocket { get; }
        public SemaphoreSlim Semaphore { get; } = new(1, 1);

        public ClientConnection(string clientId, System.Net.WebSockets.WebSocket webSocket)
        {
            ClientId = clientId;
            WebSocket = webSocket;
        }

        public async Task CloseAsync()
        {
            if (WebSocket.State == WebSocketState.Open)
            {
                try
                {
                    await WebSocket.CloseAsync(
                        WebSocketCloseStatus.NormalClosure,
                        "Server closing",
                        CancellationToken.None);
                }
                catch { }
            }
            WebSocket.Dispose();
            Semaphore.Dispose();
        }
    }
}

public sealed class MessageReceivedEventArgs : EventArgs
{
    public string ClientId { get; }
    public WebSocketMessage Message { get; }

    public MessageReceivedEventArgs(string clientId, WebSocketMessage message)
    {
        ClientId = clientId;
        Message = message;
    }
}
