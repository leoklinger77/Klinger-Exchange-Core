using KlingerExchange.Matching.Domain;
using KlingerExchange.Matching.Domain.Enums;
using KlingerExchange.Matching.Validation;
using KlingerExchange.Matching.Validation.Rules;

namespace KlingerExchange.Tests;

public class ValidationRulesTests {
    private static InstrumentValidationMetadata MakeInstrument(
        InstrumentStatus status = InstrumentStatus.Active,
        decimal lower = 20m,
        decimal upper = 30m,
        decimal tickSize = 0.01m,
        decimal minQty = 1m,
        decimal maxQty = 100_000m,
        decimal lotSize = 100m) =>
        new() {
            Symbol = "PETR4",
            Status = status,
            ReferencePrice = 25.00m,
            LowerLimit = lower,
            UpperLimit = upper,
            TickSize = tickSize,
            MinQuantity = minQty,
            MaxQuantity = maxQty,
            LotSize = lotSize,
            AllowedOrderTypes = new HashSet<OrderType> { OrderType.Limit, OrderType.Market }
        };

    private static OrderRequest MakeRequest(
        decimal price = 25.00m,
        decimal quantity = 100m,
        OrderType type = OrderType.Limit) =>
        new("CLO001", "PETR4", Side.Buy, price, quantity, type);

    // ── InstrumentStatusValidator ───────────────────────────────────
    public class InstrumentStatusTests {
        private readonly InstrumentStatusValidator _rule = new();

        [Fact]
        public void NullInstrument_Rejects() {
            var result = _rule.Validate(MakeRequest(), null);
            Assert.Equal(ValidationStatus.Rejected, result.Status);
            Assert.Equal(BusinessRejectCode.InstrumentNotFound, result.RejectCode);
        }

        [Fact]
        public void ActiveInstrument_Valid() {
            var result = _rule.Validate(MakeRequest(), MakeInstrument());
            Assert.Equal(ValidationStatus.Valid, result.Status);
        }

        [Theory]
        [InlineData(InstrumentStatus.Suspended)]
        [InlineData(InstrumentStatus.Halted)]
        [InlineData(InstrumentStatus.PreOpen)]
        public void NonActiveInstrument_Rejects(InstrumentStatus status) {
            var result = _rule.Validate(MakeRequest(), MakeInstrument(status: status));
            Assert.Equal(ValidationStatus.Rejected, result.Status);
            Assert.Equal(BusinessRejectCode.InstrumentSuspended, result.RejectCode);
        }
    }

    // ── OrderTypeValidator ──────────────────────────────────────────
    public class OrderTypeTests {
        private readonly OrderTypeValidator _rule = new();

        [Fact]
        public void AllowedOrderType_Valid() {
            var result = _rule.Validate(MakeRequest(type: OrderType.Limit), MakeInstrument());
            Assert.Equal(ValidationStatus.Valid, result.Status);
        }

        [Fact]
        public void DisallowedOrderType_Rejects() {
            var result = _rule.Validate(MakeRequest(type: OrderType.StopLimit), MakeInstrument());
            Assert.Equal(ValidationStatus.Rejected, result.Status);
            Assert.Equal(BusinessRejectCode.OrderTypeNotAllowed, result.RejectCode);
        }

        [Fact]
        public void NullInstrument_PassesThrough() {
            var result = _rule.Validate(MakeRequest(), null);
            Assert.Equal(ValidationStatus.Valid, result.Status);
        }
    }

    // ── PriceBandValidator ──────────────────────────────────────────
    public class PriceBandTests {
        private readonly PriceBandValidator _rule = new();

        [Fact]
        public void PriceWithinBand_Valid() {
            var result = _rule.Validate(MakeRequest(price: 25.00m), MakeInstrument());
            Assert.Equal(ValidationStatus.Valid, result.Status);
        }

        [Fact]
        public void PriceBelowLower_Rejects() {
            var result = _rule.Validate(MakeRequest(price: 19.99m), MakeInstrument());
            Assert.Equal(ValidationStatus.Rejected, result.Status);
            Assert.Equal(BusinessRejectCode.PriceOutOfBand, result.RejectCode);
        }

        [Fact]
        public void PriceAboveUpper_Rejects() {
            var result = _rule.Validate(MakeRequest(price: 30.01m), MakeInstrument());
            Assert.Equal(ValidationStatus.Rejected, result.Status);
            Assert.Equal(BusinessRejectCode.PriceOutOfBand, result.RejectCode);
        }

        [Fact]
        public void PriceAtExactBounds_Valid() {
            Assert.Equal(ValidationStatus.Valid,
                _rule.Validate(MakeRequest(price: 20.00m), MakeInstrument()).Status);
            Assert.Equal(ValidationStatus.Valid,
                _rule.Validate(MakeRequest(price: 30.00m), MakeInstrument()).Status);
        }

