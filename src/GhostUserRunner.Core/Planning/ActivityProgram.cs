using GhostUserRunner.Core.Actions;

namespace GhostUserRunner.Core.Planning;

public sealed record ActivityProgram(IReadOnlyList<ProposedAction> Actions);
