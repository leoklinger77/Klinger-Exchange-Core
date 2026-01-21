using KlingerExchange.Matching.Domain;
using KlingerExchange.Matching.Domain.Enums;

namespace KlingerExchange.Matching.Validation.Rules;

public sealed class PriceBandValidator : IOrderValidationRule {
    public ValidationResult Validate(OrderRequest request, InstrumentValidationMetadata? instrument) {
        if (instrument == null)
            return ValidationResult.Valid(); // Already validated in InstrumentStatusValidator

        // Market orders do not need to validate the price.
        if (request.OrderType == OrderType.Market)
            return ValidationResult.Valid();

        // Check if the price is within the bands defined by the Exchange.
        if (instrument.LowerLimit > 0 && instrument.UpperLimit > 0) {
            if (request.Price < instrument.LowerLimit || request.Price > instrument.UpperLimit) {
                return ValidationResult.Rejected($"Price {request.Price:F2} outside allowed band [{instrument.LowerLimit:F2}, {instrument.UpperLimit:F2}]",
                    BusinessRejectCode.PriceOutOfBand
                );
            }
        }

        // Validate tick size (minimum price increment)
        if (instrument.TickSize > 0) {
            var remainder = request.Price % instrument.TickSize;
            if (remainder != 0) {
                return ValidationResult.Rejected($"Price {request.Price:F2} not multiple of tick size {instrument.TickSize:F2}",
                    BusinessRejectCode.Other
                );
            }
        }

        return ValidationResult.Valid();
    }
}
