using System.Reflection;
using Jellyfin.Plugin.CjkNameFixer.Services;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Entities;

namespace Jellyfin.Plugin.CjkNameFixer.Tests;

public sealed class JellyfinPersonNameRepositoryTests
{
    [Fact]
    public async Task GetPeopleAsync_UsesCreditNameWhenPersonEntityWasPreviouslyRenamed()
    {
        var fixture = new LibraryFixture("김수현", "Kim Soo-hyun");
        var repository = new JellyfinPersonNameRepository(fixture.LibraryManager);

        var people = await repository.GetPeopleAsync(CancellationToken.None);

        var person = Assert.Single(people);
        Assert.Equal("김수현", person.Name);
        Assert.Equal("101", person.TmdbId);
    }

    [Fact]
    public async Task UpdateNameAsync_RenamesCreditsAndPreservesTmdbIdentity()
    {
        var fixture = new LibraryFixture("김수현", "Kim Soo-hyun");
        var repository = new JellyfinPersonNameRepository(fixture.LibraryManager);
        var person = Assert.Single(await repository.GetPeopleAsync(CancellationToken.None));

        await repository.UpdateNameAsync(person.Id, "Hyun Bin", CancellationToken.None);

        var renamedCredit = Assert.Single(fixture.SavedCredits);
        Assert.Equal("Hyun Bin", renamedCredit.Name);
        Assert.Equal("Actor", renamedCredit.Role);
        Assert.Equal("김수현", fixture.QueriedPersonName);
        Assert.NotEqual(fixture.OriginalCredit.Id, renamedCredit.Id);
        Assert.Equal("101", renamedCredit.ProviderIds[MetadataProvider.Tmdb.ToString()]);
        Assert.Equal("101", fixture.ByName["Hyun Bin"].ProviderIds[MetadataProvider.Tmdb.ToString()]);
    }

    public sealed class LibraryFixture
    {
        public LibraryFixture(string creditName, string entityName)
        {
            OriginalCredit = new PersonInfo
            {
                Id = Guid.NewGuid(),
                Name = creditName,
                Role = "Actor",
                Type = Jellyfin.Data.Enums.PersonKind.Actor
            };
            var oldEntity = CreatePerson(Guid.NewGuid(), entityName, "101");
            var movie = new Movie { Id = Guid.NewGuid(), Name = "Example movie" };
            ByName = new Dictionary<string, Person>(StringComparer.OrdinalIgnoreCase)
            {
                [creditName] = oldEntity
            };
            CreditsByItem[movie.Id] = [OriginalCredit];

            var proxy = DispatchProxy.Create<ILibraryManager, LibraryManagerDispatchProxy>();
            var dispatcher = (LibraryManagerDispatchProxy)(object)proxy;
            dispatcher.Fixture = this;
            LibraryManager = proxy;
        }

        public ILibraryManager LibraryManager { get; }

        public PersonInfo OriginalCredit { get; }

        public Dictionary<string, Person> ByName { get; }

        public Dictionary<Guid, IReadOnlyList<PersonInfo>> CreditsByItem { get; } = [];

        public IReadOnlyList<PersonInfo> SavedCredits { get; private set; } = [];

        public string? QueriedPersonName { get; private set; }

        private static Person CreatePerson(Guid id, string name, string tmdbId)
        {
            var person = new Person { Id = id, Name = name };
            person.ProviderIds[MetadataProvider.Tmdb.ToString()] = tmdbId;
            return person;
        }

        public class LibraryManagerDispatchProxy : DispatchProxy
        {
            public LibraryFixture Fixture { get; set; } = null!;

            protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
            {
                ArgumentNullException.ThrowIfNull(targetMethod);
                args ??= [];
                switch (targetMethod.Name)
                {
                    case "GetPeople" when args[0] is InternalPeopleQuery:
                        return new[] { Fixture.OriginalCredit };
                    case "GetPerson" when args[0] is string name:
                        return Fixture.ByName.GetValueOrDefault(name);
                    case "GetItemList" when args[0] is InternalItemsQuery query:
                        Fixture.QueriedPersonName = query.Person;
                        var movieId = Fixture.CreditsByItem.Keys.Single();
                        return new BaseItem[] { new Movie { Id = movieId, Name = "Example movie" } };
                    case "GetPeople" when args[0] is BaseItem item:
                        return Fixture.CreditsByItem[item.Id];
                    case "UpdatePeopleAsync" when args[0] is BaseItem item && args[1] is IReadOnlyList<PersonInfo> people:
                        Fixture.SavedCredits = people;
                        Fixture.CreditsByItem[item.Id] = people;
                        foreach (var credit in people)
                        {
                            if (!Fixture.ByName.ContainsKey(credit.Name))
                            {
                                Fixture.ByName[credit.Name] = CreatePerson(Guid.NewGuid(), credit.Name, credit.ProviderIds.GetValueOrDefault(MetadataProvider.Tmdb.ToString()) ?? string.Empty);
                            }
                        }

                        return Task.CompletedTask;
                    case "GetItemById":
                        return Fixture.ByName.Values.FirstOrDefault(person => person.Id.Equals(args[0]));
                    case "UpdateItemAsync":
                        return Task.CompletedTask;
                    default:
                        throw new NotSupportedException($"Unexpected ILibraryManager call: {targetMethod.Name}.");
                }
            }
        }
    }
}
