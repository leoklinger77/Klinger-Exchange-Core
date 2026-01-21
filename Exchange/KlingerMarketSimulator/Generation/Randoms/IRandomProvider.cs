namespace KlingerSimulator.Generation.Randoms;

/// <summary>
/// Abstraction for random number generation.
/// Enables deterministic replay and high-performance alternatives to System.Random.
/// </summary>
public interface IRandomProvider
{
    /// <summary>
    /// Returns a random integer in the range [min, max).
    /// </summary>
    int NextInt(int min, int max);

    /// <summary>
    /// Returns a random double in the range [0.0, 1.0).
    /// </summary>
    double NextDouble();
}
