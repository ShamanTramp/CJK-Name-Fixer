using System.Text;

namespace Jellyfin.Plugin.CjkNameFixer.Core;

/// <summary>
/// Detects Han, Hiragana, Katakana and Hangul characters.
/// </summary>
public static class CjkDetector
{
    /// <summary>
    /// Returns whether <paramref name="value"/> contains a character from a CJK script.
    /// </summary>
    /// <param name="value">The value to inspect.</param>
    /// <returns><see langword="true"/> when a CJK character is present.</returns>
    public static bool ContainsCjk(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return false;
        }

        foreach (var rune in value.EnumerateRunes())
        {
            var codePoint = rune.Value;
            if (IsHan(codePoint)
                || IsHiragana(codePoint)
                || IsKatakana(codePoint)
                || IsHangul(codePoint))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsHan(int value) =>
        value is >= 0x3400 and <= 0x4DBF
        or >= 0x4E00 and <= 0x9FFF
        or >= 0xF900 and <= 0xFAFF
        or >= 0x20000 and <= 0x2FA1F
        or >= 0x30000 and <= 0x323AF;

    private static bool IsHiragana(int value) => value is >= 0x3040 and <= 0x309F;

    private static bool IsKatakana(int value) =>
        value is >= 0x30A0 and <= 0x30FF
        or >= 0x31F0 and <= 0x31FF
        or >= 0xFF66 and <= 0xFF9F;

    private static bool IsHangul(int value) =>
        value is >= 0x1100 and <= 0x11FF
        or >= 0x3130 and <= 0x318F
        or >= 0xA960 and <= 0xA97F
        or >= 0xAC00 and <= 0xD7AF
        or >= 0xD7B0 and <= 0xD7FF;
}
