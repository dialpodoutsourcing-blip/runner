using GhostUserRunner.Core.Actions;
using GhostUserRunner.Core.Execution;
using GhostUserRunner.Core.Planning;
using GhostUserRunner.Core.Randomness;
using GhostUserRunner.Infrastructure.Input;
using GhostUserRunner.Infrastructure.Desktop;
using Microsoft.Playwright;

namespace GhostUserRunner.Infrastructure.Browser;

public sealed class BrowserAdapter : IActionExecutor, IAsyncDisposable
{
    private readonly BrowserPolicyInterceptor _policy;
    private readonly string _profilePath;
    private readonly HumanInputEngine? _humanInput;
    private readonly IWindowFocusCoordinator? _focus;
    private IPlaywright? _playwright;
    private IBrowserContext? _context;
    private IPage? _page;
    private readonly string _windowMarker = $"GhostUserRunner-{Guid.NewGuid():N}";

    public BrowserAdapter(IEnumerable<string> allowedDomains, string profilePath, HumanInputEngine? humanInput = null, IWindowFocusCoordinator? focus = null)
    {
        _policy = new(allowedDomains);
        _profilePath = profilePath;
        _humanInput = humanInput;
        _focus = focus;
    }

    public ObservedContext Observe()
    {
        var url = _page?.Url;
        return new(_page is null || string.IsNullOrEmpty(url) || url == "about:blank" || _policy.IsAllowed(url), "browser", Uri.TryCreate(url, UriKind.Absolute, out var uri) ? uri : null, null);
    }

