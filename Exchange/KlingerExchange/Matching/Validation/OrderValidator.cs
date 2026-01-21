using KlingerExchange.Matching.Domain;
using KlingerExchange.Matching.Domain.Enums;
using KlingerExchange.Matching.Engine.Instrument;
using Serilog;
using Serilog.Events;

namespace KlingerExchange.Matching.Validation;

public sealed class OrderValidator {
    private static readonly ILogger Log = Serilog.Log.ForContext<OrderValidator>();
    private readonly IOrderValidationRule[] _rules;
    private readonly InstrumentValidationCache _instrumentCache;

    public OrderValidator(
        InstrumentValidationCache instrumentCache,
        IEnumerable<IOrderValidationRule> rules) {
        _instrumentCache = instrumentCache;
        _rules = rules.ToArray();

        Log.Information("OrderValidator initialized with {RuleCount} rules", _rules.Length);
    }

    public ValidationResult Validate(OrderRequest request) {
        var instrument = _instrumentCache.GetInstrument(request.Symbol);
        var rules = _rules; // Local copy for JIT optimization

        for (var i = 0; i < rules.Length; i++) {
            var result = rules[i].Validate(request, instrument);

            if (result.Status == ValidationStatus.Rejected) {
                if (Log.IsEnabled(LogEventLevel.Debug)) {
                    Log.Debug("Order {ClOrdId} rejected by {Rule}: {Reason}",
                        request.ClOrdId, rules[i].GetType().Name, result.RejectReason);
                }
                return result;
            }
        }

        return ValidationResult.Valid();
    }

    public int RuleCount => _rules.Length;
}
