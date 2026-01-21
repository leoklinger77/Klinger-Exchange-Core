using KlingerExchange.Matching.Domain;

namespace KlingerExchange.Matching.Validation.Rules;

public sealed class OrderTypeValidator : IOrderValidationRule {
    public ValidationResult Validate(OrderRequest request, InstrumentValidationMetadata? instrument) {
        if (instrument == null)
            return ValidationResult.Valid();

        // Checks if the order type is allowed
        if (!instrument.AllowedOrderTypes.Contains(request.OrderType)) {
            return ValidationResult.Rejected($"Order type {request.OrderType} not allowed for {request.Symbol}",
                BusinessRejectCode.OrderTypeNotAllowed
            );
        }

        return ValidationResult.Valid();
    }
}
