using KlingerSimulator.Generation.Performance;
using KlingerSimulator.Generation.Randoms;
using KlingerSimulator.Generation.Strategy;

namespace KlingerSimulator.Generation;

/// <summary>
/// The orchestrator - combines strategy, randomness, and performance profile.
/// Zero business logic - just composition.
/// This is matching engine architecture.
/// </summary>
public sealed class Generator<T>
{
    private readonly IGenerationStrategy<T> _strategy;
    private readonly IRandomProvider _random;
    private readonly IPerformanceProfile _profile;

    public Generator(
        IGenerationStrategy<T> strategy,
        IRandomProvider random,
        IPerformanceProfile profile)
    {
        _strategy = strategy;
        _random = random;
        _profile = profile;
    }

    /// <summary>
    /// Generate 'count' items and pass each to the consumer.
    /// HOW it runs = _profile
    /// WHAT it generates = _strategy
    /// Randomness = _random
    /// </summary>
    public void Generate(int count, Action<T> consumer)
    {
        _profile.Run(count, i =>
        {
            var item = _strategy.Generate(i, _random);
            consumer(item);
        });
    }

    /// <summary>
    /// Generate 'count' items and return as array.
    /// Useful for batch collection scenarios.
    /// </summary>
    public T[] GenerateArray(int count)
    {
        var results = new T[count];
        _profile.Run(count, i =>
        {
            results[i] = _strategy.Generate(i, _random);
        });
        return results;
    }
}
