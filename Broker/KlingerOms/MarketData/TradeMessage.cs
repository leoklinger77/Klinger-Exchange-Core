using System.Runtime.InteropServices;

namespace KlingerBroker.MarketData;

/// <summary>
/// Market data message from UDP multicast - must match server TradeMessage exactly.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public unsafe struct TradeMessage
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
    
    public const int MessageSize = 64;
    public const byte MSG_TYPE_TRADE = 1;
    
    /// <summary>Wire protocol multiplier (must match Exchange PriceConstants.WireMultiplier)</summary>
    private const decimal WireMultiplier = 100_000m;

    public decimal Price => (decimal)PriceFixed / WireMultiplier;
}
