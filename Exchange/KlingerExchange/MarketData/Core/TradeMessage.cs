using System.Runtime.InteropServices;

namespace KlingerExchange.MarketData.Core;

/// <summary>
/// Binary protocol message for ultra-low latency market data distribution.
/// Fixed 64-byte layout for zero serialization overhead.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public unsafe struct TradeMessage
{
    /// <summary>Message type: 1=Trade, 2=Quote, 3=BookUpdate</summary>
    public byte MessageType;
    
    /// <summary>Symbol index (2 bytes vs 5+ bytes for string)</summary>
    public short SymbolIndex;
    
    /// <summary>Sequence number for gap detection</summary>
    public uint SequenceNumber;
    
    /// <summary>Timestamp in nanoseconds since epoch</summary>
    public long TimestampNs;
    
    /// <summary>Price as fixed-point: actual_price * 100000 (5 decimal places)</summary>
    public long PriceFixed;
    
    /// <summary>Quantity in shares</summary>
    public long Quantity;
    
    /// <summary>Buy side order ID</summary>
    public long BuyOrderId;
    
    /// <summary>Sell side order ID</summary>
    public long SellOrderId;
    
    /// <summary>Padding to reach exactly 64 bytes (cache line aligned)</summary>
    private fixed byte _padding[17];
    
    public const int MessageSize = 64;
    
    /// <summary>Message type constants</summary>
    public const byte MSG_TYPE_TRADE = 1;
    public const byte MSG_TYPE_QUOTE = 2;
    public const byte MSG_TYPE_BOOK_UPDATE = 3;
    
    /// <summary>Convert decimal price to fixed-point representation</summary>
    public static long PriceToFixed(decimal price) => (long)(price * 100000m);
    
    /// <summary>Convert fixed-point to decimal price</summary>
    public static decimal FixedToPrice(long priceFixed) => (decimal)priceFixed / 100000m;
}
