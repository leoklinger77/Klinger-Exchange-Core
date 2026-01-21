using System.Runtime.InteropServices;

namespace KlingerExchange.MarketData.StructModels;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public unsafe struct InstrumentListMessage
{
    public byte MessageType; // 10 = InstrumentList
    public short Count;
    public uint SequenceNumber;
    public long TimestampNs;
    private fixed byte _reserved[49];
    
    public const byte MSG_TYPE_INSTRUMENT_LIST = 10;
    public const int HeaderSize = 64;
}
