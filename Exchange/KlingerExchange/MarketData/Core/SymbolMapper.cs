using System.Collections.Concurrent;

namespace KlingerExchange.MarketData.Core;

/// <summary>
/// Maps symbol strings to short indices for bandwidth efficiency.
/// 5+ bytes string → 2 bytes index = 60% bandwidth reduction.
/// Thread-safe for concurrent access.
/// </summary>
public class SymbolMapper {
    private readonly ConcurrentDictionary<string, short> _symbolToIndex = new();
    private readonly ConcurrentDictionary<short, string> _indexToSymbol = new();
    private short _nextIndex = 0;

    public void Initialize(IEnumerable<(string Symbol, short Index)> mappings) {
        short maxIndex = -1;
        foreach (var (symbol, index) in mappings) {
            _symbolToIndex[symbol] = index;
            _indexToSymbol[index] = symbol;
            if (index > maxIndex)
                maxIndex = index;
        }

        _nextIndex = (short)(maxIndex + 1);
    }

    public short GetIndex(string symbol) {
        if (_symbolToIndex.TryGetValue(symbol, out var index))
            return index;

        while (true) {
            var currentIndex = _nextIndex;
            var newIndex = (short)(currentIndex + 1);

            if (Interlocked.CompareExchange(ref _nextIndex, newIndex, currentIndex) == currentIndex) {
                _symbolToIndex[symbol] = currentIndex;
                _indexToSymbol[currentIndex] = symbol;
                return currentIndex;
            }
        }
    }

    public string? GetSymbol(short index) {
        _indexToSymbol.TryGetValue(index, out var symbol);
        return symbol;
    }

    public Dictionary<short, string> GetAllMappings() {
        return new Dictionary<short, string>(_indexToSymbol);
    }

    public int Count => _symbolToIndex.Count;
}
