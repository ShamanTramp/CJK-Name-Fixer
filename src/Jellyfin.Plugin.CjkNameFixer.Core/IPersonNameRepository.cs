namespace Jellyfin.Plugin.CjkNameFixer.Core;

/// <summary>
/// Reads and updates Jellyfin people without tying the fixing policy to Jellyfin APIs.
/// </summary>
public interface IPersonNameRepository
{
    /// <summary>Gets all known people or only people associated with the requested media items.</summary>
    Task<IReadOnlyList<PersonName>> GetPeopleAsync(CancellationToken cancellationToken, IReadOnlyCollection<Guid>? personIds = null);

    /// <summary>Updates one person's display name.</summary>
    Task UpdateNameAsync(Guid personId, string newName, CancellationToken cancellationToken);
}
