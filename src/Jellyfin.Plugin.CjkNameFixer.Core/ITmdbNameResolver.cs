namespace Jellyfin.Plugin.CjkNameFixer.Core;

/// <summary>Resolves a TMDb person identifier to the English display name.</summary>
public interface ITmdbNameResolver
{
    /// <summary>Gets an English display name, or <see langword="null"/> when none exists.</summary>
    Task<string?> ResolveAsync(string tmdbId, CancellationToken cancellationToken);
}
