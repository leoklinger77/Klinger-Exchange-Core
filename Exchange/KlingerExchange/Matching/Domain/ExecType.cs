namespace KlingerExchange.Matching.Domain;

public enum ExecType : byte
{
    New = 0,
    PartialFill = 1,
    Fill = 2,
    Canceled = 4,
    Replaced = 5,
    Rejected = 8
}
