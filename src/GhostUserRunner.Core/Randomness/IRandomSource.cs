namespace GhostUserRunner.Core.Randomness;

public interface IRandomSource
{
    double NextDouble();
    int NextInt(int exclusiveMaximum);
}

public sealed class SeededRandomSource(int seed) : IRandomSource
{
    private readonly Random _random = new(seed);
    public double NextDouble() => _random.NextDouble();
    public int NextInt(int exclusiveMaximum) => _random.Next(exclusiveMaximum);
}
