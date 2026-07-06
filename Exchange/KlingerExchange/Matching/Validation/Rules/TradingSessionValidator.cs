using KlingerExchange.Matching.Domain;
using KlingerExchange.Matching.Domain.Enums;
using KlingerExchange.Matching.Engine.Instrument;

namespace KlingerExchange.Matching.Validation.Rules;

/// <summary>
/// Validates the current trading session phase for the instrument.
/// Rejects orders when the market is not in a tradeable phase.
/// </summary>
public sealed class TradingSessionValidator : IOrderValidationRule {
    public ValidationResult Validate(OrderRequest request, InstrumentValidationMetadata? instrument) {
        if (instrument == null)
            return ValidationResult.Valid(); // Already validated in InstrumentStatusValidator

        if (!InstrumentMetadataStore.TryGetSymbolIndex(request.Symbol, out var symbolIndex))
            return ValidationResult.Valid(); // Already validated in InstrumentStatusValidator

        ref var session = ref SessionStore.Get(symbolIndex);

        return session.Status switch {
            MarketStatus.Continuous => ValidationResult.Valid(),
            MarketStatus.Opening => ValidationResult.Valid(),
            MarketStatus.Closing => ValidationResult.Valid(),
            MarketStatus.PreOpen => ValidationResult.Rejected(
                $"Instrument {request.Symbol} in pre-open phase. New orders not accepted.",
                BusinessRejectCode.MarketClosed),
            MarketStatus.Closed => ValidationResult.Rejected(
                $"Market closed for {request.Symbol}.",
                BusinessRejectCode.MarketClosed),
            MarketStatus.Halt => ValidationResult.Rejected(
                $"Instrument {request.Symbol} halted.",
                BusinessRejectCode.InstrumentSuspended),
            MarketStatus.Suspended => ValidationResult.Rejected(
                $"Instrument {request.Symbol} suspended.",
                BusinessRejectCode.InstrumentSuspended),
            _ => ValidationResult.Valid()
        };
    }
}
