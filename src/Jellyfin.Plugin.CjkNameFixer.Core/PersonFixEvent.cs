namespace Jellyfin.Plugin.CjkNameFixer.Core;

/// <summary>Severity of a fixing event.</summary>
public enum PersonFixEventLevel
{
    Information,
    Warning,
    Error
}

/// <summary>A diagnostic emitted while a fixing run processes a person.</summary>
/// <param name="Level">The severity.</param>
/// <param name="Message">A human-readable message.</param>
public sealed record PersonFixEvent(PersonFixEventLevel Level, string Message);

/// <summary>Counts the outcomes of one fixing run.</summary>
/// <param name="Scanned">People inspected.</param>
/// <param name="Candidates">People with CJK names and TMDb identifiers.</param>
/// <param name="Changed">Names saved to Jellyfin.</param>
/// <param name="WouldChange">Names that would be changed in dry-run mode.</param>
/// <param name="Skipped">People skipped for a known reason.</param>
/// <param name="Failed">People that failed to resolve or update.</param>
public sealed record PersonFixSummary(int Scanned, int Candidates, int Changed, int WouldChange, int Skipped, int Failed);
