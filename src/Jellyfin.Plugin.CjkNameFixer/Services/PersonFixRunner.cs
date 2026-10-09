using Jellyfin.Plugin.CjkNameFixer.Configuration;
using Jellyfin.Plugin.CjkNameFixer.Core;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.CjkNameFixer.Services;

/// <summary>Runs the policy with current plugin settings and Jellyfin logging.</summary>
public sealed class PersonFixRunner(PersonNameFixer fixer, ILogger<PersonFixRunner> logger)
{
    /// <summary>Runs one scan with the current configuration.</summary>
    public async Task<PersonFixSummary> RunAsync(IProgress<double>? progress, CancellationToken cancellationToken, IReadOnlyCollection<Guid>? personIds = null)
    {
        var configuration = Plugin.Instance?.Configuration ?? new PluginConfiguration();
        var throttle = Math.Clamp(configuration.ThrottleMs, 0, 60_000);
        var summary = await fixer.FixAsync(
            new NameFixOptions(DryRun: false, TimeSpan.FromMilliseconds(throttle)),
            progress,
            LogEvent,
            cancellationToken,
            personIds).ConfigureAwait(false);

        logger.LogInformation(
            "CJK name scan complete: {Scanned} scanned, {Candidates} candidates, {Changed} changed, {WouldChange} would change, {Skipped} skipped, {Failed} failed.",
            summary.Scanned,
            summary.Candidates,
            summary.Changed,
            summary.WouldChange,
            summary.Skipped,
            summary.Failed);
        return summary;
    }

    private void LogEvent(PersonFixEvent fixEvent)
    {
        switch (fixEvent.Level)
        {
            case PersonFixEventLevel.Information:
                logger.LogInformation("{Message}", fixEvent.Message);
                break;
            case PersonFixEventLevel.Warning:
                logger.LogWarning("{Message}", fixEvent.Message);
                break;
            case PersonFixEventLevel.Error:
                logger.LogError("{Message}", fixEvent.Message);
                break;
            default:
                logger.LogDebug("{Message}", fixEvent.Message);
                break;
        }
    }
}
