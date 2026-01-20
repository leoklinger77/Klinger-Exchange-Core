namespace KlingerExchange.Matching.Domain;

public enum MarketStatus : byte {
    PreOpen = 0,
    Opening = 1,
    Continuous = 2,
    Closing = 3,
    Closed = 4,
    Halt = 5,
    Suspended = 6
}
