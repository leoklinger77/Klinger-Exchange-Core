using KlingerExchange.Matching.Domain;
using System.Collections.Concurrent;

namespace KlingerExchange.Matching.Engine;

public sealed class OrderBookRepository : IOrderBookRepository
{
    private readonly ConcurrentDictionary<string, FastOrderBook> _books;

    public OrderBookRepository()
    {
        _books = new ConcurrentDictionary<string, FastOrderBook>(StringComparer.OrdinalIgnoreCase);
    }

    public FastOrderBook GetOrCreateBook(string symbol)
    {
        return _books.GetOrAdd(symbol, s => new FastOrderBook(s));
    }

    public bool TryGetBook(string symbol, out FastOrderBook? book)
    {
        return _books.TryGetValue(symbol, out book);
    }

    public IEnumerable<FastOrderBook> GetAllBooks()
    {
        return _books.Values;
    }
}
