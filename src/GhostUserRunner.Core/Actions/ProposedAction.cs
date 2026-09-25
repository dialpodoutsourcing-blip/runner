namespace GhostUserRunner.Core.Actions;

public enum ActionKind
{
    Idle,
    NavigateWeb,
    SearchWeb,
    WatchVideo,
    Scroll,
    SwitchTab,
    BrowseFolder,
    OpenFile,
    CloseWindow,
    MinimizeWindow,
    RestoreWindow,
    DeleteFile,
    EditFile,
    MoveFile,
    CopyFile,
    Download,
    Upload,
    SubmitForm,
    SendMessage,
    Purchase,
    Authenticate
}

public sealed record ProposedAction(ActionKind Kind, string Target, IReadOnlyDictionary<string, string>? Parameters = null);

public sealed record ObservedContext(bool IsRecognizedWindow, string ProcessName, Uri? BrowserUri, string? FileSystemPath);
