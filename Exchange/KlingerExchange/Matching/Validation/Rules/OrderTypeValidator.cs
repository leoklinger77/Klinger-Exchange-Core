using KlingerExchange.Matching.Domain;
using QuickFix.Fields;

namespace KlingerExchange.Matching.Validation.Rules;

/// <summary>
/// Valida se o tipo de ordem é permitido para o instrumento
/// Alguns instrumentos só aceitam Limit orders
/// </summary>
public sealed class OrderTypeValidator : IOrderValidationRule
{
    public ValidationResult Validate(OrderRequest request, InstrumentValidationMetadata? instrument)
    {
        if (instrument == null)
            return ValidationResult.Valid();

        // Verifica se o tipo de ordem é permitido
        if (!instrument.AllowedOrderTypes.Contains(request.OrderType))
        {
            return ValidationResult.Rejected(
                $"Order type {request.OrderType} not allowed for {request.Symbol}",
                BusinessRejectCode.OrderTypeNotAllowed
            );
        }

        return ValidationResult.Valid();
    }
}
