using KlingerExchange.Matching.Domain;
using QuickFix.Fields;

namespace KlingerExchange.Matching.Validation.Rules;

/// <summary>
/// Valida se o preço está dentro das bandas permitidas
/// Protege contra fat finger e manipulação
/// </summary>
public sealed class PriceBandValidator : IOrderValidationRule
{
    public ValidationResult Validate(OrderRequest request, InstrumentValidationMetadata? instrument)
    {
        if (instrument == null)
            return ValidationResult.Valid(); // Já validado em InstrumentStatusValidator

        // Market orders não precisam validar preço
        if (request.OrderType == OrderType.Market)
            return ValidationResult.Valid();

        // Verifica se preço está dentro das bandas definidas pela Exchange
        if (instrument.LowerLimit > 0 && instrument.UpperLimit > 0)
        {
            if (request.Price < instrument.LowerLimit || request.Price > instrument.UpperLimit)
            {
                return ValidationResult.Rejected(
                    $"Price {request.Price:F2} outside allowed band [{instrument.LowerLimit:F2}, {instrument.UpperLimit:F2}]",
                    BusinessRejectCode.PriceOutOfBand
                );
            }
        }

        // Valida tick size (incremento mínimo de preço)
        if (instrument.TickSize > 0)
        {
            var remainder = request.Price % instrument.TickSize;
            if (remainder != 0)
            {
                return ValidationResult.Rejected(
                    $"Price {request.Price:F2} not multiple of tick size {instrument.TickSize:F2}",
                    BusinessRejectCode.Other
                );
            }
        }

        return ValidationResult.Valid();
    }
}
