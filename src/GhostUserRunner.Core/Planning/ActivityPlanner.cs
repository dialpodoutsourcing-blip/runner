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
    private readonly ActivityProgram _program;
    private int _programIndex;
    private int _browserActionsUntilClose;

    public ActivityPlanner(RunnerOptions options, IRandomSource random, SearchTopicGenerator topics, TimeSpan? duration = null)
    {
        _options = options;
        _random = random;
        _topics = topics;
        _history = new(options.RecentHistoryLimit);
        _browserActionsUntilClose = 6 + _random.NextInt(10);
        _program = new(BuildProgram(duration ?? TimeSpan.FromHours(8)));
    }

    public int HistoryCount => _history.Count;
    public IReadOnlyList<ProposedAction> PlannedActions => _program.Actions;

    public ProposedAction Next(ObservedContext context)
    {
        var action = _program.Actions[_programIndex++ % _program.Actions.Count];
        _history.Add(action.Kind.ToString());
        return action;
    }

    public void Record(ActionOutcome outcome) { }

    private static bool IsEligible(string name) => name switch
    {
        "idle" => false,
        "browserSearch" or "youtube" or "wikipedia" => true,
        "fileExplorer" => true,
        _ => false
    };

    private ProposedAction[] BuildProgram(TimeSpan duration)
    {
        var count = Math.Max(1, (int)Math.Ceiling(duration.TotalSeconds / 20));
        var actions = new List<ProposedAction>(count);
        var recentSignatures = new Queue<string>();
        string? previousCategory = null;
        while (actions.Count < count)
        {
            var eligible = _options.ActivityWeights
                .Where(pair => pair.Value > 0 && IsEligible(pair.Key) && !pair.Key.Equals(previousCategory, StringComparison.OrdinalIgnoreCase))
                .Select(pair => (Name: pair.Key, Weight: pair.Value))
                .ToArray();
            if (eligible.Length == 0)
                eligible = _options.ActivityWeights.Where(pair => pair.Value > 0 && IsEligible(pair.Key))
                    .Select(pair => (Name: pair.Key, Weight: pair.Value)).ToArray();
            if (eligible.Length == 0) throw new InvalidOperationException("At least one eligible activity category is required.");
            var category = Select(eligible);
            ProposedAction action;
            string signature;
            var attempts = 0;
            do
            {
                action = CreateAction(category);
                signature = Signature(action);
            } while (recentSignatures.Contains(signature) && ++attempts < 100);
            if (recentSignatures.Contains(signature)) continue;
            actions.Add(action);
            recentSignatures.Enqueue(signature);
            if (recentSignatures.Count > 30) recentSignatures.Dequeue();
            previousCategory = category;
        }
        return actions.ToArray();
    }

    private static string Signature(ProposedAction action) =>
        $"{action.Kind}|{action.Target}|{string.Join(';', action.Parameters?.OrderBy(pair => pair.Key).Select(pair => $"{pair.Key}={pair.Value}") ?? [])}";

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
        var topic = Uri.EscapeDataString(_topics.Next(_random));
        var isBrowser = category is "browserSearch" or "youtube" or "wikipedia";
        var closeAfter = false;
        if (isBrowser && --_browserActionsUntilClose == 0)
        {
            closeAfter = true;
            _browserActionsUntilClose = 6 + _random.NextInt(10);
        }
        var parameters = new Dictionary<string, string>
        {
            ["scrollCount"] = (2 + _random.NextInt(5)).ToString(),
            ["scrollDirection"] = _random.NextInt(2) == 0 ? "down" : "up",
            ["readingSeconds"] = (5 + _random.NextInt(16)).ToString(),
            ["closeAfter"] = closeAfter.ToString()
        };
        return category switch
        {
            "browserSearch" => new(ActionKind.SearchWeb, $"https://www.google.com/search?q={topic}", parameters),
            "youtube" => new(ActionKind.WatchVideo, $"https://www.youtube.com/results?search_query={topic}", parameters),
            "wikipedia" => new(ActionKind.NavigateWeb, $"https://en.wikipedia.org/wiki/Special:Search?search={topic}", parameters),
            "fileExplorer" => CreateFileAction(),
            _ => throw new InvalidOperationException($"Unknown activity category '{category}'.")
        };
    }

    private ProposedAction CreateFileAction()
    {
        var root = _options.AllowedFolderRoots[_random.NextInt(_options.AllowedFolderRoots.Count)];
        var parameters = new Dictionary<string, string>
        {
            ["readingSeconds"] = (5 + _random.NextInt(16)).ToString()
        };
        if (Directory.Exists(root) && _random.NextDouble() < .5)
        {
            var files = Directory.EnumerateFiles(root, "*", SearchOption.TopDirectoryOnly)
                .Where(file => _options.AllowedExtensions.Contains(Path.GetExtension(file), StringComparer.OrdinalIgnoreCase))
                .Take(100)
                .ToArray();
            if (files.Length > 0) return new(ActionKind.OpenFile, files[_random.NextInt(files.Length)], parameters);
        }
        return new(ActionKind.BrowseFolder, root, parameters);
    }
}
