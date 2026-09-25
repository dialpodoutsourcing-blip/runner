namespace GhostUserRunner.Core.Safety;

public sealed record PolicyDecision(bool Allowed, string ReasonCode)
{
    public static PolicyDecision Permit() => new(true, "allowed");
    public static PolicyDecision Deny(string reasonCode) => new(false, reasonCode);
}
