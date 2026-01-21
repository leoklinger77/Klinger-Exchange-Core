using KlingerExchange.Matching.Domain.Enums;

namespace KlingerExchange.Matching.Domain;

public sealed class InstrumentValidationMetadata {
    public string Symbol { get; init; } = string.Empty;
    public InstrumentStatus Status { get; init; }

    // Price bands
    public decimal ReferencePrice { get; init; }
    public decimal UpperLimit { get; init; }
    public decimal LowerLimit { get; init; }
    public decimal TickSize { get; init; }

    // Quantity limits
    public decimal MinQuantity { get; init; }
    public decimal MaxQuantity { get; init; }
    public decimal LotSize { get; init; } // Round lot

    // Allowed order types
    public HashSet<OrderType> AllowedOrderTypes { get; init; } = new();
}
