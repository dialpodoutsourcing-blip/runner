using GhostUserRunner.Infrastructure.Browser;

namespace GhostUserRunner.Infrastructure.Tests.Browser;

public sealed class BrowserPolicyInterceptorTests
{
    private readonly BrowserPolicyInterceptor _policy = new(["https://youtube.com", "https://www.google.com"]);

    [Theory]
    [InlineData("https://youtube.com/watch?v=abc", true)]
    [InlineData("https://www.youtube.com/results", true)]
    [InlineData("https://www.google.com/search?q=nature", true)]
    [InlineData("https://evil.example/", false)]
    [InlineData("http://youtube.com/", false)]
    [InlineData("https://youtube.com.evil.example/", false)]
    public void AllowsOnlyConfiguredHttpsDomainBoundaries(string url, bool allowed) => Assert.Equal(allowed, _policy.IsAllowed(url));

    [Theory]
    [InlineData("https://youtube.com/login")]
    [InlineData("https://youtube.com/signin")]
    [InlineData("https://youtube.com/upload")]
    [InlineData("https://youtube.com/checkout")]
    public void RejectsSensitiveRoutes(string url) => Assert.False(_policy.IsAllowed(url));
}
