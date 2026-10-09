namespace Jellyfin.Plugin.CjkNameFixer.Core;

/// <summary>Caches TMDb name lookups and serializes cache misses to avoid duplicate API traffic.</summary>
public sealed class CachedTmdbNameResolver : ITmdbNameResolver
{
    private readonly ITmdbNameResolver _inner;
    private readonly TimeSpan _cacheDuration;
    private readonly Func<DateTimeOffset> _utcNow;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly Dictionary<string, CacheEntry> _cache = new(StringComparer.Ordinal);

    /// <summary>Initializes a new instance of the <see cref="CachedTmdbNameResolver"/> class.</summary>
    /// <param name="inner">The underlying Jellyfin TMDb client adapter.</param>
    /// <param name="cacheDuration">How long successful and empty lookups are reused.</param>
    /// <param name="utcNow">Optional clock for deterministic tests.</param>
    public CachedTmdbNameResolver(ITmdbNameResolver inner, TimeSpan cacheDuration, Func<DateTimeOffset>? utcNow = null)
    {
        ArgumentNullException.ThrowIfNull(inner);
        if (cacheDuration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(cacheDuration), "The cache duration must be positive.");
        }

        _inner = inner;
        _cacheDuration = cacheDuration;
        _utcNow = utcNow ?? (() => DateTimeOffset.UtcNow);
    }

    /// <inheritdoc />
    public async Task<string?> ResolveAsync(string tmdbId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tmdbId);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var now = _utcNow();
            if (_cache.TryGetValue(tmdbId, out var entry) && entry.ExpiresAt > now)
            {
                return entry.Name;
            }

            _cache.Remove(tmdbId);
            var name = await _inner.ResolveAsync(tmdbId, cancellationToken).ConfigureAwait(false);
            _cache[tmdbId] = new CacheEntry(name, now + _cacheDuration);
            return name;
        }
        finally
        {
            _gate.Release();
        }
    }

    private sealed record CacheEntry(string? Name, DateTimeOffset ExpiresAt);
}
