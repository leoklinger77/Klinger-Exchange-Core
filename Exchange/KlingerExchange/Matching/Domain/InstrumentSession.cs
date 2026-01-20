namespace KlingerExchange.Matching.Domain;

/// <summary>
/// Estado da sessão do instrumento - mutável
/// </summary>
public struct InstrumentSession
{
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
