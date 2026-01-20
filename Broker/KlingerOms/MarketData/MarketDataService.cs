using System.Net;
using System.Net.Sockets;
using System.Reactive.Subjects;
using System.Text;
using System.Text.Json;
using System.Linq;
using Serilog;

namespace KlingerBroker.MarketData;

/// <summary>
/// Centralized market data receiver - single UDP connection with Rx distribution
/// </summary>
public sealed class MarketDataService : IDisposable
{
    private readonly ILogger _logger = Log.ForContext<MarketDataService>();
    private readonly Subject<TradeMessage> _tradeStream = new();
    private readonly Subject<InstrumentInfo[]> _instrumentListStream = new();
    private readonly UdpClient _udpClient;
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _receiverTask;
    private readonly Task _instrumentFetchTask;
    
    public IObservable<TradeMessage> TradeStream => _tradeStream;
    public IObservable<InstrumentInfo[]> InstrumentListStream => _instrumentListStream;
    public long MessagesReceived { get; private set; }
    public InstrumentInfo[]? Instruments { get; private set; }

    public MarketDataService(string multicastAddress = "239.1.1.100", int port = 9900)
    {
        // Create UDP client and join multicast group
        _udpClient = new UdpClient();
        _udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        _udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReceiveBuffer, 256 * 1024);
        _udpClient.Client.Bind(new IPEndPoint(IPAddress.Any, port));
        
        // Join multicast group on loopback interface for local development
        // This ensures packets sent via loopback are received
        try
        {
            var multicastAddr = IPAddress.Parse(multicastAddress);
            // Try loopback first for local dev, then any interface
            try
            {
                _udpClient.JoinMulticastGroup(multicastAddr, IPAddress.Loopback);
                _logger.Information("MarketDataService joined multicast group {Address}:{Port} on loopback", multicastAddress, port);
            }
            catch
            {
                _udpClient.JoinMulticastGroup(multicastAddr);
                _logger.Information("MarketDataService joined multicast group {Address}:{Port} on default interface", multicastAddress, port);
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to join multicast group {Address}", multicastAddress);
        }

        _receiverTask = Task.Run(ReceiverLoop, _cts.Token);
        _instrumentFetchTask = Task.Run(FetchInstrumentsFromApi, _cts.Token);
    }

    private async Task FetchInstrumentsFromApi()
    {
        while (!_cts.Token.IsCancellationRequested)
        {
            try
            {
                using var client = new HttpClient();
                client.Timeout = TimeSpan.FromSeconds(5);
                var response = await client.GetStringAsync("http://localhost:5000/instruments", _cts.Token);
                
                // Parse JSON to get instruments with full data including reference prices
                var doc = JsonDocument.Parse(response);
                var instruments = doc.RootElement.GetProperty("instruments").EnumerateArray()
                    .Select(e => 
                    {
                        var symbolIndex = (short)e.GetProperty("symbolIndex").GetInt32();
                        var symbol = e.GetProperty("symbol").GetString()!;
                        var channel = e.TryGetProperty("channel", out var chProp) ? (byte)chProp.GetInt32() : (byte)0;
                        var tickSize = e.TryGetProperty("tickSize", out var tsProp) ? tsProp.GetDecimal() : 0.01m;
                        var lotSize = e.TryGetProperty("lotSize", out var lsProp) ? lsProp.GetInt32() : 100;
                        var referencePrice = e.TryGetProperty("referencePrice", out var rpProp) ? rpProp.GetDecimal() : 0m;
                        var previousClose = e.TryGetProperty("previousClose", out var pcProp) ? pcProp.GetDecimal() : 0m;
                        var isFractional = e.TryGetProperty("isFractional", out var ifProp) && ifProp.GetBoolean();
                        
                        // Create InstrumentInfo with all data
                        var info = new InstrumentInfo();
                        info.SymbolIndex = symbolIndex;
                        info.Channel = channel;
                        info.TickSizeFixed = (long)(tickSize * 100_000m);
                        info.LotSize = lotSize;
                        info.ReferencePriceFixed = (long)(referencePrice * 100_000m);
                        info.PreviousCloseFixed = (long)(previousClose * 100_000m);
                        info.Flags = (byte)(isFractional ? 1 : 0);
                        
                        unsafe
                        {
                            var symbolBytes = Encoding.ASCII.GetBytes(symbol);
                            for (int i = 0; i < Math.Min(symbolBytes.Length, 32); i++)
                                info._symbolBytes[i] = symbolBytes[i];
                        }
                        return info;
                    })
                    .ToArray();
                
                Instruments = instruments;
                _instrumentListStream.OnNext(instruments);
                
                _logger.Information("Fetched {Count} instruments from API", instruments.Length);
                break; // Success, exit loop
            }
            catch (Exception ex)
            {
                _logger.Warning(ex, "Failed to fetch instruments from API, retrying in 2 seconds...");
                await Task.Delay(2000, _cts.Token);
            }
        }
    }

    private async Task ReceiverLoop()
    {
        var endpoint = new IPEndPoint(IPAddress.Any, 0);
        
        try
        {
            while (!_cts.Token.IsCancellationRequested)
            {
                try
                {
                    var result = await _udpClient.ReceiveAsync(_cts.Token);
                    var data = result.Buffer;

                    _logger.Debug("UDP Received: len={Length} type={Type}", data.Length, data.Length > 0 ? data[0] : -1);

                    // Process trade messages (instruments come from REST API)
                    if (data.Length == TradeMessage.MessageSize && data[0] == TradeMessage.MSG_TYPE_TRADE)
                    {
                        unsafe
                        {
                            fixed (byte* ptr = data)
                            {
                                var message = *(TradeMessage*)ptr;
                                MessagesReceived++;
                                var symbol = Instruments?
                                    .FirstOrDefault(i => i.SymbolIndex == message.SymbolIndex)
                                    .GetSymbol();

                                _logger.Information("Trade received: seq={Seq} symbol={Symbol} (idx={SymbolIdx}) qty={Qty}", 
                                    message.SequenceNumber, symbol ?? "?", message.SymbolIndex, message.Quantity);
                                _tradeStream.OnNext(message);
                            }
                        }
                    }
                    else
                    {
                        _logger.Warning("Ignored packet: len={Length} expected={Expected}", data.Length, TradeMessage.MessageSize);
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "UDP receive error");
                }
            }
        }
        finally
        {
            _tradeStream.OnCompleted();
            _instrumentListStream.OnCompleted();
        }
    }

    public void Dispose()
    {
        _cts.Cancel();
        
        try
        {
            _receiverTask.Wait(1000);
        }
        catch { }
        
        _udpClient?.Dispose();
        _cts?.Dispose();
        _tradeStream?.Dispose();
        _instrumentListStream?.Dispose();
    }
}
