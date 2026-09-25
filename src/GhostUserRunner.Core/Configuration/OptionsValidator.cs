namespace GhostUserRunner.Core.Configuration;

public static class OptionsValidator
{
    public static ValidationResult Validate(RunnerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var errors = new List<ValidationError>();

        RequireItems(options.AllowedDomains, "domains.empty", "At least one HTTPS domain is required.", errors);
        RequireItems(options.AllowedFolderRoots, "folders.empty", "At least one folder root is required.", errors);
        RequireItems(options.AllowedExtensions, "extensions.empty", "At least one file extension is required.", errors);
        RequireItems(options.AllowedApplications, "applications.empty", "At least one application is required.", errors);

        foreach (var value in options.AllowedDomains)
        {
            if (value == "*")
            {
                errors.Add(new("domain.wildcard", "Wildcard domains are not allowed."));
                continue;
            }

            if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
                uri.Scheme != Uri.UriSchemeHttps ||
                string.IsNullOrWhiteSpace(uri.Host) ||
                uri.AbsolutePath != "/" ||
                !string.IsNullOrEmpty(uri.Query) ||
                !string.IsNullOrEmpty(uri.Fragment))
            {
                errors.Add(new("domain.invalid", $"Domain entry '{value}' must be an HTTPS origin."));
            }
        }

        ValidateAbsolutePaths(options.AllowedFolderRoots, "folder.not_absolute", errors);
        ValidateAbsolutePaths(options.AllowedApplications, "application.not_absolute", errors);

        if (options.ActivityWeights.Count == 0)
        {
            errors.Add(new("weights.empty", "At least one activity weight is required."));
        }
        else if (options.ActivityWeights.Any(pair => string.IsNullOrWhiteSpace(pair.Key) || pair.Value <= 0))
        {
            errors.Add(new("weight.nonpositive", "Activity names must be present and weights must be positive."));
        }

        if (options.RecentHistoryLimit is < 10 or > 10_000)
        {
            errors.Add(new("history.out_of_range", "Recent history limit must be between 10 and 10,000."));
        }

        ValidateRange(options.Timing.MouseMoveMilliseconds, "mouse movement", errors);
        ValidateRange(options.Timing.KeystrokeMilliseconds, "keystrokes", errors);
        ValidateRange(options.Timing.PauseMilliseconds, "pauses", errors);
        if (options.SessionDuration is { } duration && duration < Session.SessionLimits.MinimumDuration)
            errors.Add(new("session.too_short", "Session duration must be at least eight hours."));
        if (options.Timing.PauseMilliseconds.Maximum > 40_000)
            errors.Add(new("timing.pause_too_long", "Generated pauses may not exceed 40 seconds."));

        return new(errors);
    }

    private static void RequireItems<T>(IReadOnlyCollection<T> items, string code, string message, ICollection<ValidationError> errors)
    {
        if (items.Count == 0)
        {
            errors.Add(new(code, message));
        }
    }

    private static void ValidateAbsolutePaths(IEnumerable<string> paths, string code, ICollection<ValidationError> errors)
    {
        foreach (var path in paths)
        {
            if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path))
            {
                errors.Add(new(code, $"Path '{path}' must be absolute."));
            }
        }
    }

    private static void ValidateRange(IntRange range, string name, ICollection<ValidationError> errors)
    {
        if (!range.IsValid)
        {
            errors.Add(new("timing.invalid_range", $"The {name} timing range is invalid."));
        }
    }
}
