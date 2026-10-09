using System.Collections.Concurrent;
using Jellyfin.Plugin.CjkNameFixer.Core;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Entities;

namespace Jellyfin.Plugin.CjkNameFixer.Services;

/// <summary>Adapts Jellyfin's people store to the plugin's person-name repository.</summary>
public sealed class JellyfinPersonNameRepository(ILibraryManager libraryManager) : IPersonNameRepository
{
    private readonly ConcurrentDictionary<Guid, Person> _people = new();

    /// <inheritdoc />
    public Task<IReadOnlyList<PersonName>> GetPeopleAsync(CancellationToken cancellationToken, IReadOnlyCollection<Guid>? personIds = null)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _people.Clear();
        var people = personIds is { Count: > 0 }
            ? personIds.Distinct().Select(libraryManager.GetItemById<Person>).Where(person => person is not null).Cast<Person>().ToList()
            : libraryManager.GetPeopleItems(new InternalPeopleQuery());
        var results = new List<PersonName>(people.Count);
        foreach (var person in people)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _people[person.Id] = person;
            person.ProviderIds.TryGetValue(MetadataProvider.Tmdb.ToString(), out var tmdbId);
            var isNameLocked = person.LockedFields?.Contains(MetadataField.Name) ?? false;
            results.Add(new PersonName(person.Id, person.Name, tmdbId, isNameLocked));
        }

        return Task.FromResult<IReadOnlyList<PersonName>>(results);
    }

    /// <inheritdoc />
    public async Task UpdateNameAsync(Guid personId, string newName, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!_people.TryGetValue(personId, out var person))
        {
            throw new InvalidOperationException($"Person {personId} was not part of the current library snapshot.");
        }

        person.Name = newName;
        await libraryManager.UpdateItemAsync(person, null!, ItemUpdateType.MetadataEdit, cancellationToken).ConfigureAwait(false);
    }
}
