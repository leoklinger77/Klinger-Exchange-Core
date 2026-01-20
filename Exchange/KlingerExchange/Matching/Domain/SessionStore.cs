using KlingerExchange.Config;

namespace KlingerExchange.Matching.Domain;

/// <summary>
/// Store de sessões - mutável durante o pregão
/// </summary>
public static class SessionStore
{
    private static InstrumentSession[] _sessions = Array.Empty<InstrumentSession>();

    public static void Initialize(TradingSessionConfig config)
    {
        var maxIndex = config.Instruments.Max(i => i.SymbolIndex);
        _sessions = new InstrumentSession[maxIndex + 1];

        foreach (var dto in config.Instruments)
        {
            var status = dto.Status.ToLowerInvariant() switch
            {
                "preopen" => MarketStatus.PreOpen,
                "opening" => MarketStatus.Opening,
                "continuous" => MarketStatus.Continuous,
                "trading" => MarketStatus.Continuous,
                "closing" => MarketStatus.Closing,
                "closed" => MarketStatus.Closed,
                "halt" => MarketStatus.Halt,
                "suspended" => MarketStatus.Suspended,
                _ => MarketStatus.Continuous
            };

            _sessions[dto.SymbolIndex] = new InstrumentSession
            {
                SymbolIndex = dto.SymbolIndex,
                Status = status,
                ReferencePriceFixed = (long)(dto.ReferencePrice * 100_000m),
                PreviousCloseFixed = (long)(dto.PreviousClose * 100_000m),
                PreviousHighFixed = (long)(dto.PreviousHigh * 100_000m),
                PreviousLowFixed = (long)(dto.PreviousLow * 100_000m),
                UpperLimitFixed = (long)(dto.UpperLimit * 100_000m),
                LowerLimitFixed = (long)(dto.LowerLimit * 100_000m),
                Volume = 0,
                Trades = 0
            };
        }
    }

    /// <summary>
    /// Acesso O(1) à sessão - hot path
    /// </summary>
    public static ref InstrumentSession Get(short symbolIndex)
    {
        if (symbolIndex < 0 || symbolIndex >= _sessions.Length)
            throw new ArgumentOutOfRangeException(nameof(symbolIndex));

        return ref _sessions[symbolIndex];
    }

    public static decimal GetPrice(long priceFixed)
    {
        return (decimal)priceFixed / 100_000m;
    }
}
