using System.Globalization;
using GhostUserRunner.Core.Actions;
using GhostUserRunner.Core.Configuration;

namespace GhostUserRunner.Core.Safety;

public interface IActionPolicy
{
    PolicyDecision Evaluate(ProposedAction action, ObservedContext context);
}

public sealed class ActionPolicy(RunnerOptions options) : IActionPolicy
{
    private static readonly HashSet<ActionKind> BlockedKinds =
    [
        ActionKind.DeleteFile, ActionKind.EditFile, ActionKind.MoveFile, ActionKind.CopyFile,
        ActionKind.Download, ActionKind.Upload, ActionKind.SubmitForm, ActionKind.SendMessage,
        ActionKind.Purchase, ActionKind.Authenticate
    ];

    public PolicyDecision Evaluate(ProposedAction action, ObservedContext context)
    {
        if (!context.IsRecognizedWindow)
        {
            return PolicyDecision.Deny("window.unknown");
        }

        if (BlockedKinds.Contains(action.Kind))
        {
            return PolicyDecision.Deny("action.blocked");
        }

        if (action.Kind is ActionKind.NavigateWeb or ActionKind.SearchWeb or ActionKind.WatchVideo)
        {
            if (!Uri.TryCreate(action.Target, UriKind.Absolute, out var target) ||
                target.Scheme != Uri.UriSchemeHttps || !IsAllowedHost(target.Host))
            {
                return PolicyDecision.Deny("domain.not_allowed");
            }
        }

        if (action.Kind is ActionKind.BrowseFolder or ActionKind.OpenFile)
        {
            if (!Path.IsPathFullyQualified(action.Target)) return PolicyDecision.Deny("path.not_absolute");
            var target = Path.GetFullPath(action.Target).TrimEnd(Path.DirectorySeparatorChar);
            var beneathRoot = options.AllowedFolderRoots.Any(root =>
            {
                var normalizedRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar);
                return target.Equals(normalizedRoot, StringComparison.OrdinalIgnoreCase) ||
                    target.StartsWith(normalizedRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
            });
            if (!beneathRoot) return PolicyDecision.Deny("path.not_allowed");
            if (action.Kind == ActionKind.OpenFile && !options.AllowedExtensions.Contains(Path.GetExtension(target), StringComparer.OrdinalIgnoreCase))
                return PolicyDecision.Deny("extension.not_allowed");
        }

        return PolicyDecision.Permit();
    }

    private bool IsAllowedHost(string host)
    {
        var asciiHost = new IdnMapping().GetAscii(host).TrimEnd('.');
        foreach (var entry in options.AllowedDomains)
        {
            if (!Uri.TryCreate(entry, UriKind.Absolute, out var allowed)) continue;
            var allowedHost = new IdnMapping().GetAscii(allowed.Host).TrimEnd('.');
            if (asciiHost.Equals(allowedHost, StringComparison.OrdinalIgnoreCase) ||
                asciiHost.EndsWith('.' + allowedHost, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }
}
