using System.Runtime.InteropServices;

namespace KlingerExchange.MarketData.Core;

/// <summary>
/// Binary protocol message for ultra-low latency market data distribution.
/// Fixed 64-byte layout for zero serialization overhead.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public unsafe struct TradeMessage {
    public byte MessageType;
    public short SymbolIndex;
    public uint SequenceNumber;
    public long TimestampNs;
    public long PriceFixed;
    public long Quantity;
    public long BuyOrderId;
    public long SellOrderId;
    private fixed byte _padding[17];
    public const int MessageSize = 64;

    public const byte MSG_TYPE_TRADE = 1;
    public const byte MSG_TYPE_QUOTE = 2;
    public const byte MSG_TYPE_BOOK_UPDATE = 3;

    public static long PriceToFixed(decimal price) => (long)(price * PriceConstants.WireMultiplierDecimal);

    public static decimal FixedToPrice(long priceFixed) => (decimal)priceFixed / PriceConstants.WireMultiplierDecimal;
}
