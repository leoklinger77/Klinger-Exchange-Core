using KlingerExchange.Matching.Domain.Enums;

namespace KlingerExchange.Matching.Domain.Struct;

public struct InstrumentSession {
    public short SymbolIndex;
    public MarketStatus Status;
    public long ReferencePriceFixed;
    public long PreviousCloseFixed;
    public long PreviousHighFixed;
    public long PreviousLowFixed;
    public long UpperLimitFixed;
    public long LowerLimitFixed;
    public long Volume;
    public int Trades;
}
