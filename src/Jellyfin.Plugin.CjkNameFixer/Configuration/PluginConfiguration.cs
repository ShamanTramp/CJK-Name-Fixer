using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.CjkNameFixer.Configuration;

/// <summary>Settings for CJK person-name correction.</summary>
public sealed class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>Gets or sets whether added media triggers a targeted CJK people check.</summary>
    public bool EnableNewMediaFix { get; set; } = true;

    /// <summary>Gets or sets the delay between TMDb requests, in milliseconds.</summary>
    public int ThrottleMs { get; set; } = 500;
    /// <summary>Gets or sets an optional TMDb v3 API key used instead of Jellyfin's shared provider key.</summary>
    public string TmdbApiKeyOverride { get; set; } = string.Empty;
}
