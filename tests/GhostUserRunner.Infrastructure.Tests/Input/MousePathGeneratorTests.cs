using System.Drawing;
using GhostUserRunner.Core.Randomness;
using GhostUserRunner.Infrastructure.Input;

namespace GhostUserRunner.Infrastructure.Tests.Input;

public sealed class MousePathGeneratorTests
{
    [Fact]
    public void PathStartsAndEndsAtRequestedPoints()
    {
        var path = MousePathGenerator.Generate(new Point(10, 20), new Point(500, 300), new SeededRandomSource(4), 25);
        Assert.Equal(new Point(10, 20), path[0]);
        Assert.Equal(new Point(500, 300), path[^1]);
        Assert.All(path, point => { Assert.InRange(point.X, 0, 510); Assert.InRange(point.Y, 0, 310); });
    }
}
