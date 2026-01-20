using System.Collections.Concurrent;

namespace KlingerExchange.MarketData.Core;

/// <summary>
/// Maps symbol strings to short indices for bandwidth efficiency.
/// 5+ bytes string → 2 bytes index = 60% bandwidth reduction.
/// Thread-safe for concurrent access.
/// </summary>
public class SymbolMapper
{
    private readonly ConcurrentDictionary<string, short> _symbolToIndex = new();
    private readonly ConcurrentDictionary<short, string> _indexToSymbol = new();
    private short _nextIndex = 0;

    /// <summary>Preload symbol mappings to align with exchange configuration.</summary>
    public void Initialize(IEnumerable<(string Symbol, short Index)> mappings)
    {
        short maxIndex = -1;
        foreach (var (symbol, index) in mappings)
        {
            _symbolToIndex[symbol] = index;
            _indexToSymbol[index] = symbol;
            if (index > maxIndex)
                maxIndex = index;
        }

        _nextIndex = (short)(maxIndex + 1);
    }
    
    /// <summary>Get or create index for a symbol</summary>
    public short GetIndex(string symbol)
    {
        if (_symbolToIndex.TryGetValue(symbol, out var index))
            return index;
        
        // Lock-free increment with retry
        while (true)
        {
            var currentIndex = _nextIndex;
            var newIndex = (short)(currentIndex + 1);
            
            if (Interlocked.CompareExchange(ref _nextIndex, newIndex, currentIndex) == currentIndex)
            {
                // We won the race, register this symbol
                _symbolToIndex[symbol] = currentIndex;
                _indexToSymbol[currentIndex] = symbol;
                return currentIndex;
            }
            // Race lost, retry
        }
    }
    
    /// <summary>Get symbol from index (returns null if not found)</summary>
    public string? GetSymbol(short index)
    {
        _indexToSymbol.TryGetValue(index, out var symbol);
        return symbol;
    }
    
    /// <summary>Get all mappings for client initialization</summary>
    public Dictionary<short, string> GetAllMappings()
    {
        return new Dictionary<short, string>(_indexToSymbol);
    }
    
    /// <summary>Get total number of mapped symbols</summary>
    public int Count => _symbolToIndex.Count;
}
