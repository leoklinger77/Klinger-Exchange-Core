using KlingerExchange.Matching.Domain;

namespace KlingerExchange.Matching.Validation.Rules;

public sealed class TradingSessionValidator : IOrderValidationRule {
    public ValidationResult Validate(OrderRequest request, InstrumentValidationMetadata? instrument) {
        // TODO: Implementar regras de sessão (PreOpen, Open, Closed, etc)
        return ValidationResult.Valid();
    }
}
