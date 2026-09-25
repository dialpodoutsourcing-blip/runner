namespace GhostUserRunner.Core.Session;

public enum ControlWindowVisibility { Visible, TrayOnly }

public sealed class TrayWindowPolicy
{
    public ControlWindowVisibility Visibility { get; private set; } = ControlWindowVisibility.Visible;
    public void OnSessionStarted() => Visibility = ControlWindowVisibility.TrayOnly;
    public void OnShowRequested() => Visibility = ControlWindowVisibility.Visible;
}
