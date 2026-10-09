namespace Jellyfin.Plugin.CjkNameFixer.Core;

/// <summary>Applies the CJK name correction policy to the people indexed by Jellyfin.</summary>
public sealed class PersonNameFixer
{
    private readonly IPersonNameRepository _repository;
    private readonly ITmdbNameResolver _resolver;
    private readonly Func<TimeSpan, CancellationToken, Task> _delayAsync;
    private readonly SemaphoreSlim _runLock = new(1, 1);

    /// <summary>Initializes a new instance of the <see cref="PersonNameFixer"/> class.</summary>
    /// <param name="repository">Jellyfin people storage.</param>
    /// <param name="resolver">TMDb name resolver.</param>
    /// <param name="delayAsync">Optional delay implementation for throttling and deterministic tests.</param>
    public PersonNameFixer(
        IPersonNameRepository repository,
        ITmdbNameResolver resolver,
        Func<TimeSpan, CancellationToken, Task>? delayAsync = null)
    {
        _repository = repository;
        _resolver = resolver;
        _delayAsync = delayAsync ?? Task.Delay;
    }

    /// <summary>Scans people and fixes eligible names.</summary>
    /// <param name="options">Settings for this run.</param>
    /// <param name="progress">Optional percent progress report.</param>
    /// <param name="log">Optional diagnostic sink.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A summary of the run.</returns>
    public async Task<PersonFixSummary> FixAsync(
        NameFixOptions options,
        IProgress<double>? progress,
        Action<PersonFixEvent>? log,
        CancellationToken cancellationToken,
        IReadOnlyCollection<Guid>? personIds = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (options.Throttle < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "The request throttle cannot be negative.");
        }

        await _runLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var people = await _repository.GetPeopleAsync(cancellationToken, personIds).ConfigureAwait(false);
            var candidates = 0;
            var changed = 0;
            var wouldChange = 0;
            var skipped = 0;
            var failed = 0;
            var requestCount = 0;

            for (var index = 0; index < people.Count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var person = people[index];

                if (!CjkDetector.ContainsCjk(person.Name))
                {
                    skipped++;
                    progress?.Report(PercentComplete(index + 1, people.Count));
                    continue;
                }

                if (person.NameIsLocked)
                {
                    skipped++;
                    log?.Invoke(new PersonFixEvent(PersonFixEventLevel.Information, $"Skipped locked name '{person.Name}'."));
                    progress?.Report(PercentComplete(index + 1, people.Count));
                    continue;
                }

                if (string.IsNullOrWhiteSpace(person.TmdbId))
                {
                    skipped++;
                    log?.Invoke(new PersonFixEvent(PersonFixEventLevel.Warning, $"Skipped '{person.Name}': no TMDb person ID."));
                    progress?.Report(PercentComplete(index + 1, people.Count));
                    continue;
                }

                candidates++;
                try
                {
                    if (requestCount > 0 && options.Throttle > TimeSpan.Zero)
                    {
                        await _delayAsync(options.Throttle, cancellationToken).ConfigureAwait(false);
                    }

                    requestCount++;
                    var resolvedName = await _resolver.ResolveAsync(person.TmdbId, cancellationToken).ConfigureAwait(false);
                    if (string.IsNullOrWhiteSpace(resolvedName)
                        || string.Equals(resolvedName.Trim(), person.Name, StringComparison.OrdinalIgnoreCase)
                        || CjkDetector.ContainsCjk(resolvedName))
                    {
                        skipped++;
                        log?.Invoke(new PersonFixEvent(PersonFixEventLevel.Information, $"Skipped '{person.Name}': TMDb returned no distinct romanized name."));
                    }
                    else if (options.DryRun)
                    {
                        wouldChange++;
                        log?.Invoke(new PersonFixEvent(PersonFixEventLevel.Information, $"Dry run: '{person.Name}' would become '{resolvedName.Trim()}'."));
                    }
                    else
                    {
                        await _repository.UpdateNameAsync(person.Id, resolvedName.Trim(), cancellationToken).ConfigureAwait(false);
                        changed++;
                        log?.Invoke(new PersonFixEvent(PersonFixEventLevel.Information, $"Renamed '{person.Name}' to '{resolvedName.Trim()}'."));
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    failed++;
                    log?.Invoke(new PersonFixEvent(PersonFixEventLevel.Error, $"Failed to fix '{person.Name}': {exception.Message}"));
                }

                progress?.Report(PercentComplete(index + 1, people.Count));
            }

            progress?.Report(100);
            return new PersonFixSummary(people.Count, candidates, changed, wouldChange, skipped, failed);
        }
        finally
        {
            _runLock.Release();
        }
    }

    private static double PercentComplete(int completed, int total) =>
        total == 0 ? 100 : completed * 100d / total;
}
