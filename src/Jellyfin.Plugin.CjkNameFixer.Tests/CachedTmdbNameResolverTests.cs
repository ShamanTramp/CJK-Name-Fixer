using Jellyfin.Plugin.CjkNameFixer.Core;

namespace Jellyfin.Plugin.CjkNameFixer.Tests;

public sealed class CachedTmdbNameResolverTests
{
    [Fact]
    public async Task ResolveAsync_ReusesPositiveAndEmptyResultsUntilExpiry()
    {
        var now = DateTimeOffset.UtcNow;
        var inner = new FakeResolver(new Dictionary<string, string?> { ["101"] = "Kim Soo-hyun", ["102"] = null });
        var resolver = new CachedTmdbNameResolver(inner, TimeSpan.FromHours(12), () => now);

        Assert.Equal("Kim Soo-hyun", await resolver.ResolveAsync("101", CancellationToken.None));
        Assert.Equal("Kim Soo-hyun", await resolver.ResolveAsync("101", CancellationToken.None));
        Assert.Null(await resolver.ResolveAsync("102", CancellationToken.None));
        Assert.Null(await resolver.ResolveAsync("102", CancellationToken.None));
        Assert.Equal(new[] { "101", "102" }, inner.Calls);

        now = now.AddHours(13);
        Assert.Equal("Kim Soo-hyun", await resolver.ResolveAsync("101", CancellationToken.None));
        Assert.Equal(new[] { "101", "102", "101" }, inner.Calls);
    }

    [Fact]
    public async Task ResolveAsync_DoesNotCacheFailures()
    {
        var inner = new FakeResolver(new Dictionary<string, string?>()) { FailFirstCall = true };
        var resolver = new CachedTmdbNameResolver(inner, TimeSpan.FromHours(12));

        await Assert.ThrowsAsync<HttpRequestException>(() => resolver.ResolveAsync("101", CancellationToken.None));
        Assert.Null(await resolver.ResolveAsync("101", CancellationToken.None));
        Assert.Equal(2, inner.Calls.Count);
    }

    private sealed class FakeResolver(Dictionary<string, string?> names) : ITmdbNameResolver
    {
        public List<string> Calls { get; } = [];

        public bool FailFirstCall { get; init; }

        public Task<string?> ResolveAsync(string tmdbId, CancellationToken cancellationToken)
        {
            Calls.Add(tmdbId);
            if (FailFirstCall && Calls.Count == 1)
            {
                return Task.FromException<string?>(new HttpRequestException("simulated error"));
            }

            return Task.FromResult(names.GetValueOrDefault(tmdbId));
        }
    }
}
