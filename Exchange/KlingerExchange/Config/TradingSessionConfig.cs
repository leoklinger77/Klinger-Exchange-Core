using KlingerShared.Config;
using System.Text.Json.Serialization;

namespace KlingerExchange.Config;

public sealed class TradingSessionConfig : ConfigBase<TradingSessionConfig>
{
    [JsonPropertyName("tradingDate")]
    public string TradingDate { get; set; } = string.Empty;

    [JsonPropertyName("sessionPhase")]
    public string SessionPhase { get; set; } = string.Empty;

    [JsonPropertyName("instruments")]
    public InstrumentSessionDto[] Instruments { get; set; } = Array.Empty<InstrumentSessionDto>();
}

public sealed class InstrumentSessionDto
{
    [JsonPropertyName("symbolIndex")]
    public short SymbolIndex { get; set; }

    [JsonPropertyName("symbol")]
    public string Symbol { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("referencePrice")]
    public decimal ReferencePrice { get; set; }

    [JsonPropertyName("previousClose")]
    public decimal PreviousClose { get; set; }

    [JsonPropertyName("previousHigh")]
    public decimal PreviousHigh { get; set; }

    [JsonPropertyName("previousLow")]
    public decimal PreviousLow { get; set; }

    [JsonPropertyName("upperLimit")]
    public decimal UpperLimit { get; set; }

    [JsonPropertyName("lowerLimit")]
    public decimal LowerLimit { get; set; }
}
