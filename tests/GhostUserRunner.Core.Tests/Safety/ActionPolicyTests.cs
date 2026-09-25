using GhostUserRunner.Core.Actions;
using GhostUserRunner.Core.Configuration;
using GhostUserRunner.Core.Safety;

namespace GhostUserRunner.Core.Tests.Safety;

public sealed class ActionPolicyTests
{
    public static TheoryData<ActionKind> BlockedKinds => new()
    {
        ActionKind.DeleteFile, ActionKind.SubmitForm, ActionKind.Download,
        ActionKind.SendMessage, ActionKind.EditFile, ActionKind.Upload
    };

    [Theory]
    [MemberData(nameof(BlockedKinds))]
    public void AlwaysRejectsBlockedKinds(ActionKind kind)
    {
        Assert.False(CreatePolicy().Evaluate(new ProposedAction(kind, "target"), SafeContext()).Allowed);
    }

    [Fact]
    public void RejectsUnknownWindow()
    {
        var decision = CreatePolicy().Evaluate(
            new ProposedAction(ActionKind.Idle, "idle"),
            SafeContext() with { IsRecognizedWindow = false });

        Assert.Equal("window.unknown", decision.ReasonCode);
    }

    [Theory]
    [InlineData("https://www.youtube.com/watch?v=abc", true)]
    [InlineData("https://music.youtube.com/watch?v=abc", true)]
    [InlineData("https://notyoutube.com/", false)]
    [InlineData("http://www.youtube.com/", false)]
    public void AppliesHttpsDomainBoundary(string target, bool allowed)
    {
        var result = CreatePolicy().Evaluate(new ProposedAction(ActionKind.NavigateWeb, target), SafeContext());

        Assert.Equal(allowed, result.Allowed);
    }

    [Theory]
    [InlineData(@"C:\Safe\notes.txt", ActionKind.OpenFile, true)]
    [InlineData(@"C:\Safe\unsafe.exe", ActionKind.OpenFile, false)]
    [InlineData(@"C:\Outside", ActionKind.BrowseFolder, false)]
    public void AppliesFileRootAndExtensionBoundaries(string target, ActionKind kind, bool allowed)
    {
        Assert.Equal(allowed, CreatePolicy().Evaluate(new ProposedAction(kind, target), SafeContext()).Allowed);
    }

    private static ActionPolicy CreatePolicy() => new(new RunnerOptions
    {
        AllowedDomains = ["https://youtube.com"],
        AllowedFolderRoots = [@"C:\Safe"],
        AllowedExtensions = [".txt"],
        AllowedApplications = [@"C:\Windows\System32\notepad.exe"],
        ActivityWeights = new Dictionary<string, double> { ["idle"] = 1 }
    });

    private static ObservedContext SafeContext() => new(true, "runner", null, null);
}
