using System.Drawing;
using GhostUserRunner.Core.Randomness;

namespace GhostUserRunner.Infrastructure.Input;

public static class MousePathGenerator
{
    public static IReadOnlyList<Point> Generate(Point start, Point end, IRandomSource random, int steps)
    {
        if (steps < 2) throw new ArgumentOutOfRangeException(nameof(steps));
        var dx = end.X - start.X;
        var dy = end.Y - start.Y;
        var jitterX = (random.NextDouble() - .5) * Math.Abs(dx) * .2;
        var jitterY = (random.NextDouble() - .5) * Math.Abs(dy) * .2;
        var control = new PointF((float)(start.X + dx * .5 + jitterX), (float)(start.Y + dy * .5 + jitterY));
        var points = new List<Point>(steps) { start };
        for (var index = 1; index < steps - 1; index++)
        {
            var t = index / (double)(steps - 1);
            var inverse = 1 - t;
            var x = inverse * inverse * start.X + 2 * inverse * t * control.X + t * t * end.X;
            var y = inverse * inverse * start.Y + 2 * inverse * t * control.Y + t * t * end.Y;
            points.Add(new((int)Math.Round(x), (int)Math.Round(y)));
        }
        points.Add(end);
        return points;
    }
}
