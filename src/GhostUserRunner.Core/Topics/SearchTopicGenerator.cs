using GhostUserRunner.Core.Randomness;

namespace GhostUserRunner.Core.Planning;

public sealed class SearchTopicGenerator(IReadOnlyList<string> topics)
{
    public string Next(IRandomSource random)
    {
        if (topics.Count == 0) throw new InvalidOperationException("At least one safe search topic is required.");
        return topics[random.NextInt(topics.Count)];
    }
}
