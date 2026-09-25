using GhostUserRunner.Core.Session;

namespace GhostUserRunner.Core.Tests.Session;

public sealed class TrayWindowPolicyTests
{
    [Fact]
    public void StartingSessionHidesControlWindowToTray()
    {
        var policy = new TrayWindowPolicy();
        policy.OnSessionStarted();
        Assert.Equal(ControlWindowVisibility.TrayOnly, policy.Visibility);
    }

    [Fact]
    public void ShowRequestRestoresControlWindow()
    {
        var policy = new TrayWindowPolicy();
        policy.OnSessionStarted();
        policy.OnShowRequested();
        Assert.Equal(ControlWindowVisibility.Visible, policy.Visibility);
    }
}
