using GhostUserRunner.Core.Actions;
using GhostUserRunner.Core.Configuration;
using GhostUserRunner.Core.Execution;
using GhostUserRunner.Core.Randomness;

namespace GhostUserRunner.Core.Planning;

public sealed class ActivityPlanner : IActivityPlanner
{
    private readonly RunnerOptions _options;
    private readonly IRandomSource _random;
    private readonly SearchTopicGenerator _topics;
    private readonly BoundedHistory<string> _history;

    public ActivityPlanner(RunnerOptions options, IRandomSource random, SearchTopicGenerator topics)
    {
        _options = options;
        _random = random;
        _topics = topics;
        _history = new(options.RecentHistoryLimit);
    }

    public int HistoryCount => _history.Count;

    public ProposedAction Next(ObservedContext context)
    {
        var eligible = _options.ActivityWeights
            .Where(pair => pair.Value > 0 && IsEligible(pair.Key, context))
            .Select(pair => (Name: pair.Key, Weight: PenalizedWeight(pair.Key, pair.Value)))
            .ToArray();
        if (eligible.Length == 0) throw new InvalidOperationException("No activity is eligible in the current context.");

        var selected = Select(eligible);
        var action = CreateAction(selected);
        _history.Add(selected);
        return action;
    }

    public void Record(ActionOutcome outcome) { }

    private double PenalizedWeight(string name, double weight) =>
        _history.Items.TakeLast(3).Contains(name, StringComparer.OrdinalIgnoreCase) ? weight / 4d : weight;

    private static bool IsEligible(string name, ObservedContext context) => name switch
    {
        "idle" => true,
        "browserSearch" or "youtube" => context.ProcessName.Contains("browser", StringComparison.OrdinalIgnoreCase),
        "fileExplorer" => true,
        _ => false
    };

    private string Select((string Name, double Weight)[] entries)
    {
        var threshold = _random.NextDouble() * entries.Sum(entry => entry.Weight);
        foreach (var entry in entries)
        {
            threshold -= entry.Weight;
            if (threshold <= 0) return entry.Name;
        }
        return entries[^1].Name;
    }

    private ProposedAction CreateAction(string category)
    {
        var topic = category == "idle" ? string.Empty : Uri.EscapeDataString(_topics.Next(_random));
        return category switch
        {
            "idle" => new(ActionKind.Idle, "idle"),
            "browserSearch" => new(ActionKind.SearchWeb, $"https://www.google.com/search?q={topic}"),
            "youtube" => new(ActionKind.WatchVideo, $"https://www.youtube.com/results?search_query={topic}"),
            "fileExplorer" => CreateFileAction(),
            _ => throw new InvalidOperationException($"Unknown activity category '{category}'.")
        };
    }

    private ProposedAction CreateFileAction()
    {
        var root = _options.AllowedFolderRoots[_random.NextInt(_options.AllowedFolderRoots.Count)];
        if (Directory.Exists(root) && _random.NextDouble() < .5)
        {
            var files = Directory.EnumerateFiles(root, "*", SearchOption.TopDirectoryOnly)
                .Where(file => _options.AllowedExtensions.Contains(Path.GetExtension(file), StringComparer.OrdinalIgnoreCase))
                .Take(100)
                .ToArray();
            if (files.Length > 0) return new(ActionKind.OpenFile, files[_random.NextInt(files.Length)]);
        }
        return new(ActionKind.BrowseFolder, root);
    }
}
