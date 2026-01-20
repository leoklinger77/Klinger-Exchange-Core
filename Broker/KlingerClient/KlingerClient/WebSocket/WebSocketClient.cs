using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Serilog;

namespace KlingerClient.WebSocket;

/// <summary>
/// WebSocket client for connecting to OMS
/// </summary>
public sealed class WebSocketClient : IDisposable
{
    private readonly ILogger _logger = Log.ForContext<WebSocketClient>();
    private readonly string _url;
    private ClientWebSocket? _ws;
    private CancellationTokenSource? _cts;
    private Task? _receiveTask;
    private readonly SemaphoreSlim _sendLock = new(1, 1);

    public event EventHandler<WebSocketMessageEventArgs>? MessageReceived;
    public event EventHandler<bool>? ConnectionChanged;

    public bool IsConnected => _ws?.State == WebSocketState.Open;

    public WebSocketClient(string url = "ws://localhost:8080/")
    {
        _url = url;
    }

    public async Task ConnectAsync()
    {
        if (IsConnected)
            return;

        try
        {
            _ws = new ClientWebSocket();
            _cts = new CancellationTokenSource();

            _logger.Information("Connecting to {Url}...", _url);
            await _ws.ConnectAsync(new Uri(_url), _cts.Token);

            _logger.Information("Connected to OMS");
            ConnectionChanged?.Invoke(this, true);

            _receiveTask = ReceiveLoopAsync();
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error connecting to OMS");
            ConnectionChanged?.Invoke(this, false);
            throw;
        }
    }

    public async Task DisconnectAsync()
    {
        if (_ws == null || _ws.State != WebSocketState.Open)
            return;

        try
        {
            _cts?.Cancel();
            
            await _ws.CloseAsync(
                WebSocketCloseStatus.NormalClosure,
                "Client closing",
                CancellationToken.None);

            _logger.Information("Disconnected from OMS");
            ConnectionChanged?.Invoke(this, false);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error disconnecting from OMS");
        }
    }

    public async Task SendAsync(string type, object data)
    {
        if (!IsConnected || _ws == null)
            throw new InvalidOperationException("Not connected");

        var message = new
        {
            type,
            data,
            timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };

        var json = JsonSerializer.Serialize(message);
        var bytes = Encoding.UTF8.GetBytes(json);

        await _sendLock.WaitAsync();
        try
        {
            await _ws.SendAsync(
                new ArraySegment<byte>(bytes),
                WebSocketMessageType.Text,
                true,
                CancellationToken.None);

            _logger.Debug("Sent: {Type}", type);
        }
        finally
        {
            _sendLock.Release();
        }
    }

    private async Task ReceiveLoopAsync()
    {
        if (_ws == null || _cts == null)
            return;

        var buffer = new byte[8192];
        var messageBuilder = new StringBuilder();

        while (_ws.State == WebSocketState.Open && !_cts.Token.IsCancellationRequested)
        {
            try
            {
                var result = await _ws.ReceiveAsync(
                    new ArraySegment<byte>(buffer),
                    _cts.Token);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    _logger.Information("Server closed connection");
                    ConnectionChanged?.Invoke(this, false);
                    break;
                }

                var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                messageBuilder.Append(message);

                if (result.EndOfMessage)
                {
                    var fullMessage = messageBuilder.ToString();
                    messageBuilder.Clear();

                    ProcessMessage(fullMessage);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error receiving message");
                ConnectionChanged?.Invoke(this, false);
                break;
            }
        }
    }

    private void ProcessMessage(string json)
    {
        try
        {
            _logger.Debug("Received: {Json}", json);

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty("type", out var typeElement))
            {
                var type = typeElement.GetString() ?? string.Empty;
                var data = root.TryGetProperty("data", out var dataElement) 
                    ? dataElement.GetRawText() 
                    : "{}";

                MessageReceived?.Invoke(this, new WebSocketMessageEventArgs(type, data));
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error processing message: {Json}", json);
        }
    }

    public void Dispose()
    {
        DisconnectAsync().Wait(TimeSpan.FromSeconds(2));
        
        _cts?.Dispose();
        _ws?.Dispose();
        _sendLock.Dispose();
    }
}

public sealed class WebSocketMessageEventArgs : EventArgs
{
    public string Type { get; }
    public string DataJson { get; }

    public WebSocketMessageEventArgs(string type, string dataJson)
    {
        Type = type;
        DataJson = dataJson;
    }

    public T? GetData<T>()
    {
        try
        {
            return JsonSerializer.Deserialize<T>(DataJson);
        }
        catch
        {
            return default;
        }
    }
}
