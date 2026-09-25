using GhostUserRunner.Core.Actions;
using GhostUserRunner.Core.Configuration;
using GhostUserRunner.Core.Planning;
using GhostUserRunner.Core.Randomness;
using GhostUserRunner.Core.Safety;

namespace GhostUserRunner.Core.Tests.Planning;

public sealed class EnduranceTests
{
    [Fact]
    public void TwelveHourEquivalentNeverProposesDeniedBrowserAction()
    {
        var options = new RunnerOptions
        {
            AllowedDomains = ["https://www.youtube.com", "https://www.google.com"],
            AllowedFolderRoots = [@"C:\GhostUserRunnerSafe"],
            AllowedExtensions = [".txt"],
            AllowedApplications = [@"C:\Windows\System32\notepad.exe"],
            ActivityWeights = new Dictionary<string, double> { ["browserSearch"] = 3, ["youtube"] = 3 },
            RecentHistoryLimit = 100
        };
        var policy = new ActionPolicy(options);
        var context = new ObservedContext(true, "browser", new Uri("https://www.youtube.com"), null);

        for (var seed = 0; seed < 100; seed++)
        {
            var planner = new ActivityPlanner(options, new SeededRandomSource(seed), new SearchTopicGenerator(["nature", "space", "history", "cooking"]));
            for (var minute = 0; minute < 12 * 60; minute++)
            {
                var action = planner.Next(context);
                Assert.NotEqual(ActionKind.Idle, action.Kind);
                Assert.True(policy.Evaluate(action, context).Allowed, $"Seed {seed}, minute {minute}, action {action}");
            }
            Assert.InRange(planner.HistoryCount, 1, options.RecentHistoryLimit);
        }
    }
}
