using KlingerExchange.Matching.Domain;

namespace KlingerExchange.Matching.Validation;

public interface IOrderValidationRule
{
    ValidationResult Validate(OrderRequest request, InstrumentValidationMetadata? instrument);
}
