using KlingerExchange.Matching.Domain;
using KlingerExchange.Matching.Domain.Enums;
using System.Collections.Concurrent;

namespace KlingerExchange.Matching.Engine.Instrument;

public sealed class InstrumentValidationCache {
    private readonly ConcurrentDictionary<string, InstrumentValidationMetadata> _instruments;

    public InstrumentValidationCache() {
        _instruments = new ConcurrentDictionary<string, InstrumentValidationMetadata>(StringComparer.OrdinalIgnoreCase);
        LoadInstrumentsFromStores();
    }

    public InstrumentValidationMetadata? GetInstrument(string symbol) {
        _instruments.TryGetValue(symbol, out var instrument);
        return instrument;
    }

    public void AddOrUpdateInstrument(InstrumentValidationMetadata instrument) {
        _instruments[instrument.Symbol] = instrument;
    }

    private void LoadInstrumentsFromStores() {
        var sw = System.Diagnostics.Stopwatch.StartNew();

        foreach (var (symbolIndex, symbol) in InstrumentMetadataStore.GetAllSymbols()) {
            var metadata = InstrumentMetadataStore.Get(symbolIndex);
            ref var session = ref SessionStore.Get(symbolIndex);

            var tickSizeDecimal = SessionStore.GetPrice(metadata.TickSizeFixed);
            var referencePriceDecimal = SessionStore.GetPrice(session.ReferencePriceFixed);
            var upperLimitDecimal = SessionStore.GetPrice(session.UpperLimitFixed);
            var lowerLimitDecimal = SessionStore.GetPrice(session.LowerLimitFixed);

            var instrument = new InstrumentValidationMetadata {
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
