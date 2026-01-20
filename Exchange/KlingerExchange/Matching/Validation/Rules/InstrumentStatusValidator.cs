using KlingerExchange.Matching.Domain;
using QuickFix.Fields;

namespace KlingerExchange.Matching.Validation.Rules;

/// <summary>
/// Valida se o instrumento está ativo para trading
/// Verifica status: Active, Suspended, Halted
/// </summary>
public sealed class InstrumentStatusValidator : IOrderValidationRule
{
    public ValidationResult Validate(OrderRequest request, InstrumentValidationMetadata? instrument)
    {
        if (instrument == null)
        {
            return ValidationResult.Rejected(
                $"Instrument not found: {request.Symbol}",
                BusinessRejectCode.InstrumentNotFound
            );
        }

        if (instrument.Status != InstrumentStatus.Active)
        {
            return ValidationResult.Rejected(
                $"Instrument {request.Symbol} is {instrument.Status}. Trading not allowed.",
                BusinessRejectCode.InstrumentSuspended
            );
        }

        return ValidationResult.Valid();
    }
}
