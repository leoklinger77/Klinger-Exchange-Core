using KlingerExchange.Matching.Domain;
using QuickFix.Fields;

namespace KlingerExchange.Matching.Validation.Rules;

/// <summary>
/// Valida se o mercado está aberto para trading
/// Por enquanto sempre aceita (mercado 24x7)
/// </summary>
public sealed class TradingSessionValidator : IOrderValidationRule
{
    public ValidationResult Validate(OrderRequest request, InstrumentValidationMetadata? instrument)
    {
        // Por enquanto, mercado sempre aberto
        // TODO: Implementar regras de sessão (PreOpen, Open, Closed, etc)
        return ValidationResult.Valid();
    }
}
