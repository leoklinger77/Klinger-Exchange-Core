using System.Reactive.Subjects;
using KlingerClient.WebSocket;
using Serilog;

namespace KlingerClient.Services;

/// <summary>
/// Trade message from WebSocket
/// </summary>
public sealed record TradeMessage
{
    public string? Symbol { get; init; }
    public short SymbolIndex { get; init; }
    public uint SequenceNumber { get; init; }
    public long TimestampNs { get; init; }
    public decimal Price { get; init; }
    public long Quantity { get; init; }
    public long BuyOrderId { get; init; }
    public long SellOrderId { get; init; }
}

/// <summary>
/// Instrument information
/// </summary>
public sealed record InstrumentInfo
{
    public short SymbolIndex { get; init; }
    public required string Symbol { get; init; }
    public byte Channel { get; init; }
    public decimal TickSize { get; init; }
    public int LotSize { get; init; }
    public decimal ReferencePrice { get; init; }
    public decimal PreviousClose { get; init; }
    public bool IsFractional { get; init; }
}

/// <summary>
/// Market data service that receives trades via WebSocket
/// </summary>
public sealed class MarketDataService : IDisposable
{
    private readonly ILogger _logger = Log.ForContext<MarketDataService>();
    private readonly Subject<TradeMessage> _tradeStream = new();
    private readonly Subject<InstrumentInfo[]> _instrumentListStream = new();
    private readonly WebSocketClient _wsClient;
    private readonly SynchronizationContext? _uiContext;

    public IObservable<TradeMessage> TradeStream => _tradeStream;
    public IObservable<InstrumentInfo[]> InstrumentListStream => _instrumentListStream;
    public long MessagesReceived { get; private set; }
    public InstrumentInfo[]? Instruments { get; private set; }

    public MarketDataService(WebSocketClient wsClient)
    {
        _wsClient = wsClient;
        _uiContext = SynchronizationContext.Current;
        _wsClient.MessageReceived += OnMessageReceived;
        _logger.Information("MarketDataService initialized");
    }

    private void OnMessageReceived(object? sender, WebSocketMessageEventArgs e)
    {
        if (e.Type.ToLowerInvariant() == "trade")
        {
            var trade = e.GetData<TradeUpdateMessage>();
            if (trade != null)
            {
                MessagesReceived++;
                
                var tradeMsg = new TradeMessage
                {
                    Symbol = trade.Symbol,
                    SymbolIndex = trade.SymbolIndex,
                    SequenceNumber = trade.Sequence,
                    TimestampNs = trade.TimestampNs,
                    Price = trade.Price,
                    Quantity = trade.Quantity,
                    BuyOrderId = trade.BuyOrderId,
                    SellOrderId = trade.SellOrderId
                };

                _tradeStream.OnNext(tradeMsg);
            }
        }
        else if (e.Type.ToLowerInvariant() == "instrumentlist")
        {
            _logger.Information("Received instrumentList message, attempting to parse...");
            
            var instruments = e.GetData<InstrumentInfoMessage[]>();
            if (instruments != null && instruments.Length > 0)
            {
                _logger.Information("Successfully parsed {Count} instruments", instruments.Length);
                
                var instrumentInfos = instruments.Select(i => new InstrumentInfo
                {
                    SymbolIndex = i.SymbolIndex,
                    Symbol = i.Symbol,
                    Channel = i.Channel,
                    TickSize = i.TickSize,
                    LotSize = i.LotSize,
                    ReferencePrice = i.ReferencePrice,
                    PreviousClose = i.PreviousClose,
                    IsFractional = i.IsFractional
                }).ToArray();
                
                Instruments = instrumentInfos;
                _logger.Information("Instruments stored, notifying subscribers...");
                
                // Notify on UI thread
                if (_uiContext != null)
                {
                    _uiContext.Post(_ => 
                    {
                        _logger.Information("Publishing to InstrumentListStream on UI thread");
                        _instrumentListStream.OnNext(instrumentInfos);
                    }, null);
                }
                else
                {
                    _logger.Information("Publishing to InstrumentListStream on worker thread");
                    _instrumentListStream.OnNext(instrumentInfos);
                }
            }
            else
            {
                _logger.Warning("Failed to parse instruments or received empty list");
            }
        }
    }

    public void Dispose()
    {
        _wsClient.MessageReceived -= OnMessageReceived;
        _tradeStream.OnCompleted();
        _tradeStream.Dispose();
    }
}
