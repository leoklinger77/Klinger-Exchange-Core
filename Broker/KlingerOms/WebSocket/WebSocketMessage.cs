using System.Text.Json.Serialization;
using KlingerBroker.Oms.Dtos;

namespace KlingerBroker.WebSocket;

/// <summary>
/// Base message envelope for WebSocket communication
/// </summary>
public sealed record WebSocketMessage
{
    [JsonPropertyName("type")]
    public required string Type { get; init; }
    
    [JsonPropertyName("data")]
    public required object Data { get; init; }
    
    [JsonPropertyName("timestamp")]
    public long Timestamp { get; init; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
}

// ==================== Client -> OMS ====================

/// <summary>
/// Client sends new order request
/// </summary>
public sealed record WsNewOrderRequest
{
    [JsonPropertyName("symbol")]
    public required string Symbol { get; init; }
    
    [JsonPropertyName("side")]
    public required string Side { get; init; } // "Buy" or "Sell"
    
    [JsonPropertyName("quantity")]
    public required int Quantity { get; init; }
    
    [JsonPropertyName("price")]
    public required decimal Price { get; init; }
}

/// <summary>
/// Client sends replace order request
/// </summary>
public sealed record WsReplaceOrderRequest
{
    [JsonPropertyName("origClOrdId")]
    public required string OrigClOrdId { get; init; }
    
    [JsonPropertyName("newQuantity")]
    public int? NewQuantity { get; init; }
    
    [JsonPropertyName("newPrice")]
    public decimal? NewPrice { get; init; }
}

/// <summary>
/// Client sends cancel order request
/// </summary>
public sealed record WsCancelOrderRequest
{
    [JsonPropertyName("origClOrdId")]
    public required string OrigClOrdId { get; init; }
}

// ==================== OMS -> Client ====================

/// <summary>
/// OMS sends order acknowledgement
/// </summary>
public sealed record WsOrderAck
{
    [JsonPropertyName("clOrdId")]
    public required string ClOrdId { get; init; }
    
    [JsonPropertyName("status")]
    public required string Status { get; init; } // "Accepted" or "Rejected"
    
    [JsonPropertyName("message")]
    public string? Message { get; init; }
}

/// <summary>
/// OMS sends execution report
/// </summary>
public sealed record WsExecutionReport
{
    [JsonPropertyName("clOrdId")]
    public required string ClOrdId { get; init; }
    
    [JsonPropertyName("orderId")]
    public string? OrderId { get; init; }
    
    [JsonPropertyName("symbol")]
    public string? Symbol { get; init; }
    
    [JsonPropertyName("execType")]
    public required string ExecType { get; init; }
    
    [JsonPropertyName("ordStatus")]
    public required string OrdStatus { get; init; }
    
    [JsonPropertyName("cumQty")]
    public int CumQty { get; init; }
    
    [JsonPropertyName("leavesQty")]
    public int LeavesQty { get; init; }
    
    [JsonPropertyName("lastPx")]
    public decimal? LastPx { get; init; }
    
    [JsonPropertyName("lastQty")]
    public int? LastQty { get; init; }
    
    [JsonPropertyName("text")]
    public string? Text { get; init; }
    
    public static WsExecutionReport FromDto(ExecutionReportEvent er) => new()
    {
        ClOrdId = er.ClOrdId,
        OrderId = er.OrderId,
        Symbol = er.Symbol,
        ExecType = er.ExecType,
        OrdStatus = er.OrdStatus,
        CumQty = er.CumQty,
        LeavesQty = er.LeavesQty,
        LastPx = er.LastPx,
        LastQty = er.LastQty,
        Text = er.Text
    };
}

/// <summary>
/// OMS sends market data trade update
/// </summary>
public sealed record WsTradeUpdate
{
    [JsonPropertyName("symbol")]
    public string? Symbol { get; init; }

    [JsonPropertyName("symbolIndex")]
    public short SymbolIndex { get; init; }
    
    [JsonPropertyName("sequence")]
    public uint Sequence { get; init; }
    
    [JsonPropertyName("timestampNs")]
    public long TimestampNs { get; init; }
    
    [JsonPropertyName("price")]
    public decimal Price { get; init; }
    
    [JsonPropertyName("quantity")]
    public long Quantity { get; init; }
    
    [JsonPropertyName("buyOrderId")]
    public long BuyOrderId { get; init; }
    
    [JsonPropertyName("sellOrderId")]
    public long SellOrderId { get; init; }
}

/// <summary>
/// OMS sends connection status updates
/// </summary>
public sealed record WsStatusUpdate
{
    [JsonPropertyName("component")]
    public required string Component { get; init; } // "FIX" or "WebSocket"
    
    [JsonPropertyName("isConnected")]
    public bool IsConnected { get; init; }
    
    [JsonPropertyName("details")]
    public string? Details { get; init; }
}

/// <summary>
/// OMS sends instrument list at connection
/// </summary>
public sealed record WsInstrumentInfo
{
    [JsonPropertyName("symbolIndex")]
    public short SymbolIndex { get; init; }
    
    [JsonPropertyName("symbol")]
    public required string Symbol { get; init; }
    
    [JsonPropertyName("channel")]
    public byte Channel { get; init; }
    
    [JsonPropertyName("tickSize")]
    public decimal TickSize { get; init; }
    
    [JsonPropertyName("lotSize")]
    public int LotSize { get; init; }
    
    [JsonPropertyName("referencePrice")]
    public decimal ReferencePrice { get; init; }
    
    [JsonPropertyName("previousClose")]
    public decimal PreviousClose { get; init; }
    
    [JsonPropertyName("isFractional")]
    public bool IsFractional { get; init; }
}
