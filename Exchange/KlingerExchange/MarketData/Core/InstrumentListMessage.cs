using System.Runtime.InteropServices;
using System.Text;

namespace KlingerExchange.MarketData.Core;

/// <summary>
/// Message sent at market open with instrument list
/// Variable size but sent only once at startup
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
/// Individual instrument info (64 bytes fixed)
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public unsafe struct InstrumentInfo
{
    public short SymbolIndex;
    public byte Channel;
    public byte Flags; // bit 0: isFractional
    public long TickSizeFixed;
    public int LotSize;
    public long ReferencePriceFixed;
    public long PreviousCloseFixed;
    private fixed byte _symbolBytes[32]; // Symbol as ASCII
    
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
    
    public void SetSymbol(string symbol)
    {
        var bytes = Encoding.ASCII.GetBytes(symbol);
        var len = Math.Min(bytes.Length, 31);
        
        fixed (byte* ptr = _symbolBytes)
        {
            for (int i = 0; i < 32; i++)
                ptr[i] = 0;
                
            for (int i = 0; i < len; i++)
                ptr[i] = bytes[i];
        }
    }
}

/// <summary>
/// Helper para serializar lista de instrumentos
/// </summary>
public static class InstrumentListSerializer
{
    public static byte[] Serialize(InstrumentInfo[] instruments, uint sequenceNumber)
    {
        var header = new InstrumentListMessage
        {
            MessageType = InstrumentListMessage.MSG_TYPE_INSTRUMENT_LIST,
            Count = (short)instruments.Length,
            SequenceNumber = sequenceNumber,
            TimestampNs = DateTimeOffset.UtcNow.Ticks * 100
        };
        
        var totalSize = InstrumentListMessage.HeaderSize + (instruments.Length * InstrumentInfo.Size);
        var buffer = new byte[totalSize];
        
        // Copy header
        unsafe
        {
            fixed (byte* ptr = buffer)
            {
                *(InstrumentListMessage*)ptr = header;
                
                var dataPtr = ptr + InstrumentListMessage.HeaderSize;
                for (int i = 0; i < instruments.Length; i++)
                {
                    var offset = i * InstrumentInfo.Size;
                    *(InstrumentInfo*)(dataPtr + offset) = instruments[i];
                }
            }
        }
        
        return buffer;
    }
    
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