    public async Task<ActionOutcome> ExecuteAsync(ProposedAction action, CancellationToken cancellationToken)
    {
        await EnsureStartedAsync(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        await MarkOwnedPageAsync(cancellationToken);
        if (_focus is not null && !_focus.TryActivateBrowser(_windowMarker)) return new(false, "browser.focus_failed");
        if (action.Kind == ActionKind.Idle)
        {
            await Task.Delay(ActivityDwellTime.Choose(action.Kind, new SeededRandomSource(Random.Shared.Next())), cancellationToken);
            return new(true, "idle");
        }
        if (action.Kind is not (ActionKind.NavigateWeb or ActionKind.SearchWeb or ActionKind.WatchVideo)) return new(false, "browser.unsupported");
        if (!_policy.IsAllowed(action.Target)) return new(false, "browser.denied");

        if (_humanInput is not null)
        {
            GetCursorPos(out var current);
            var target = new System.Drawing.Point(100 + Random.Shared.Next(Math.Max(1, GetSystemMetrics(0) - 200)), 100 + Random.Shared.Next(Math.Max(1, GetSystemMetrics(1) - 200)));
            await _humanInput.MoveAsync(current, target, cancellationToken);
        }

        if (action.Kind == ActionKind.SearchWeb)
        {
            var query = GetQueryValue(action.Target, "q");
            await _page!.GotoAsync("https://www.google.com", new() { WaitUntil = WaitUntilState.DOMContentLoaded, Timeout = 30_000 }).WaitAsync(cancellationToken);
            var search = _page.Locator("textarea[name=q], input[name=q]").First;
            if (!await ActivateAndClickAsync(search, cancellationToken)) return new(false, "browser.target_changed");
            if (_focus is not null && !_focus.TryActivateBrowser(_windowMarker)) return new(false, "browser.focus_lost");
            await _humanInput!.TypeTextAsync(query, cancellationToken);
            await _humanInput.TypeAsync([0x0D], cancellationToken);
            await _page.WaitForLoadStateAsync(LoadState.DOMContentLoaded).WaitAsync(cancellationToken);
        }
        else if (action.Kind == ActionKind.WatchVideo)
        {
            var query = GetQueryValue(action.Target, "search_query");
            await _page!.GotoAsync("https://www.youtube.com", new() { WaitUntil = WaitUntilState.DOMContentLoaded, Timeout = 30_000 }).WaitAsync(cancellationToken);
            var search = _page.Locator("input[name=search_query]").First;
            if (!await ActivateAndClickAsync(search, cancellationToken)) return new(false, "browser.target_changed");
            if (_focus is not null && !_focus.TryActivateBrowser(_windowMarker)) return new(false, "browser.focus_lost");
            await _humanInput!.TypeTextAsync(query, cancellationToken);
            await _humanInput.TypeAsync([0x0D], cancellationToken);
            await _page.WaitForLoadStateAsync(LoadState.DOMContentLoaded).WaitAsync(cancellationToken);
        }
        else
        {
            await _page!.GotoAsync(action.Target, new() { WaitUntil = WaitUntilState.DOMContentLoaded, Timeout = 30_000 }).WaitAsync(cancellationToken);
        }
        if (!_policy.IsAllowed(_page.Url)) return new(false, "browser.redirect_denied");
        if (_humanInput is not null)
        {
            await MarkOwnedPageAsync(cancellationToken);
            if (_focus is not null && !_focus.TryActivateBrowser(_windowMarker)) return new(false, "browser.focus_lost");
            await _humanInput.ScrollAsync(-350, cancellationToken);
        }
        else await _page.Mouse.WheelAsync(0, 350);

        if (action.Kind == ActionKind.WatchVideo)
        {
            var video = _page.Locator("ytd-video-renderer a#thumbnail").First;
            if (await video.CountAsync().WaitAsync(cancellationToken) > 0 && !await ActivateAndClickAsync(video, cancellationToken)) return new(false, "browser.target_changed");
            if (!_policy.IsAllowed(_page.Url)) return new(false, "browser.redirect_denied");
        }
        await Task.Delay(ActivityDwellTime.Choose(action.Kind, new SeededRandomSource(Random.Shared.Next())), cancellationToken);
        return new(true, "browser.complete");
    }

    private async Task<bool> ActivateAndClickAsync(ILocator locator, CancellationToken cancellationToken)
    {
        if (_humanInput is null) { await locator.ClickAsync(); return true; }
        await MarkOwnedPageAsync(cancellationToken);
        if (_focus is not null && !_focus.TryActivateBrowser(_windowMarker)) return false;
        var box = await locator.BoundingBoxAsync().WaitAsync(cancellationToken);
        if (box is null || !await locator.IsVisibleAsync().WaitAsync(cancellationToken)) return false;
        var metrics = await _page!.EvaluateAsync<float[]>("() => [window.screenX, window.screenY, window.outerWidth-window.innerWidth, window.outerHeight-window.innerHeight]").WaitAsync(cancellationToken);
        if (_focus is not null && !_focus.TryActivateBrowser(_windowMarker)) return false;
        var checkedBox = await locator.BoundingBoxAsync().WaitAsync(cancellationToken);
        if (checkedBox is null || Math.Abs(checkedBox.X - box.X) > 2 || Math.Abs(checkedBox.Y - box.Y) > 2) return false;
        var point = new System.Drawing.Point((int)(metrics[0] + metrics[2] / 2 + box.X + box.Width / 2), (int)(metrics[1] + metrics[3] + box.Y + box.Height / 2));
        await _humanInput.ClickAsync(point, cancellationToken);
        return true;
    }

    private Task MarkOwnedPageAsync(CancellationToken cancellationToken) =>
        _page!.EvaluateAsync("marker => document.title = marker", _windowMarker).WaitAsync(cancellationToken);

    private static string GetQueryValue(string url, string name)
    {
        var query = new Uri(url).Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries);
        var pair = query.Select(value => value.Split('=', 2)).FirstOrDefault(parts => Uri.UnescapeDataString(parts[0]).Equals(name, StringComparison.Ordinal));
        return pair is { Length: 2 } ? Uri.UnescapeDataString(pair[1].Replace('+', ' ')) : "nature";
    }

    private async Task EnsureStartedAsync(CancellationToken cancellationToken)
    {
        if (_page is not null) return;
        Directory.CreateDirectory(_profilePath);
        Environment.SetEnvironmentVariable("PLAYWRIGHT_BROWSERS_PATH", "0");
        _playwright = await Playwright.CreateAsync().WaitAsync(cancellationToken);
        using var cancellationRegistration = cancellationToken.Register(() => _playwright?.Dispose());
        _context = await _playwright.Chromium.LaunchPersistentContextAsync(_profilePath, new() { Headless = false, AcceptDownloads = false }).WaitAsync(cancellationToken);
        _context.Page += (_, page) => _ = GuardPageAsync(page);
        _page = _context.Pages.FirstOrDefault() ?? await _context.NewPageAsync().WaitAsync(cancellationToken);
        await GuardPageAsync(_page);
    }

    private async Task GuardPageAsync(IPage page)
    {
        page.Download += (_, download) => _ = download.CancelAsync();
        await page.RouteAsync("**/*", async route =>
        {
            if (route.Request.ResourceType == "document" && !_policy.IsAllowed(route.Request.Url)) await route.AbortAsync();
            else await route.ContinueAsync();
        });
    }

    public async ValueTask DisposeAsync()
    {
        if (_context is not null) await _context.CloseAsync();
        _playwright?.Dispose();
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern bool GetCursorPos(out System.Drawing.Point point);
    [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern int GetSystemMetrics(int index);
}
