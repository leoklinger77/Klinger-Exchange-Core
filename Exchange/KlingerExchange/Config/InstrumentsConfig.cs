using System.Text.Json.Serialization;

namespace KlingerExchange.Config;

public sealed class InstrumentsConfig
{
    [JsonPropertyName("instruments")]
    public InstrumentDto[] Instruments { get; set; } = Array.Empty<InstrumentDto>();

    [JsonPropertyName("channels")]
    public ChannelDto[] Channels { get; set; } = Array.Empty<ChannelDto>();
}

public sealed class InstrumentDto
{
    [JsonPropertyName("symbolIndex")]
    public short SymbolIndex { get; set; }

    [JsonPropertyName("symbol")]
    public string Symbol { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("channel")]
    public byte Channel { get; set; }

    [JsonPropertyName("sector")]
    public string Sector { get; set; } = string.Empty;

    [JsonPropertyName("tickSize")]
    public decimal TickSize { get; set; }

    [JsonPropertyName("lotSize")]
    public int LotSize { get; set; }

    [JsonPropertyName("isFractional")]
    public bool IsFractional { get; set; }
}

public sealed class ChannelDto
{
    [JsonPropertyName("id")]
    public byte Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
}
