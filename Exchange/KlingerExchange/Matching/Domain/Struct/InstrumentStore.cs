using System.Runtime.InteropServices;

namespace KlingerExchange.Matching.Domain.Struct;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct InstrumentMetadata {
    public short SymbolIndex;
    public byte Channel;
    public byte Flags; // bit 0: isFractional, bits 1-7: reserved
    public long TickSizeFixed; // 0.01 = 1000 (5 decimais fixos)
    public int LotSize;

    public bool IsFractional => (Flags & 1) != 0;
}
