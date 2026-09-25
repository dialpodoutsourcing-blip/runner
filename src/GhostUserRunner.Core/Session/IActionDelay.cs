namespace GhostUserRunner.Core.Session;

public interface IActionDelay
{
    Task WaitAsync(TimeSpan duration, CancellationToken cancellationToken);
}

public sealed class SystemActionDelay : IActionDelay
{
    public static SystemActionDelay Instance { get; } = new();
    private SystemActionDelay() { }
    public Task WaitAsync(TimeSpan duration, CancellationToken cancellationToken) =>
        Task.Delay(duration, cancellationToken);
}
