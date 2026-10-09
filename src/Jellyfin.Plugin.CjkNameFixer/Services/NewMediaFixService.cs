using System.Collections.Concurrent;
using System.Threading.Channels;
using Jellyfin.Plugin.CjkNameFixer.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.CjkNameFixer.Services;

/// <summary>Responds to newly added media events without polling or rescanning the whole library.</summary>
public sealed class NewMediaFixService(
    ILibraryManager libraryManager,
    PersonFixRunner runner,
    ILogger<NewMediaFixService> logger) : IHostedService
{
    private readonly Channel<BaseItem> _queue = Channel.CreateUnbounded<BaseItem>(new UnboundedChannelOptions { SingleReader = true });
    private readonly ConcurrentDictionary<Guid, byte> _pendingItems = new();
    private CancellationTokenSource? _stopping;
    private Task? _worker;

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _stopping = new CancellationTokenSource();
        libraryManager.ItemAdded += OnItemAdded;
        _worker = ProcessQueueAsync(_stopping.Token);
        logger.LogInformation("CJK name fixer is listening for newly added movies, series, and episodes.");
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        libraryManager.ItemAdded -= OnItemAdded;
        _queue.Writer.TryComplete();
        _stopping?.Cancel();
        if (_worker is not null)
        {
            try
            {
                await _worker.WaitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (_stopping?.IsCancellationRequested == true)
            {
                // The host is stopping; queued, not-yet-processed media will be seen on the next add event.
            }
        }

        _stopping?.Dispose();
        _stopping = null;
    }

    private void OnItemAdded(object? sender, ItemChangeEventArgs eventArgs)
    {
        if (Plugin.Instance?.Configuration.EnableNewMediaFix != true
            || eventArgs.Item is not (Movie or Series or Episode)
            || !_pendingItems.TryAdd(eventArgs.Item.Id, 0))
        {
            return;
        }

        if (!_queue.Writer.TryWrite(eventArgs.Item))
        {
            _pendingItems.TryRemove(eventArgs.Item.Id, out _);
        }
    }

    private async Task ProcessQueueAsync(CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var addedItem in _queue.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
            {
                try
                {
                    // Let Jellyfin's metadata providers finish associating credits with the new media.
                    await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken).ConfigureAwait(false);
                    var item = libraryManager.GetItemById(addedItem.Id) ?? addedItem;
                    var personIds = libraryManager.GetPeople(item)
                        .Select(person => person.Id)
                        .Where(id => id != Guid.Empty)
                        .Distinct()
                        .ToArray();

                    if (personIds.Length > 0)
                    {
                        logger.LogInformation("Checking {PersonCount} people associated with newly added media '{MediaName}'.", personIds.Length, item.Name);
                        await runner.RunAsync(null, cancellationToken, personIds).ConfigureAwait(false);
                    }
                    else
                    {
                        logger.LogDebug("No people are associated with newly added media '{MediaName}' yet.", item.Name);
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    logger.LogError(exception, "Could not check CJK person names for newly added media '{MediaName}'.", addedItem.Name);
                }
                finally
                {
                    _pendingItems.TryRemove(addedItem.Id, out _);
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Normal host shutdown.
        }
    }
}
