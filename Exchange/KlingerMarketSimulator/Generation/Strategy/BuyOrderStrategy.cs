using KlingerSimulator.Generation.Models;
using KlingerSimulator.Generation.Random;

namespace KlingerSimulator.Generation.Strategy;

/// <summary>
/// Generates single buy orders with realistic price distribution.
/// </summary>
public sealed class BuyOrderStrategy : IGenerationStrategy<OrderRequest>
{
    private readonly OrderGenerationContext _context;
    private long _sequence;

    public BuyOrderStrategy(OrderGenerationContext context)
    {
        _context = context;
        _sequence = context.OrderSequence;
    }

    public OrderRequest Generate(int index, IRandomProvider random)
    {
        if (_context.Symbols.Length == 0)
            throw new InvalidOperationException("No symbols available");

        var symbol = _context.Symbols[random.NextInt(0, _context.Symbols.Length)];
        var spec = _context.GetInstrumentSpec(symbol);
        var quantity = GetRandomQuantity(random, spec);
        var lastPrice = _context.GetLastPrice(symbol);

        // 30% aggressive (above market), 40% at market, 30% passive (below market)
        var roll = random.NextInt(0, 100);
        decimal price;

        if (roll < 30)
        {
            // Aggressive: pay above market (+0.1% to +1%)
            var premium = 0.001m + (decimal)random.NextDouble() * 0.009m;
            price = RoundToTick(lastPrice * (1m + premium), spec.TickSize);
        }
        else if (roll < 70)
        {
            // At market: very tight spread (-0.1% to +0.1%)
            var variation = ((decimal)random.NextDouble() - 0.5m) * 0.002m;
            price = RoundToTick(lastPrice * (1m + variation), spec.TickSize);
        }
        else
        {
            // Passive: bid below market (-0.1% to -1%)
            var discount = 0.001m + (decimal)random.NextDouble() * 0.009m;
            price = RoundToTick(lastPrice * (1m - discount), spec.TickSize);
        }

        return new OrderRequest
        {
            Symbol = symbol,
            Side = QuickFix.Fields.Side.BUY,
            Price = price,
            Quantity = quantity,
            ClOrdId = GenerateClOrdId()
        };
    }

    private static decimal RoundToTick(decimal price, decimal tickSize)
    {
        if (tickSize <= 0)
            return Math.Round(price, 2);

        var ticks = Math.Round(price / tickSize, MidpointRounding.AwayFromZero);
        return ticks * tickSize;
    }

    private static decimal GetRandomQuantity(IRandomProvider random, InstrumentSpec spec)
    {
        var lotSize = spec.LotSize > 0 ? spec.LotSize : 100;
        var lots = random.NextInt(1, 21); // 1 to 20 lots
        return lotSize * lots;
    }

    private string GenerateClOrdId()
    {
        return $"SIM{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}{Interlocked.Increment(ref _sequence):x}";
    }
}
