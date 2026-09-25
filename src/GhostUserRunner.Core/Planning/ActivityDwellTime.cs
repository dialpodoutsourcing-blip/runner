using GhostUserRunner.Core.Actions;
using GhostUserRunner.Core.Randomness;

namespace GhostUserRunner.Core.Planning;

public static class ActivityDwellTime
{
    public static TimeSpan Choose(ActionKind kind, IRandomSource random)
    {
        var (minimum, maximum) = kind switch
        {
            ActionKind.WatchVideo => (30, 40),
            ActionKind.SearchWeb => (3, 15),
            ActionKind.Idle => (2, 30),
            _ => (1, 8)
        };
        return TimeSpan.FromSeconds(minimum + random.NextInt(maximum - minimum + 1));
    }
}
