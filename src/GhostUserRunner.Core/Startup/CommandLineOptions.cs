namespace GhostUserRunner.Core.Startup;

public sealed record CommandLineOptions(bool Simulate, double Hours, int? Seed)
{
    public static CommandLineOptions Parse(IReadOnlyList<string> args)
    {
        var simulate = false;
        var hours = 12d;
        int? seed = null;
        for (var index = 0; index < args.Count; index++)
        {
            switch (args[index])
            {
                case "--simulate": simulate = true; break;
                case "--hours" when index + 1 < args.Count && double.TryParse(args[++index], out var parsedHours) && parsedHours > 0: hours = parsedHours; break;
                case "--seed" when index + 1 < args.Count && int.TryParse(args[++index], out var parsedSeed): seed = parsedSeed; break;
                default: throw new ArgumentException($"Unknown or invalid argument '{args[index]}'.");
            }
        }
        return new(simulate, hours, seed);
    }
}
