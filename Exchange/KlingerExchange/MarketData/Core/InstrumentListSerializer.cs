using KlingerExchange.MarketData.StructModels;

namespace KlingerExchange.MarketData.Core;

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
