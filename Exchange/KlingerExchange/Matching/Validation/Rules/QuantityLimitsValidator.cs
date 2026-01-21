using KlingerExchange.Matching.Domain;

namespace KlingerExchange.Matching.Validation.Rules;

public sealed class QuantityLimitsValidator : IOrderValidationRule {
    public ValidationResult Validate(OrderRequest request, InstrumentValidationMetadata? instrument) {
        if (instrument == null)
            return ValidationResult.Valid();

        // Minimum quantity
        if (request.Quantity < instrument.MinQuantity) {
            return ValidationResult.Rejected(
                $"Quantity {request.Quantity} below minimum {instrument.MinQuantity}",
                BusinessRejectCode.QuantityBelowMinimum
            );
        }

        // Maximum quantity
        if (request.Quantity > instrument.MaxQuantity) {
            return ValidationResult.Rejected(
                $"Quantity {request.Quantity} exceeds maximum {instrument.MaxQuantity}",
                BusinessRejectCode.QuantityAboveMaximum
            );
        }

        // Minimum lot size (round lot)
        if (instrument.LotSize > 0) {
            var remainder = request.Quantity % instrument.LotSize;
            if (remainder != 0) {
                return ValidationResult.Rejected(
                    $"Quantity {request.Quantity} not multiple of lot size {instrument.LotSize}",
                    BusinessRejectCode.InvalidLotSize
                );
            }
        }

        return ValidationResult.Valid();
    }
}
