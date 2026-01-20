using KlingerExchange.Matching.Domain;

namespace KlingerExchange.Matching.Engine;

public interface IOrderBookRepository
{
    FastOrderBook GetOrCreateBook(string symbol);
    bool TryGetBook(string symbol, out FastOrderBook? book);
    IEnumerable<FastOrderBook> GetAllBooks();
}
