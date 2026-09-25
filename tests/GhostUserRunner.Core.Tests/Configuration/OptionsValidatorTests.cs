using GhostUserRunner.Core.Configuration;

namespace GhostUserRunner.Core.Tests.Configuration;

public sealed class OptionsValidatorTests
{
    [Fact]
    public void RejectsEmptyAllowlists()
    {
        var result = OptionsValidator.Validate(new RunnerOptions());

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Code == "domains.empty");
        Assert.Contains(result.Errors, error => error.Code == "folders.empty");
        Assert.Contains(result.Errors, error => error.Code == "extensions.empty");
        Assert.Contains(result.Errors, error => error.Code == "applications.empty");
    }

    [Fact]
    public void RejectsUnlimitedWildcardDomain()
    {
        var options = ValidOptions() with { AllowedDomains = ["*"] };

        var result = OptionsValidator.Validate(options);

        Assert.Contains(result.Errors, error => error.Code == "domain.wildcard");
    }

    [Fact]
    public void AcceptsUnlimitedDuration()
    {
        var options = ValidOptions() with { SessionDuration = null };

        Assert.True(OptionsValidator.Validate(options).IsValid);
    }

    [Fact]
    public void RejectsSessionDurationBelowEightHours()
    {
        var result = OptionsValidator.Validate(ValidOptions() with { SessionDuration = TimeSpan.FromHours(7) });
        Assert.Contains(result.Errors, error => error.Code == "session.too_short");
    }

    [Fact]
    public void RejectsPauseMaximumAboveFortySeconds()
    {
        var options = ValidOptions() with { Timing = ValidOptions().Timing with { PauseMilliseconds = new IntRange(250, 40_001) } };
        Assert.Contains(OptionsValidator.Validate(options).Errors, error => error.Code == "timing.pause_too_long");
    }

    [Fact]
    public void RejectsRelativePathsAndExecutableNames()
    {
        var options = ValidOptions() with
        {
            AllowedFolderRoots = ["relative-folder"],
            AllowedApplications = ["notepad.exe"]
        };

        var result = OptionsValidator.Validate(options);

        Assert.Contains(result.Errors, error => error.Code == "folder.not_absolute");
        Assert.Contains(result.Errors, error => error.Code == "application.not_absolute");
    }

    [Fact]
    public void RejectsInvalidWeightsHistoryAndTimingRanges()
    {
        var options = ValidOptions() with
        {
            ActivityWeights = new Dictionary<string, double> { ["browser"] = 0 },
            RecentHistoryLimit = 9,
            Timing = new TimingOptions
            {
                MouseMoveMilliseconds = new IntRange(500, 100),
                KeystrokeMilliseconds = new IntRange(120, 30),
                PauseMilliseconds = new IntRange(1_000, 100)
            }
        };

        var result = OptionsValidator.Validate(options);

        Assert.Contains(result.Errors, error => error.Code == "weight.nonpositive");
        Assert.Contains(result.Errors, error => error.Code == "history.out_of_range");
        Assert.Equal(3, result.Errors.Count(error => error.Code == "timing.invalid_range"));
    }

    private static RunnerOptions ValidOptions() => new()
    {
        AllowedDomains = ["https://www.youtube.com"],
        AllowedFolderRoots = [@"C:\GhostUserRunnerSafe"],
        AllowedExtensions = [".txt"],
        AllowedApplications = [@"C:\Windows\System32\notepad.exe"],
        ActivityWeights = new Dictionary<string, double> { ["browser"] = 1 },
        RecentHistoryLimit = 100,
        Timing = new TimingOptions
        {
            MouseMoveMilliseconds = new IntRange(100, 500),
            KeystrokeMilliseconds = new IntRange(30, 120),
            PauseMilliseconds = new IntRange(100, 1_000)
        }
    };
}
