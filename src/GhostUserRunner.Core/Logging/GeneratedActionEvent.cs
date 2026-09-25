using GhostUserRunner.Core.Actions;

namespace GhostUserRunner.Core.Logging;

public sealed record GeneratedActionEvent(DateTimeOffset Timestamp, Guid SessionId, int Seed, ActionKind Kind, string Target, string OutcomeCode);
