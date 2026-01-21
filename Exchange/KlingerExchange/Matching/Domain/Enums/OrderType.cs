namespace KlingerExchange.Matching.Domain.Enums;

public enum OrderType : byte {
    Market = 1,
    Limit = 2,
    Stop = 3,
    StopLimit = 4
}
