namespace KlingerSimulator.Generation.Randoms;

/// <summary>
/// Ultra-fast thread-safe random provider using XorShift algorithm.
/// NO LOCK - ideal for high-throughput scenarios where System.Random becomes a bottleneck.
/// </summary>
public sealed class XorShiftRandom : IRandomProvider
{
    private ulong _state;

    public XorShiftRandom(ulong seed)
    {
        _state = seed == 0 ? 88172645463325252UL : seed;
    }

    public int NextInt(int min, int max)
    {
        var range = (ulong)(max - min);
        return (int)(NextULong() % range) + min;
    }

    public double NextDouble()
    {
        return (NextULong() >> 11) * (1.0 / (1UL << 53));
    }

    private ulong NextULong()
    {
        _state ^= _state << 7;
        _state ^= _state >> 9;
        return _state;
    }
}
