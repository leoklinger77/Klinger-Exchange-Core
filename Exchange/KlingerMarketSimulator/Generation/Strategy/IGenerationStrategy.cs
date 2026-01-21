using KlingerSimulator.Generation.Randoms;

namespace KlingerSimulator.Generation.Strategy;

/// <summary>
/// Defines WHAT is generated (domain business logic).
/// Completely independent of HOW it's generated (performance) and randomness source.
/// </summary>
public interface IGenerationStrategy<T>
{
    /// <summary>
    /// Generate a single item using the provided random source.
    /// </summary>
    /// <param name="index">Sequential index of the item being generated</param>
    /// <param name="random">Random provider for any needed randomness</param>
    T Generate(int index, IRandomProvider random);
}
