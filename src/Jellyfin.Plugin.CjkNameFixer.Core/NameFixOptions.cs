namespace Jellyfin.Plugin.CjkNameFixer.Core;

/// <summary>Controls one person-name fixing run.</summary>
/// <param name="DryRun">When true, report candidate names without saving them.</param>
/// <param name="Throttle">Delay between TMDb requests.</param>
public sealed record NameFixOptions(bool DryRun, TimeSpan Throttle);
