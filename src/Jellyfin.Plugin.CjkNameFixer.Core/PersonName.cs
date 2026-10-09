namespace Jellyfin.Plugin.CjkNameFixer.Core;

/// <summary>
/// The fields needed to decide whether a Jellyfin person can be fixed.
/// </summary>
/// <param name="Id">The Jellyfin person identifier.</param>
/// <param name="Name">The current display name.</param>
/// <param name="TmdbId">The TMDb person identifier, if known.</param>
/// <param name="NameIsLocked">Whether Jellyfin protects the name from metadata edits.</param>
public sealed record PersonName(Guid Id, string Name, string? TmdbId, bool NameIsLocked);
