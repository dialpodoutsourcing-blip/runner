using System.Globalization;

namespace GhostUserRunner.Infrastructure.Browser;

public sealed class BrowserPolicyInterceptor
{
    private static readonly string[] BlockedSegments = ["login", "signin", "upload", "checkout", "purchase", "account"];
    private readonly string[] _hosts;

    public BrowserPolicyInterceptor(IEnumerable<string> allowedOrigins) =>
        _hosts = allowedOrigins.Select(origin => new IdnMapping().GetAscii(new Uri(origin).Host).TrimEnd('.')).ToArray();

    public bool IsAllowed(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps) return false;
        var host = new IdnMapping().GetAscii(uri.Host).TrimEnd('.');
        if (!_hosts.Any(allowed => host.Equals(allowed, StringComparison.OrdinalIgnoreCase) || host.EndsWith('.' + allowed, StringComparison.OrdinalIgnoreCase))) return false;
        var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return !segments.Any(segment => BlockedSegments.Contains(segment, StringComparer.OrdinalIgnoreCase));
    }
}
