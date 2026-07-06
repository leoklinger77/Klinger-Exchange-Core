using KlingerExchange.Config;
using KlingerExchange.Matching.Domain.Enums;
using KlingerExchange.Matching.Domain.Struct;

namespace KlingerExchange.Matching.Domain;

/// <summary>
/// Sessions store - changeable during trading hours.
/// </summary>
public static class SessionStore {
    private static InstrumentSession[] _sessions = Array.Empty<InstrumentSession>();

    public static void Initialize(TradingSessionConfig config) {
        var maxIndex = config.Instruments.Max(i => i.SymbolIndex);
        _sessions = new InstrumentSession[maxIndex + 1];

        foreach (var dto in config.Instruments) {
            var status = dto.Status.ToLowerInvariant() switch {
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

            _sessions[dto.SymbolIndex] = new InstrumentSession {
                SymbolIndex = dto.SymbolIndex,
                Status = status,
                ReferencePriceFixed = (long)(dto.ReferencePrice * PriceConstants.WireMultiplierDecimal),
                PreviousCloseFixed = (long)(dto.PreviousClose * PriceConstants.WireMultiplierDecimal),
                PreviousHighFixed = (long)(dto.PreviousHigh * PriceConstants.WireMultiplierDecimal),
                PreviousLowFixed = (long)(dto.PreviousLow * PriceConstants.WireMultiplierDecimal),
                UpperLimitFixed = (long)(dto.UpperLimit * PriceConstants.WireMultiplierDecimal),
                LowerLimitFixed = (long)(dto.LowerLimit * PriceConstants.WireMultiplierDecimal),
                Volume = 0,
                Trades = 0
            };
        }
    }

    /// <summary>
    /// O(1) access to the session - hot path
    /// </summary>
    public static ref InstrumentSession Get(short symbolIndex) {
        if (symbolIndex < 0 || symbolIndex >= _sessions.Length)
            throw new ArgumentOutOfRangeException(nameof(symbolIndex));

        return ref _sessions[symbolIndex];
    }

    public static decimal GetPrice(long priceFixed) {
        return (decimal)priceFixed / PriceConstants.WireMultiplierDecimal;
    }
}
