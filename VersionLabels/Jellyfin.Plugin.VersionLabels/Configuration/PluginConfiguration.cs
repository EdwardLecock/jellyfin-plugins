using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.VersionLabels.Configuration;

/// <summary>
/// Plugin configuration.
/// </summary>
public class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>
    /// The default label template.
    /// </summary>
    public const string DefaultTemplate = "{name} · {resolution} {hdr} · {languages}";

    /// <summary>
    /// Gets or sets a value indicating whether version names are rewritten.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the label template. See the configuration page for placeholders.
    /// </summary>
    public string Template { get; set; } = DefaultTemplate;

    /// <summary>
    /// Gets or sets a value indicating whether only items with more than one version are relabelled.
    /// </summary>
    public bool OnlyWhenMultipleVersions { get; set; } = true;
}
