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
    private readonly ConcurrentDictionary<Guid, string> _creditNames = new();

    /// <inheritdoc />
    public Task<IReadOnlyList<PersonName>> GetPeopleAsync(CancellationToken cancellationToken, IReadOnlyCollection<Guid>? personIds = null)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _people.Clear();
        _creditNames.Clear();

        // The credits table is Jellyfin's source of truth for the name attached to each media item.
        // Read it as well as the by-name Person entities: older plugin versions changed only the
        // entity, leaving the CJK name in media credits and making those people disappear in DTOs.
        var namedPeople = personIds is { Count: > 0 }
            ? personIds.Distinct()
                .Select(libraryManager.GetItemById<Person>)
                .Where(person => person is not null)
                .Select(person => (Person: person!, CreditName: person!.Name))
                .ToList()
            : libraryManager.GetPeople(new InternalPeopleQuery())
                .Select(personInfo => (Person: libraryManager.GetPerson(personInfo.Name), CreditName: personInfo.Name))
                .Where(entry => entry.Person is not null)
                .Select(entry => (Person: entry.Person!, entry.CreditName))
                .ToList();

        var results = new List<PersonName>(namedPeople.Count);
        foreach (var entry in namedPeople.DistinctBy(entry => entry.Person.Id))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var person = entry.Person;
            _people[person.Id] = person;
            _creditNames[person.Id] = entry.CreditName;
            person.ProviderIds.TryGetValue(MetadataProvider.Tmdb.ToString(), out var tmdbId);
            var isNameLocked = person.LockedFields?.Contains(MetadataField.Name) ?? false;
            results.Add(new PersonName(person.Id, entry.CreditName, tmdbId, isNameLocked));
        }

        return Task.FromResult<IReadOnlyList<PersonName>>(results);
    }

    /// <inheritdoc />
    public async Task UpdateNameAsync(Guid personId, string newName, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!_people.TryGetValue(personId, out var person) || !_creditNames.TryGetValue(personId, out var oldCreditName))
        {
            throw new InvalidOperationException($"Person {personId} was not part of the current library snapshot.");
        }

        var targetPerson = libraryManager.GetPerson(newName);
        if (targetPerson is not null && person.ProviderIds.Any(pair =>
                targetPerson.ProviderIds.TryGetValue(pair.Key, out var existingId)
                && !string.Equals(existingId, pair.Value, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException($"Cannot rename '{oldCreditName}' to '{newName}': the target name already has conflicting provider metadata.");
        }

        // Jellyfin stores media credits separately from by-name Person items. Update every matching
        // credit first so Jellyfin can resolve the new name to its Person item when building DTOs.
        var items = libraryManager.GetItemList(new InternalItemsQuery
        {
            Person = oldCreditName,
            EnableTotalRecordCount = false
        });
        var changedItems = 0;
        foreach (var item in items)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var people = libraryManager.GetPeople(item);
            var renamedPeople = people.Select(credit =>
            {
                if (!string.Equals(credit.Name, oldCreditName, StringComparison.OrdinalIgnoreCase))
                {
                    return credit;
                }

                var renamedCredit = new PersonInfo
                {
                    Id = Guid.NewGuid(),
                    Name = newName,
                    Role = credit.Role,
                    Type = credit.Type,
                    SortOrder = credit.SortOrder,
                    ProviderIds = new Dictionary<string, string>(person.ProviderIds, StringComparer.OrdinalIgnoreCase)
                };
                return renamedCredit;
            }).ToArray();

            if (renamedPeople.Where((credit, index) => !ReferenceEquals(credit, people[index])).Any())
            {
                await libraryManager.UpdatePeopleAsync(item, renamedPeople, cancellationToken).ConfigureAwait(false);
                changedItems++;
            }
        }

        // UpdatePeopleAsync creates (or finds) the romanized by-name item. Copy the original
        // provider IDs onto it; do not mutate the old item whose ID/path is based on the CJK name.
        targetPerson ??= libraryManager.GetPerson(newName);
        if (targetPerson is not null)
        {
            var changedProviderIds = false;
            foreach (var (provider, providerId) in person.ProviderIds)
            {
                if (!targetPerson.ProviderIds.TryGetValue(provider, out var targetId))
                {
                    targetPerson.ProviderIds[provider] = providerId;
                    changedProviderIds = true;
                }
                else if (!string.Equals(targetId, providerId, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException($"Cannot transfer {provider} ID to '{newName}': the target already has a different provider ID.");
                }
            }

            if (changedProviderIds)
            {
                await libraryManager.UpdateItemAsync(targetPerson, null!, ItemUpdateType.MetadataEdit, cancellationToken).ConfigureAwait(false);
            }
        }

        if (changedItems == 0)
        {
            throw new InvalidOperationException($"No media credits were found for '{oldCreditName}'; the person was not renamed.");
        }
    }
}
