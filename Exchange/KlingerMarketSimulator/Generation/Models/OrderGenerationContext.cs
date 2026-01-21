namespace KlingerSimulator.Generation.Models;

/// <summary>
/// Strategy context - encapsulates all dependencies needed for order generation.
/// Immutable by design.
/// </summary>
public sealed record OrderGenerationContext
{
    public required string[] Symbols { get; init; }
    public required Func<string, decimal> GetLastPrice { get; init; }
    public required Func<string, InstrumentSpec> GetInstrumentSpec { get; init; }
    public required long OrderSequence { get; init; }
}
