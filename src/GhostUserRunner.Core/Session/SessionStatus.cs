using GhostUserRunner.Core.Logging;

namespace GhostUserRunner.Core.Session;

public sealed record SessionStatus(
    SessionState State,
    Guid? SessionId,
    DateTimeOffset? StartedAt,
    TimeSpan Elapsed,
    TimeSpan? Remaining,
    string? CurrentActivity,
    GeneratedActionEvent? LastAction,
    IReadOnlyList<GeneratedActionEvent> RecentEvents,
    string? Error);
