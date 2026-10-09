using Jellyfin.Plugin.CjkNameFixer.Core;

namespace Jellyfin.Plugin.CjkNameFixer.Services;

/// <summary>Selects the user's TMDb API key when present, otherwise reuses Jellyfin's provider.</summary>
public sealed class ConfiguredTmdbNameResolver(
    ITmdbNameResolver jellyfinResolver,
    ITmdbNameResolver customKeyResolver,
    Func<string?> apiKeyProvider) : ITmdbNameResolver
{
    /// <inheritdoc />
    public Task<string?> ResolveAsync(string tmdbId, CancellationToken cancellationToken) =>
        (string.IsNullOrWhiteSpace(apiKeyProvider()) ? jellyfinResolver : customKeyResolver)
            .ResolveAsync(tmdbId, cancellationToken);
}
