using GhostUserRunner.Core.Configuration;
using GhostUserRunner.Core.Startup;

namespace GhostUserRunner.Core.Tests.Configuration;

public sealed class RunnerStartupTests
{
    [Fact]
    public void ParsesSimulationArguments()
    {
        var options = CommandLineOptions.Parse(["--simulate", "--hours", "12", "--seed", "12345"]);

        Assert.True(options.Simulate);
        Assert.Equal(12, options.Hours);
        Assert.Equal(12345, options.Seed);
    }

    [Fact]
    public void LoadsRunnerOptionsFromJson()
    {
        const string json = """
        {
          "allowedDomains": ["https://www.youtube.com"],
          "allowedFolderRoots": ["C:\\GhostUserRunnerSafe"],
          "allowedExtensions": [".txt"],
          "allowedApplications": ["C:\\Windows\\System32\\notepad.exe"],
          "activityWeights": { "browser": 1 },
          "recentHistoryLimit": 100
        }
        """;

        var options = RunnerOptionsLoader.Load(json);

        Assert.Equal("https://www.youtube.com", Assert.Single(options.AllowedDomains));
        Assert.True(OptionsValidator.Validate(options).IsValid);
    }

    [Fact]
    public void ResolvesAppDirectoryFolderRootBeforeValidation()
    {
        const string json = """
        {
          "allowedDomains": ["https://www.youtube.com"],
          "allowedFolderRoots": ["{AppDirectory}\\SafeFiles"],
          "allowedExtensions": [".txt"],
          "allowedApplications": ["C:\\Windows\\System32\\notepad.exe"],
          "activityWeights": { "browser": 1 },
          "recentHistoryLimit": 100
        }
        """;
        var installDirectory = Path.Combine(Path.GetTempPath(), "GhostUserRunner-install");

        var options = RunnerOptionsPathResolver.Resolve(
            RunnerOptionsLoader.Load(json), installDirectory);

        Assert.Equal(
            Path.Combine(installDirectory, "SafeFiles"),
            Assert.Single(options.AllowedFolderRoots));
        Assert.True(OptionsValidator.Validate(options).IsValid);
    }
}
