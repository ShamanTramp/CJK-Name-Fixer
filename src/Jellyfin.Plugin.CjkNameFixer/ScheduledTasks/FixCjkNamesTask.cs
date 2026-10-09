using Jellyfin.Plugin.CjkNameFixer.Services;
using MediaBrowser.Model.Tasks;

namespace Jellyfin.Plugin.CjkNameFixer.ScheduledTasks;

/// <summary>Provides a manually runnable full scan for CJK person names already in Jellyfin.</summary>
public sealed class FixCjkNamesTask(PersonFixRunner runner) : IScheduledTask, IConfigurableScheduledTask
{
    /// <inheritdoc />
    public string Name => "Fix CJK Person Names";

    /// <inheritdoc />
    public string Key => "CjkNameFixer";

    /// <inheritdoc />
    public string Description => "Manually scan existing Jellyfin people for CJK names. This task has no automatic schedule.";

    /// <inheritdoc />
    public string Category => "Metadata";

    /// <inheritdoc />
    public bool IsHidden => false;

    /// <inheritdoc />
    public bool IsEnabled => true;

    /// <inheritdoc />
    public bool IsLogged => true;

    /// <inheritdoc />
    public Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken) =>
        runner.RunAsync(progress, cancellationToken);

    /// <inheritdoc />
    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers() => [];
}
