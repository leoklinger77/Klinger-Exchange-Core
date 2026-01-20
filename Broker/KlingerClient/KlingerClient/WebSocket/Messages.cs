using System.Text.Json.Serialization;

namespace KlingerClient.WebSocket;

// ==================== Messages from Client to OMS ====================

public sealed record NewOrderMessage
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

public sealed record ReplaceOrderMessage
{
    [JsonPropertyName("origClOrdId")]
    public required string OrigClOrdId { get; init; }
    
    [JsonPropertyName("newQuantity")]
    public int? NewQuantity { get; init; }
    
    [JsonPropertyName("newPrice")]
    public decimal? NewPrice { get; init; }
}

public sealed record CancelOrderMessage
{
    [JsonPropertyName("origClOrdId")]
    public required string OrigClOrdId { get; init; }
}

// ==================== Messages from OMS to Client ====================

public sealed record OrderAckMessage
{
    [JsonPropertyName("clOrdId")]
    public required string ClOrdId { get; init; }
    
    [JsonPropertyName("status")]
    public required string Status { get; init; }
    
    [JsonPropertyName("message")]
    public string? Message { get; init; }
}

public sealed record ExecutionReportMessage
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
}

public sealed record TradeUpdateMessage
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

public sealed record StatusUpdateMessage
{
    [JsonPropertyName("component")]
    public required string Component { get; init; }
    
    [JsonPropertyName("isConnected")]
    public bool IsConnected { get; init; }
    
    [JsonPropertyName("details")]
    public string? Details { get; init; }
}

public sealed record ErrorMessage
{
    [JsonPropertyName("message")]
    public required string Message { get; init; }
}

// ==================== Instrument List ====================

public sealed record InstrumentInfoMessage
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
