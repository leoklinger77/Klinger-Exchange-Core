using System.Runtime.InteropServices;
using System.Text;

namespace KlingerExchange.MarketData.StructModels;

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
