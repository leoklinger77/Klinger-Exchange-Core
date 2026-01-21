using KlingerExchange.Config;
using KlingerExchange.Matching.Domain.Struct;

namespace KlingerExchange.Matching.Engine.Instrument;

public static class InstrumentMetadataStore
{
    private static InstrumentMetadata[] _metadata = Array.Empty<InstrumentMetadata>();
    private static Dictionary<string, short> _symbolToIndex = new();
    private static Dictionary<string, string> _symbolNames = new();
    private static Dictionary<byte, string> _channelNames = new();

    public static int Count => _metadata.Length;

    public static void Initialize(InstrumentsConfig config)
    {
        _metadata = new InstrumentMetadata[config.Instruments.Length];
        _symbolToIndex = new Dictionary<string, short>(config.Instruments.Length);
        _symbolNames = new Dictionary<string, string>(config.Instruments.Length);

        foreach (var dto in config.Instruments)
        {
            var index = dto.SymbolIndex;

            _metadata[index] = new InstrumentMetadata
            {
                SymbolIndex = dto.SymbolIndex,
                Channel = dto.Channel,
                TickSizeFixed = (long)(dto.TickSize * 100_000m),
                LotSize = dto.LotSize,
                Flags = (byte)(dto.IsFractional ? 1 : 0)
            };

            _symbolToIndex[dto.Symbol] = dto.SymbolIndex;
            _symbolNames[dto.Symbol] = dto.Name;
        }

        _channelNames = config.Channels.ToDictionary(c => c.Id, c => c.Name);
    }

    public static InstrumentMetadata Get(short symbolIndex)
    {
        if (symbolIndex < 0 || symbolIndex >= _metadata.Length)
            throw new ArgumentOutOfRangeException(nameof(symbolIndex));

        return _metadata[symbolIndex];
    }

    public static bool TryGetSymbolIndex(string symbol, out short symbolIndex)
    {
        return _symbolToIndex.TryGetValue(symbol, out symbolIndex);
    }

    public static string GetSymbolName(string symbol)
    {
        return _symbolNames.TryGetValue(symbol, out var name) ? name : symbol;
    }

    public static string GetChannelName(byte channelId)
    {
        return _channelNames.TryGetValue(channelId, out var name) ? name : $"Channel {channelId}";
    }

    public static IEnumerable<(short Index, string Symbol)> GetAllSymbols()
    {
        return _symbolToIndex.Select(kvp => (kvp.Value, kvp.Key));
    }
}