        [Fact]
        public void MarketOrder_SkipsPriceBandCheck() {
            var result = _rule.Validate(MakeRequest(price: 999m, type: OrderType.Market), MakeInstrument());
            Assert.Equal(ValidationStatus.Valid, result.Status);
        }

        [Fact]
        public void PriceNotMultipleOfTickSize_Rejects() {
            var result = _rule.Validate(MakeRequest(price: 25.005m), MakeInstrument(tickSize: 0.01m));
            Assert.Equal(ValidationStatus.Rejected, result.Status);
        }

        [Fact]
        public void PriceMultipleOfTickSize_Valid() {
            var result = _rule.Validate(MakeRequest(price: 25.01m), MakeInstrument(tickSize: 0.01m));
            Assert.Equal(ValidationStatus.Valid, result.Status);
        }
    }

    // ── QuantityLimitsValidator ─────────────────────────────────────
    public class QuantityLimitsTests {
        private readonly QuantityLimitsValidator _rule = new();

        [Fact]
        public void QuantityWithinLimits_Valid() {
            var result = _rule.Validate(MakeRequest(quantity: 100m), MakeInstrument());
            Assert.Equal(ValidationStatus.Valid, result.Status);
        }

        [Fact]
        public void QuantityBelowMinimum_Rejects() {
            var result = _rule.Validate(MakeRequest(quantity: 0m), MakeInstrument(minQty: 1));
            Assert.Equal(ValidationStatus.Rejected, result.Status);
            Assert.Equal(BusinessRejectCode.QuantityBelowMinimum, result.RejectCode);
        }

        [Fact]
        public void QuantityAboveMaximum_Rejects() {
            var result = _rule.Validate(MakeRequest(quantity: 200_000m), MakeInstrument(maxQty: 100_000));
            Assert.Equal(ValidationStatus.Rejected, result.Status);
            Assert.Equal(BusinessRejectCode.QuantityAboveMaximum, result.RejectCode);
        }

        [Fact]
        public void QuantityNotMultipleOfLot_Rejects() {
            var result = _rule.Validate(MakeRequest(quantity: 150m), MakeInstrument(lotSize: 100));
            Assert.Equal(ValidationStatus.Rejected, result.Status);
            Assert.Equal(BusinessRejectCode.InvalidLotSize, result.RejectCode);
        }

        [Fact]
        public void QuantityMultipleOfLot_Valid() {
            var result = _rule.Validate(MakeRequest(quantity: 200m), MakeInstrument(lotSize: 100));
            Assert.Equal(ValidationStatus.Valid, result.Status);
        }
    }

    // ── OrderValidator (chain) ──────────────────────────────────────
    public class OrderValidatorChainTests {
        [Fact]
        public void ValidOrder_PassesAllRules() {
            var cache = new InstrumentValidationCacheStub(MakeInstrument());
            var rules = new IOrderValidationRule[] {
                new InstrumentStatusValidator(),
                new OrderTypeValidator(),
                new PriceBandValidator(),
                new QuantityLimitsValidator()
            };
            var validator = new OrderValidator(cache, rules);

            var result = validator.Validate(MakeRequest());
            Assert.Equal(ValidationStatus.Valid, result.Status);
        }

        [Fact]
        public void RuleCount_ReturnsCorrectCount() {
            var cache = new InstrumentValidationCacheStub(MakeInstrument());
            var rules = new IOrderValidationRule[] {
                new InstrumentStatusValidator(),
                new OrderTypeValidator()
            };
            var validator = new OrderValidator(cache, rules);
            Assert.Equal(2, validator.RuleCount);
        }

        [Fact]
        public void FirstFailingRule_ShortCircuits() {
            var cache = new InstrumentValidationCacheStub(MakeInstrument(status: InstrumentStatus.Suspended));
            var rules = new IOrderValidationRule[] {
                new InstrumentStatusValidator(),
                new PriceBandValidator()
            };
            var validator = new OrderValidator(cache, rules);

            var result = validator.Validate(MakeRequest());
            Assert.Equal(ValidationStatus.Rejected, result.Status);
            Assert.Equal(BusinessRejectCode.InstrumentSuspended, result.RejectCode);
        }
    }

    /// <summary>
    /// Stub that replaces InstrumentValidationCache for testing without static store initialization.
    /// </summary>
    private sealed class InstrumentValidationCacheStub : KlingerExchange.Matching.Engine.Instrument.InstrumentValidationCache {
        private readonly InstrumentValidationMetadata? _instrument;

        public InstrumentValidationCacheStub(InstrumentValidationMetadata? instrument) {
            _instrument = instrument;
            if (instrument != null) AddOrUpdateInstrument(instrument);
        }
    }
}
