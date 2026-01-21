using KlingerSimulator.Generation.Randoms;

namespace KlingerSimulator.Generation;

/// <summary>
/// Deterministic random provider for replay and backtesting.
/// Uses seeded System.Random for reproducible sequences.
/// </summary>
public sealed class DeterministicRandom : IRandomProvider
{
    private readonly Random _random;

    public DeterministicRandom(int seed)
    {
        _random = new Random(seed);
    }

    public int NextInt(int min, int max) => _random.Next(min, max);

    public double NextDouble() => _random.NextDouble();
}
