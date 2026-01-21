namespace KlingerSimulator.Generation.Models;

public sealed record InstrumentSpec(string Symbol, int LotSize, decimal ReferencePrice, decimal TickSize);
