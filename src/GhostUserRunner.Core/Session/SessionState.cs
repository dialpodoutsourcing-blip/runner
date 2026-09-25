namespace GhostUserRunner.Core.Session;

public enum SessionState { Stopped, Starting, Running, Pausing, Paused, Stopping, Completed, Failed }

public sealed record SessionRequest(TimeSpan Duration, int Seed);

public static class SessionLimits
{
    public static TimeSpan MinimumDuration => TimeSpan.FromHours(8);
    public static TimeSpan DefaultDuration => TimeSpan.FromHours(8);
    public static TimeSpan MaximumGeneratedDelay => TimeSpan.FromSeconds(40);
}
