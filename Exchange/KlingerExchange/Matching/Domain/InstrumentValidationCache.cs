using System.Collections.Concurrent;
using KlingerExchange.Matching.Validation;
using KlingerExchange.Matching.Validation.Rules;

namespace KlingerExchange.Matching.Domain;

/// <summary>
/// Store de metadados de instrumentos para validação
/// Cache em memória, configurável em runtime
/// Usa InstrumentMetadataStore e SessionStore como fonte
/// </summary>
public sealed class InstrumentValidationCache
{
    private readonly ConcurrentDictionary<string, InstrumentValidationMetadata> _instruments;

    public InstrumentValidationCache()
    {
        _instruments = new ConcurrentDictionary<string, InstrumentValidationMetadata>(StringComparer.OrdinalIgnoreCase);
        LoadInstrumentsFromStores();
    }

    public InstrumentValidationMetadata? GetInstrument(string symbol)
    {
        _instruments.TryGetValue(symbol, out var instrument);
        return instrument;
    }

    public void AddOrUpdateInstrument(InstrumentValidationMetadata instrument)
    {
        _instruments[instrument.Symbol] = instrument;
    }

    private void LoadInstrumentsFromStores()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        
        // Carrega instrumentos dos stores já inicializados
        // Os stores foram inicializados no OmsAcceptors.Initialize()
        foreach (var (symbolIndex, symbol) in InstrumentMetadataStore.GetAllSymbols())
        {
            var metadata = InstrumentMetadataStore.Get(symbolIndex);
            ref var session = ref SessionStore.Get(symbolIndex);
            
            var tickSizeDecimal = SessionStore.GetPrice(metadata.TickSizeFixed);
            var referencePriceDecimal = SessionStore.GetPrice(session.ReferencePriceFixed);
            var upperLimitDecimal = SessionStore.GetPrice(session.UpperLimitFixed);
            var lowerLimitDecimal = SessionStore.GetPrice(session.LowerLimitFixed);
            
            var instrument = new InstrumentValidationMetadata
            {
                Symbol = symbol,
                Status = session.Status == MarketStatus.Continuous ? InstrumentStatus.Active : InstrumentStatus.Halted,
                ReferencePrice = referencePriceDecimal,
                UpperLimit = upperLimitDecimal,
                LowerLimit = lowerLimitDecimal,
                TickSize = tickSizeDecimal,
                MinQuantity = metadata.LotSize,
                MaxQuantity = 1_000_000m,
                LotSize = metadata.LotSize,
                AllowedOrderTypes = new HashSet<OrderType> { OrderType.Limit, OrderType.Market }
            };
            
            _instruments[symbol] = instrument;
        }
        
        sw.Stop();
        Serilog.Log.Information("Loaded {Count} instruments into validation cache in {ElapsedMs}ms", 
            _instruments.Count, sw.Elapsed.TotalMilliseconds);
    }
}

/// <summary>
/// Metadados de instrumento para validação
/// </summary>
public sealed class InstrumentValidationMetadata
{
    public string Symbol { get; init; } = string.Empty;
    public InstrumentStatus Status { get; init; }
    
    // Price bands
    public decimal ReferencePrice { get; init; }
    public decimal UpperLimit { get; init; }
    public decimal LowerLimit { get; init; }
    public decimal TickSize { get; init; }
    
    // Quantity limits
    public decimal MinQuantity { get; init; }
    public decimal MaxQuantity { get; init; }
    public decimal LotSize { get; init; } // Round lot
    
    // Allowed order types
    public HashSet<OrderType> AllowedOrderTypes { get; init; } = new();
}
