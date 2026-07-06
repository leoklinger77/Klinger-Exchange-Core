using System.Runtime.InteropServices;
using System.Text;

namespace KlingerBroker.MarketData;

/// <summary>
/// Message received from Exchange with instrument list
/// </summary>
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

/// <summary>
/// Individual instrument info
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public unsafe struct InstrumentInfo
{
    public short SymbolIndex;
    public byte Channel;
    public byte Flags;
    public long TickSizeFixed;
    public int LotSize;
    public long ReferencePriceFixed;
    public long PreviousCloseFixed;
    public fixed byte _symbolBytes[32];
    
    public const int Size = 64;
    
    public string GetSymbol()
    {
        fixed (byte* ptr = _symbolBytes)
        {
            int len = 0;
            while (len < 32 && ptr[len] != 0) len++;
            return Encoding.ASCII.GetString(ptr, len);
        }
    }
    
    /// <summary>Wire protocol multiplier (must match Exchange PriceConstants.WireMultiplier)</summary>
    private const decimal WireMultiplier = 100_000m;
    
    public decimal GetPrice(long priceFixed) => (decimal)priceFixed / WireMultiplier;
    public decimal TickSize => (decimal)TickSizeFixed / WireMultiplier;
    public decimal ReferencePrice => GetPrice(ReferencePriceFixed);
    public decimal PreviousClose => GetPrice(PreviousCloseFixed);
    public bool IsFractional => (Flags & 1) != 0;
}

/// <summary>
/// Deserializer for instrument list
/// </summary>
public static class InstrumentListDeserializer
{
    public static (InstrumentListMessage Header, InstrumentInfo[] Instruments) Deserialize(byte[] buffer)
    {
        if (buffer.Length < InstrumentListMessage.HeaderSize)
            throw new ArgumentException("Buffer too small");
            
        InstrumentListMessage header;
        unsafe
        {
            fixed (byte* ptr = buffer)
            {
                header = *(InstrumentListMessage*)ptr;
            }
        }
        
        if (header.MessageType != InstrumentListMessage.MSG_TYPE_INSTRUMENT_LIST)
            throw new ArgumentException("Invalid message type");
            
        var expectedSize = InstrumentListMessage.HeaderSize + (header.Count * InstrumentInfo.Size);
        if (buffer.Length < expectedSize)
            throw new ArgumentException("Buffer too small for instrument count");
            
        var instruments = new InstrumentInfo[header.Count];
        unsafe
        {
            fixed (byte* ptr = buffer)
            {
                var dataPtr = ptr + InstrumentListMessage.HeaderSize;
                for (int i = 0; i < header.Count; i++)
                {
                    var offset = i * InstrumentInfo.Size;
                    instruments[i] = *(InstrumentInfo*)(dataPtr + offset);
                }
            }
        }
        
        return (header, instruments);
    }
}
