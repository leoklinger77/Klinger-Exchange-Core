using KlingerExchange.Matching.Domain;

namespace KlingerExchange.Matching.Validation;

/// <summary>
/// Interface para regra de validação de ordem
/// Pattern: Chain of Responsibility
/// </summary>
public interface IOrderValidationRule
{
    /// <summary>
    /// Valida uma ordem
    /// </summary>
    ValidationResult Validate(OrderRequest request, InstrumentValidationMetadata? instrument);
}
