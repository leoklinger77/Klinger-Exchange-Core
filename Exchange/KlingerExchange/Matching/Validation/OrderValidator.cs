using KlingerExchange.Matching.Domain;
using KlingerExchange.Matching.Validation.Rules;
using Serilog;

namespace KlingerExchange.Matching.Validation;

/// <summary>
/// Orquestrador de validação de ordens
/// Aplica chain of responsibility pattern
/// Target: <10µs overhead total
/// </summary>
public sealed class OrderValidator
{
    private static readonly ILogger Log = Serilog.Log.ForContext<OrderValidator>();
    private readonly List<IOrderValidationRule> _rules;
    private readonly InstrumentValidationCache _instrumentCache;

    public OrderValidator(
        InstrumentValidationCache instrumentCache,
        IEnumerable<IOrderValidationRule> rules)
    {
        _instrumentCache = instrumentCache;
        _rules = rules.ToList();
        
        Log.Information("OrderValidator initialized with {RuleCount} rules", _rules.Count);
    }

    /// <summary>
    /// Valida ordem completa
    /// Fast-fail: para no primeiro erro
    /// </summary>
    public ValidationResult Validate(OrderRequest request)
    {
        // Lookup do instrumento (cached, ~50ns)
        var instrument = _instrumentCache.GetInstrument(request.Symbol);

        // Aplica cada regra em sequência (fast-fail)
        foreach (var rule in _rules)
        {
            var result = rule.Validate(request, instrument);
            
            if (result.Status == ValidationStatus.Rejected)
            {
                // Log desabilitado para performance (economiza ~500ns por rejeição)
                // Log.Debug("Order {ClOrdId} rejected by {Rule}: {Reason}", 
                //     request.ClOrdId, 
                //     rule.GetType().Name, 
                //     result.RejectReason);
                
                return result;
            }
        }

        return ValidationResult.Valid();
    }

    /// <summary>
    /// Estatísticas de validação
    /// </summary>
    public int RuleCount => _rules.Count;
}
