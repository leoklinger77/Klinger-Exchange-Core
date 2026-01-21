using KlingerSimulator.Generation.Models;
using KlingerSimulator.Generation.Random;

namespace KlingerSimulator.Generation.Strategy;

/// <summary>
/// Generates crossed order pairs for maximum fill rate.
/// Creates buy and sell orders that will immediately match.
/// </summary>
public sealed class CrossedPairStrategy : IGenerationStrategy<OrderRequest[]>
{
    private readonly OrderGenerationContext _context;
    private long _sequence;

    public CrossedPairStrategy(OrderGenerationContext context)
    {
        _context = context;
        _sequence = context.OrderSequence;
    }

    public OrderRequest[] Generate(int index, IRandomProvider random)
    {
        if (_context.Symbols.Length == 0)
            return Array.Empty<OrderRequest>();

        var symbol = _context.Symbols[random.NextInt(0, _context.Symbols.Length)];
        var spec = _context.GetInstrumentSpec(symbol);
        var quantity = GetRandomQuantity(random, spec);
        var lastPrice = _context.GetLastPrice(symbol);

        // Random market direction: 50% bullish, 50% bearish
        var isBullish = random.NextInt(0, 100) < 50;

        decimal buyPrice, sellPrice;

        if (isBullish)
        {
            // Bullish: price drifts UP - buy aggressive, sell passive
            var buyPremium = 0.005m + (decimal)random.NextDouble() * 0.015m;  // +0.5% to +2%
            var sellDiscount = (decimal)random.NextDouble() * 0.005m;          // 0% to -0.5%

            buyPrice = RoundToTick(lastPrice * (1m + buyPremium), spec.TickSize);
            sellPrice = RoundToTick(lastPrice * (1m - sellDiscount), spec.TickSize);
        }
        else
        {
            // Bearish: price drifts DOWN - sell aggressive, buy passive
            var sellDiscount = 0.005m + (decimal)random.NextDouble() * 0.015m; // -0.5% to -2%
            var buyPremium = (decimal)random.NextDouble() * 0.005m;             // 0% to +0.5%

            buyPrice = RoundToTick(lastPrice * (1m + buyPremium), spec.TickSize);
            sellPrice = RoundToTick(lastPrice * (1m - sellDiscount), spec.TickSize);
        }

        // Ensure minimum price
        if (sellPrice <= 0)
            sellPrice = RoundToTick(Math.Max(spec.TickSize, lastPrice * 0.95m), spec.TickSize);

        return new[]
        {
            new OrderRequest
            {
                Symbol = symbol,
                Side = QuickFix.Fields.Side.BUY,
                Price = buyPrice,
                Quantity = quantity,
                ClOrdId = GenerateClOrdId()
            },
            new OrderRequest
            {
                Symbol = symbol,
                Side = QuickFix.Fields.Side.SELL,
                Price = sellPrice,
                Quantity = quantity,
                ClOrdId = GenerateClOrdId()
            }
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
