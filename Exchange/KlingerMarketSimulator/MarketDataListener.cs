using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Collections.Concurrent;
using Serilog;

namespace KlingerSimulator;

/// <summary>
/// Listens to UDP multicast market data to track current market prices
/// </summary>
public sealed class MarketDataListener : IDisposable
{
    private readonly ILogger _log = Log.ForContext<MarketDataListener>();
    private readonly UdpClient _udpClient;
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _receiverTask;
    private readonly ConcurrentDictionary<short, MarketPrice> _prices = new();
    private readonly ConcurrentDictionary<short, string> _symbolByIndex = new();
    private readonly ConcurrentDictionary<string, short> _indexBySymbol = new();
    
    public const string MulticastAddress = "239.1.1.100";
    public const int MulticastPort = 9900;
    
    public long MessagesReceived { get; private set; }
    public bool HasInstruments => _symbolByIndex.Count > 0;
    
    public MarketDataListener()
    {
        _udpClient = new UdpClient();
        _udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        _udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReceiveBuffer, 256 * 1024);
        _udpClient.Client.Bind(new IPEndPoint(IPAddress.Any, MulticastPort));
        
        // Join multicast group
        try
        {
            _udpClient.JoinMulticastGroup(IPAddress.Parse(MulticastAddress));
            _log.Information("Joined multicast group {Address}:{Port}", MulticastAddress, MulticastPort);
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Failed to join multicast group");
        }
        
        _receiverTask = Task.Run(ReceiverLoop, _cts.Token);
    }
    
    private async Task ReceiverLoop()
    {
        try
        {
            while (!_cts.Token.IsCancellationRequested)
            {
                try
                {
                    var result = await _udpClient.ReceiveAsync(_cts.Token);
                    var data = result.Buffer;
                    
                    if (data.Length < 1) continue;
                    
                    var messageType = data[0];
                    
                    // Instrument List message
                    if (messageType == 10 && data.Length >= 64) // MSG_TYPE_INSTRUMENT_LIST
                    {
                        ProcessInstrumentList(data);
                    }
                    // Trade message
                    else if (messageType == 1 && data.Length == 64) // MSG_TYPE_TRADE
                    {
                        unsafe
                        {
                            fixed (byte* ptr = data)
                            {
                                var message = *(TradeMessage*)ptr;
                                
                                var price = new MarketPrice
                                {
                                    SymbolIndex = message.SymbolIndex,
                                    LastPrice = (decimal)message.PriceFixed / 100000m,
                                    LastQty = message.Quantity,
                                    Timestamp = DateTimeOffset.FromUnixTimeMilliseconds(message.TimestampNs / 1_000_000)
                                };
                                
                                _prices[message.SymbolIndex] = price;
                                MessagesReceived++;
                            }
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _log.Error(ex, "UDP receive error");
                }
            }
        }
        catch (Exception ex)
        {
            _log.Error(ex, "ReceiverLoop failed");
        }
    }
    
    private void ProcessInstrumentList(byte[] buffer)
    {
        try
        {
            unsafe
            {
                fixed (byte* ptr = buffer)
                {
                    // Read header
                    var header = *(InstrumentListHeader*)ptr;
                    var dataPtr = ptr + 64; // Header size
                    
                    _log.Information("Received instrument list: {Count} instruments", header.Count);
                    
                    for (int i = 0; i < header.Count; i++)
                    {
                        var offset = i * 64; // InstrumentInfo size
                        var info = *(InstrumentInfo*)(dataPtr + offset);
                        
                        var symbol = GetSymbol(info.SymbolBytes);
                        _symbolByIndex[info.SymbolIndex] = symbol;
                        _indexBySymbol[symbol] = info.SymbolIndex;
                        
                        // Initialize price if we don't have one
                        if (!_prices.ContainsKey(info.SymbolIndex))
                        {
                            _prices[info.SymbolIndex] = new MarketPrice
                            {
                                SymbolIndex = info.SymbolIndex,
                                LastPrice = (decimal)info.ReferencePriceFixed / 100_000m,
                                LastQty = 0,
                                Timestamp = DateTimeOffset.UtcNow
                            };
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Failed to process instrument list");
        }
    }
    
    private unsafe string GetSymbol(byte* symbolBytes)
    {
        int len = 0;
        while (len < 32 && symbolBytes[len] != 0) len++;
        return System.Text.Encoding.ASCII.GetString(symbolBytes, len);
    }
    
    /// <summary>
    /// Get last traded price for a symbol
    /// </summary>
    public decimal? GetLastPrice(short symbolIndex)
    {
        if (_prices.TryGetValue(symbolIndex, out var price))
            return price.LastPrice;
        return null;
    }
    
    /// <summary>
    /// Get last traded price by symbol name
    /// </summary>
    public decimal? GetLastPrice(string symbol)
    {
        if (_indexBySymbol.TryGetValue(symbol, out var index))
            return GetLastPrice(index);
        return null;
    }
    
    /// <summary>
    /// Get all available symbols
    /// </summary>
    public string[] GetAllSymbols()
    {
        return _symbolByIndex.Values.ToArray();
    }
    
    /// <summary>
    /// Get symbol index
    /// </summary>
    public short? GetSymbolIndex(string symbol)
    {
        if (_indexBySymbol.TryGetValue(symbol, out var index))
            return index;
        return null;
    }
    
    /// <summary>
    /// Get market price with all details
    /// </summary>
    public MarketPrice? GetMarketPrice(short symbolIndex)
    {
        if (_prices.TryGetValue(symbolIndex, out var price))
            return price;
        return null;
    }
    
    public void Dispose()
    {
        _cts.Cancel();
        _receiverTask.Wait(TimeSpan.FromSeconds(2));
        _udpClient.Dispose();
        _log.Information("MarketDataListener disposed. Received {Count} messages", MessagesReceived);
    }
}

/// <summary>
/// Trade message format - must match Exchange TradeMessage
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal unsafe struct TradeMessage
{
    public byte MessageType;
    public short SymbolIndex;
    public uint SequenceNumber;
    public long TimestampNs;
    public long PriceFixed;
    public long Quantity;
    public long BuyOrderId;
    public long SellOrderId;
    private fixed byte _padding[17];
}

/// <summary>
/// Instrument list header
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal unsafe struct InstrumentListHeader
{
    public byte MessageType;
    public short Count;
    public uint SequenceNumber;
    public long TimestampNs;
    private fixed byte _reserved[49];
}

/// <summary>
/// Instrument info
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal unsafe struct InstrumentInfo
{
    public short SymbolIndex;
    public byte Channel;
    public byte Flags;
    public long TickSizeFixed;
    public int LotSize;
    public long ReferencePriceFixed;
    public long PreviousCloseFixed;
    public fixed byte SymbolBytes[32];
}

/// <summary>
/// Market price information
/// </summary>
public struct MarketPrice
{
    public short SymbolIndex { get; init; }
    public decimal LastPrice { get; init; }
    public long LastQty { get; init; }
    public DateTimeOffset Timestamp { get; init; }
}
