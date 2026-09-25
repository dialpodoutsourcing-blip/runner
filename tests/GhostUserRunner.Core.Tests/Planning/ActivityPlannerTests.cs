using GhostUserRunner.Core.Actions;
using GhostUserRunner.Core.Configuration;
using GhostUserRunner.Core.Planning;
using GhostUserRunner.Core.Randomness;

namespace GhostUserRunner.Core.Tests.Planning;

public sealed class ActivityPlannerTests
{
    [Fact]
    public void SameSeedProducesSameThousandActions()
    {
        var first = CreatePlanner(42);
        var second = CreatePlanner(42);
        var context = new ObservedContext(true, "browser", new Uri("https://www.youtube.com"), null);

        var firstActions = Enumerable.Range(0, 1_000).Select(_ => first.Next(context)).ToArray();
        var secondActions = Enumerable.Range(0, 1_000).Select(_ => second.Next(context)).ToArray();

        Assert.Equal(firstActions.Select(Signature), secondActions.Select(Signature));
    }

    [Fact]
    public void BuildsCompleteProgramAtStartupWithoutAdjacentTemplateRepeats()
    {
        var planner = new ActivityPlanner(Options(), new SeededRandomSource(42),
            new SearchTopicGenerator(["nature", "space", "history"]), TimeSpan.FromHours(8));

        Assert.True(planner.PlannedActions.Count >= 8 * 60);
        Assert.DoesNotContain(planner.PlannedActions.Zip(planner.PlannedActions.Skip(1)),
            pair => pair.First.Kind == pair.Second.Kind);
    }

    [Fact]
    public void ExactActivitySignatureDoesNotRepeatWithinTenMinutes()
    {
        var planner = new ActivityPlanner(Options(), new SeededRandomSource(17),
            new SearchTopicGenerator(["nature", "space", "history"]), TimeSpan.FromHours(8));

        var signatures = planner.PlannedActions.Select(action => $"{action.Kind}|{action.Target}|{string.Join(';', action.Parameters ?? new Dictionary<string, string>())}").ToArray();
        for (var index = 0; index < signatures.Length; index++)
            Assert.DoesNotContain(signatures[index], signatures.Skip(index + 1).Take(30));
    }

    [Fact]
    public void IdleCategoryIsNeverSelectedAndHistoryIsBounded()
    {
        var options = Options() with
        {
            ActivityWeights = new Dictionary<string, double> { ["idle"] = 100, ["browserSearch"] = 1, ["youtube"] = 0 },
            RecentHistoryLimit = 10
        };
        var planner = new ActivityPlanner(options, new SeededRandomSource(9), new SearchTopicGenerator(["nature"]));
        var context = new ObservedContext(true, "browser", null, null);

        var actions = Enumerable.Range(0, 100).Select(_ => planner.Next(context)).ToArray();

        Assert.All(actions, action => Assert.Equal(ActionKind.SearchWeb, action.Kind));
        Assert.Equal(10, planner.HistoryCount);
    }

    [Fact]
    public void GeneratedQueriesComeOnlyFromConfiguredTopics()
    {
        var planner = new ActivityPlanner(
            Options() with { ActivityWeights = new Dictionary<string, double> { ["browserSearch"] = 1 } },
            new SeededRandomSource(7), new SearchTopicGenerator(["nature", "space"]));

        var actions = Enumerable.Range(0, 50).Select(_ => planner.Next(new(true, "browser", null, null))).ToArray();

        Assert.All(actions, action => Assert.Contains(new[] { "nature", "space" }, topic => action.Target.Contains(topic, StringComparison.Ordinal)));
    }

    [Fact]
    public void FileActivitySometimesSelectsApprovedFile()
    {
        var root = Path.Combine(Path.GetTempPath(), "ghost-planner", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var safeFile = Path.Combine(root, "notes.txt");
        File.WriteAllText(safeFile, "safe");
        try
        {
            var options = Options() with
            {
                AllowedFolderRoots = [root],
                ActivityWeights = new Dictionary<string, double> { ["fileExplorer"] = 1 }
            };
            var planner = new ActivityPlanner(options, new SeededRandomSource(8), new SearchTopicGenerator(["nature"]));

            var actions = Enumerable.Range(0, 30).Select(_ => planner.Next(new(true, "browser", null, null))).ToArray();

            Assert.Contains(actions, action => action.Kind == ActionKind.OpenFile && action.Target == safeFile);
            Assert.All(actions, action => Assert.False(action.Parameters?.ContainsKey("variation")));
        }
        finally { Directory.Delete(root, true); }
    }

    [Theory]
    [InlineData(ActionKind.WatchVideo, 30, 180)]
    [InlineData(ActionKind.SearchWeb, 3, 15)]
    public void ActivityDwellTimesStayInsideHumanRanges(ActionKind kind, int minimumSeconds, int maximumSeconds)
    {
        var delay = ActivityDwellTime.Choose(kind, new SeededRandomSource(5));
        Assert.InRange(delay.TotalSeconds, minimumSeconds, maximumSeconds);
    }

    [Theory]
    [InlineData(ActionKind.WatchVideo)]
    [InlineData(ActionKind.SearchWeb)]
    public void EveryGeneratedDwellIsAtMostFortySeconds(ActionKind kind)
    {
        for (var seed = 0; seed < 100; seed++)
            Assert.InRange(ActivityDwellTime.Choose(kind, new SeededRandomSource(seed)), TimeSpan.Zero, TimeSpan.FromSeconds(40));
    }

    private static ActivityPlanner CreatePlanner(int seed) => new(Options(), new SeededRandomSource(seed), new SearchTopicGenerator(["nature", "space", "history"]));

    private static string Signature(ProposedAction action) =>
        $"{action.Kind}|{action.Target}|{string.Join(';', action.Parameters?.OrderBy(pair => pair.Key).Select(pair => $"{pair.Key}={pair.Value}") ?? [])}";

    private static RunnerOptions Options() => new()
    {
        AllowedDomains = ["https://www.youtube.com", "https://www.google.com"],
        AllowedFolderRoots = [@"C:\GhostUserRunnerSafe"],
        AllowedExtensions = [".txt"],
        AllowedApplications = [@"C:\Windows\System32\notepad.exe"],
        ActivityWeights = new Dictionary<string, double> { ["browserSearch"] = 2, ["youtube"] = 2 },
        RecentHistoryLimit = 25
    };
}
