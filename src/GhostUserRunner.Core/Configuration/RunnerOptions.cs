namespace GhostUserRunner.Core.Configuration;

public sealed record RunnerOptions
{
    public IReadOnlyList<string> AllowedDomains { get; init; } = [];
    public IReadOnlyList<string> AllowedFolderRoots { get; init; } = [];
    public IReadOnlyList<string> AllowedExtensions { get; init; } = [];
    public IReadOnlyList<string> AllowedApplications { get; init; } = [];
    public IReadOnlyDictionary<string, double> ActivityWeights { get; init; }
        = new Dictionary<string, double>();
    public TimeSpan? SessionDuration { get; init; }
    public int RecentHistoryLimit { get; init; } = 100;
    public TimingOptions Timing { get; init; } = new();
}

public sealed record TimingOptions
{
    public IntRange MouseMoveMilliseconds { get; init; } = new(150, 900);
    public IntRange KeystrokeMilliseconds { get; init; } = new(40, 180);
    public IntRange PauseMilliseconds { get; init; } = new(250, 4_000);
}

public readonly record struct IntRange(int Minimum, int Maximum)
{
    public bool IsValid => Minimum >= 0 && Maximum >= Minimum;
}

public sealed record ValidationError(string Code, string Message);

public sealed record ValidationResult(IReadOnlyList<ValidationError> Errors)
{
    public bool IsValid => Errors.Count == 0;
}
