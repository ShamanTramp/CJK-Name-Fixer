using Jellyfin.Plugin.CjkNameFixer.Core;

namespace Jellyfin.Plugin.CjkNameFixer.Tests;

public sealed class PersonNameFixerTests
{
    [Fact]
    public async Task FixAsync_UpdatesEligiblePeopleAndSkipsLockedMissingIdAndRomanizedNames()
    {
        var repository = new FakeRepository(
            new PersonName(Guid.NewGuid(), "김수현", "101", false),
            new PersonName(Guid.NewGuid(), "周星驰", null, false),
            new PersonName(Guid.NewGuid(), "木村拓哉", "103", true),
            new PersonName(Guid.NewGuid(), "Kimura Takuya", "104", false));
        var resolver = new FakeResolver(new Dictionary<string, string?> { ["101"] = "Kim Soo-hyun" });
        var fixer = new PersonNameFixer(repository, resolver, (_, _) => Task.CompletedTask);

        var summary = await fixer.FixAsync(new NameFixOptions(DryRun: false, TimeSpan.Zero), null, null, CancellationToken.None);

        Assert.Equal(new PersonFixSummary(4, 1, 1, 0, 3, 0), summary);
        Assert.Equal("Kim Soo-hyun", repository.UpdatedNames.Single().Value);
        Assert.Equal(new[] { "101" }, resolver.Calls);
    }

    [Fact]
    public async Task FixAsync_DryRunNeverWrites()
    {
        var repository = new FakeRepository(new PersonName(Guid.NewGuid(), "김수현", "101", false));
        var fixer = new PersonNameFixer(
            repository,
            new FakeResolver(new Dictionary<string, string?> { ["101"] = "Kim Soo-hyun" }),
            (_, _) => Task.CompletedTask);

        var summary = await fixer.FixAsync(new NameFixOptions(DryRun: true, TimeSpan.Zero), null, null, CancellationToken.None);

        Assert.Equal(1, summary.WouldChange);
        Assert.Empty(repository.UpdatedNames);
    }

    [Fact]
    public async Task FixAsync_OnlyScansPeopleSelectedForNewMedia()
    {
        var selectedId = Guid.NewGuid();
        var repository = new FakeRepository(
            new PersonName(selectedId, "김수현", "101", false),
            new PersonName(Guid.NewGuid(), "周星驰", "102", false));
        var resolver = new FakeResolver(new Dictionary<string, string?>());
        var fixer = new PersonNameFixer(repository, resolver, (_, _) => Task.CompletedTask);

        var summary = await fixer.FixAsync(
            new NameFixOptions(DryRun: true, TimeSpan.Zero), null, null, CancellationToken.None, [selectedId]);

        Assert.Equal(new PersonFixSummary(1, 1, 0, 0, 1, 0), summary);
        Assert.Equal(new[] { "101" }, resolver.Calls);
    }

    [Fact]
    public async Task FixAsync_ContinuesAfterOneResolverFailure()
    {
        var repository = new FakeRepository(
            new PersonName(Guid.NewGuid(), "김수현", "101", false),
            new PersonName(Guid.NewGuid(), "周星驰", "102", false));
        var resolver = new FakeResolver(new Dictionary<string, string?> { ["102"] = "Stephen Chow" })
        {
            FailedIds = ["101"]
        };
        var fixer = new PersonNameFixer(repository, resolver, (_, _) => Task.CompletedTask);

        var summary = await fixer.FixAsync(new NameFixOptions(DryRun: false, TimeSpan.Zero), null, null, CancellationToken.None);

        Assert.Equal(1, summary.Failed);
        Assert.Equal(1, summary.Changed);
        Assert.Single(repository.UpdatedNames);
    }

    [Fact]
    public async Task FixAsync_CancelsBeforeReadingPeople()
    {
        var repository = new FakeRepository();
        var fixer = new PersonNameFixer(repository, new FakeResolver([]));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            fixer.FixAsync(new NameFixOptions(DryRun: true, TimeSpan.Zero), null, null, cancellation.Token));
        Assert.False(repository.Read);
    }

    private sealed class FakeRepository(params PersonName[] people) : IPersonNameRepository
    {
        public bool Read { get; private set; }

        public Dictionary<Guid, string> UpdatedNames { get; } = [];

        public Task<IReadOnlyList<PersonName>> GetPeopleAsync(CancellationToken cancellationToken, IReadOnlyCollection<Guid>? personIds = null)
        {
            Read = true;
            var selected = personIds is null ? people : people.Where(person => personIds.Contains(person.Id)).ToArray();
            return Task.FromResult<IReadOnlyList<PersonName>>(selected);
        }

        public Task UpdateNameAsync(Guid personId, string newName, CancellationToken cancellationToken)
        {
            UpdatedNames[personId] = newName;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeResolver(Dictionary<string, string?> names) : ITmdbNameResolver
    {
        public List<string> Calls { get; } = [];

        public HashSet<string> FailedIds { get; init; } = [];

        public Task<string?> ResolveAsync(string tmdbId, CancellationToken cancellationToken)
        {
            Calls.Add(tmdbId);
            return FailedIds.Contains(tmdbId)
                ? Task.FromException<string?>(new HttpRequestException("simulated error"))
                : Task.FromResult(names.GetValueOrDefault(tmdbId));
        }
    }
}
