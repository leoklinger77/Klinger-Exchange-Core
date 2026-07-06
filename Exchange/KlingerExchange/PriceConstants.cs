namespace KlingerExchange;

/// <summary>
/// Centralized fixed-point price multiplier constants.
/// Two intentional precision levels:
/// - OrderBook: 8 decimal places for high-precision matching
/// - Wire: 5 decimal places for market data and session data (sufficient for all Brazilian instruments)
/// </summary>
public static class PriceConstants {
    /// <summary>
    /// High-precision multiplier for internal order book matching.
    /// 8 decimal places (e.g. 28.50 → 2_850_000_000).
    /// </summary>
    public const long OrderBookMultiplier = 100_000_000L;
    public const decimal OrderBookMultiplierDecimal = 100_000_000m;

    /// <summary>
    /// Wire protocol multiplier for market data, session data, and instrument metadata.
    /// 5 decimal places (e.g. 28.50 → 2_850_000).
    /// Used by: TradeMessage, SessionStore, InstrumentMetadataStore.
    /// Must match consumers: KlingerOms, KlingerMarketSimulator.
    /// </summary>
    public const long WireMultiplier = 100_000L;
    public const decimal WireMultiplierDecimal = 100_000m;
}
