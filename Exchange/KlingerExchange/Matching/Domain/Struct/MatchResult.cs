namespace KlingerExchange.Matching.Domain.Struct;

public readonly struct MatchResult {
    public long CounterOrderId { get; init; }
    public decimal Price { get; init; }
    public decimal Quantity { get; init; }
}
