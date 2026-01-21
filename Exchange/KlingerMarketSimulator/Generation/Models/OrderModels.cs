namespace KlingerSimulator.Generation.Models;

public sealed record OrderRequest
{
    public required string Symbol { get; init; }
    public required char Side { get; init; }
    public required decimal Price { get; init; }
    public required decimal Quantity { get; init; }
    public required string ClOrdId { get; init; }
}
