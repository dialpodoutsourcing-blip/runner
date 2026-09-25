using System.Drawing;
using GhostUserRunner.Core.Randomness;

namespace GhostUserRunner.Infrastructure.Input;

public interface IInputSink
{
    void MoveTo(Point point);
    void KeyDown(ushort virtualKey);
    void KeyUp(ushort virtualKey);
    void MouseButtonDown();
    void MouseButtonUp();
    void Scroll(int amount);
    void ReleaseAll();
}

public sealed class HumanInputEngine(IInputSink sink, IRandomSource random)
{
    public async Task ClickAsync(Point point, CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            sink.MoveTo(point);
            sink.MouseButtonDown();
            await Task.Delay(35 + random.NextInt(86), cancellationToken);
        }
        finally { sink.MouseButtonUp(); }
    }

    public Task ScrollAsync(int amount, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        sink.Scroll(amount);
        return Task.CompletedTask;
    }

    public async Task TypeTextAsync(string text, CancellationToken cancellationToken)
    {
        foreach (var character in text)
        {
            var key = character switch
            {
                >= 'a' and <= 'z' => (ushort)char.ToUpperInvariant(character),
                >= 'A' and <= 'Z' => (ushort)character,
                >= '0' and <= '9' => (ushort)character,
                ' ' => (ushort)0x20,
                _ => throw new ArgumentException($"Unsupported input character '{character}'.", nameof(text))
            };
            await TypeAsync([key], cancellationToken);
        }
    }

    public async Task MoveAsync(Point start, Point end, CancellationToken cancellationToken)
    {
        try
        {
            foreach (var point in MousePathGenerator.Generate(start, end, random, 30 + random.NextInt(30)))
            {
                cancellationToken.ThrowIfCancellationRequested();
                sink.MoveTo(point);
                await Task.Delay(4 + random.NextInt(9), cancellationToken);
            }
        }
        finally { sink.ReleaseAll(); }
    }

    public async Task TypeAsync(IEnumerable<ushort> virtualKeys, CancellationToken cancellationToken)
    {
        try
        {
            foreach (var key in virtualKeys)
            {
                cancellationToken.ThrowIfCancellationRequested();
                sink.KeyDown(key);
                sink.KeyUp(key);
                await Task.Delay(35 + random.NextInt(120), cancellationToken);
            }
        }
        finally { sink.ReleaseAll(); }
    }
}
