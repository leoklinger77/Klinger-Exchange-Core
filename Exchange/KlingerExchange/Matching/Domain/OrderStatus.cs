namespace KlingerExchange.Matching.Domain;

public enum OrderStatus : byte
{
    New = 0,
    PartiallyFilled = 1,
    Filled = 2,
    Canceled = 4,
    Rejected = 8
}
