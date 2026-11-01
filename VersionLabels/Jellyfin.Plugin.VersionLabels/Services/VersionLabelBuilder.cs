using System.Collections.Concurrent;
using System.Globalization;
using System.Text.RegularExpressions;
using MediaBrowser.Model.Globalization;

namespace Jellyfin.Plugin.VersionLabels.Services;

/// <summary>
/// Stream data needed to build a label (independent of where it came from: JSON or library).
/// </summary>
public sealed record StreamData(
    string Type,
    string? Codec,
    string? Profile,
    string? Language,
    int Width,
    int Height,
    int Channels,
    bool IsDefault,
    string? VideoRangeType,
    string? VideoRange);

/// <summary>
/// Builds version labels from a template.
/// </summary>
public partial class VersionLabelBuilder
{
    private const string SeparatorChars = "·|/•–-";

    private readonly ILocalizationManager _localization;
    private readonly ConcurrentDictionary<string, string> _languageNames = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Initializes a new instance of the <see cref="VersionLabelBuilder"/> class.
    /// </summary>
    /// <param name="localization">Localization manager used for language names.</param>
    public VersionLabelBuilder(ILocalizationManager localization)
    {
        _localization = localization;
    }

    [GeneratedRegex(@"\{(\w+)\}")]
    private static partial Regex PlaceholderRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();

    [GeneratedRegex(@"\[\s*\]|\(\s*\)")]
    private static partial Regex EmptyBracketsRegex();

    [GeneratedRegex(@"(\s*[·|/•–-]\s*)(?:[·|/•–-]\s*)+")]
    private static partial Regex RepeatedSeparatorRegex();

    /// <summary>
    /// Builds the label for one version.
    /// </summary>
    /// <param name="template">The label template.</param>
    /// <param name="originalName">The name Jellyfin gave the version.</param>
    /// <param name="streams">The version's media streams.</param>
    /// <param name="sizeBytes">The file size in bytes, if known.</param>
    /// <returns>The label, or null when nothing useful could be built.</returns>
    public string? Build(string template, string? originalName, IReadOnlyList<StreamData> streams, long? sizeBytes)
    {
        if (string.IsNullOrWhiteSpace(template))
        {
            return null;
        }

        var video = streams.FirstOrDefault(s => s.Type.Equals("Video", StringComparison.OrdinalIgnoreCase));
        var audio = streams.Where(s => s.Type.Equals("Audio", StringComparison.OrdinalIgnoreCase)).ToList();
        var subtitles = streams.Where(s => s.Type.Equals("Subtitle", StringComparison.OrdinalIgnoreCase)).ToList();
        var defaultAudio = audio.FirstOrDefault(a => a.IsDefault) ?? audio.FirstOrDefault();

        var audioLangCodes = DistinctLanguages(audio);
        var subLangCodes = DistinctLanguages(subtitles);

        string Resolve(string key) => key.ToLowerInvariant() switch
        {
            "name" => originalName ?? string.Empty,
            "resolution" => video == null ? string.Empty : FormatResolution(video.Width, video.Height),
            "hdr" => video == null ? string.Empty : FormatHdr(video),
            "videocodec" => FormatVideoCodec(video?.Codec),
            "audiocodec" => defaultAudio == null ? string.Empty : FormatAudioCodec(defaultAudio),
            "channels" => defaultAudio == null ? string.Empty : FormatChannels(defaultAudio.Channels),
            "languages" => string.Join(", ", audioLangCodes.Select(GetLanguageName)),
            "languagecodes" => string.Join("/", audioLangCodes.Select(c => c.ToUpperInvariant())),
            "defaultlanguage" => string.IsNullOrEmpty(defaultAudio?.Language) || IsUndefined(defaultAudio.Language)
                ? string.Empty
                : GetLanguageName(defaultAudio.Language),
            "subtitles" => string.Join(", ", subLangCodes.Select(GetLanguageName)),
            "size" => FormatSize(sizeBytes),
            _ => "{" + key + "}"
        };

        var label = PlaceholderRegex().Replace(template, m => Resolve(m.Groups[1].Value));
        label = Cleanup(label);

        return string.IsNullOrWhiteSpace(label) ? null : label;
    }

    /// <summary>
    /// Removes the gaps that empty placeholders leave behind (double separators, empty brackets, etc.).
    /// </summary>
    private static string Cleanup(string label)
    {
        label = EmptyBracketsRegex().Replace(label, string.Empty);
        label = WhitespaceRegex().Replace(label, " ");
        label = RepeatedSeparatorRegex().Replace(label, "$1");
        return label.Trim().Trim(SeparatorChars.ToCharArray()).Trim();
    }

    private static List<string> DistinctLanguages(IEnumerable<StreamData> streams)
    {
        return streams
            .Select(s => s.Language)
            .Where(l => !string.IsNullOrWhiteSpace(l) && !IsUndefined(l!))
            .Select(l => l!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool IsUndefined(string lang) =>
        lang.Equals("und", StringComparison.OrdinalIgnoreCase) || lang.Equals("unk", StringComparison.OrdinalIgnoreCase);

    private string GetLanguageName(string code)
    {
        return _languageNames.GetOrAdd(code, c =>
        {
            try
            {
                var info = _localization.FindLanguageInfo(c);
                var name = info?.DisplayName;
                if (!string.IsNullOrWhiteSpace(name))
                {
                    // e.g. "Dutch; Flemish" -> "Dutch"
                    return name.Split(';', ',')[0].Trim();
                }
            }
            catch
            {
                // fall through to the code itself
            }

            return c.ToUpperInvariant();
        });
    }

    private static string FormatResolution(int width, int height)
    {
        var max = Math.Max(width, height);
        if (max >= 3800) return "4K";
        if (max >= 1900) return "1080p";
        if (max >= 1260) return "720p";
        if (max > 0) return "SD";
        return string.Empty;
    }

    private static string FormatHdr(StreamData video)
    {
        var range = video.VideoRangeType ?? string.Empty;
        if (range.StartsWith("DOVI", StringComparison.OrdinalIgnoreCase)) return "DV";
        if (range.Equals("HDR10Plus", StringComparison.OrdinalIgnoreCase)) return "HDR10+";
        if (range.Equals("HDR10", StringComparison.OrdinalIgnoreCase)) return "HDR10";
        if (range.Equals("HLG", StringComparison.OrdinalIgnoreCase)) return "HLG";
        if (string.Equals(video.VideoRange, "HDR", StringComparison.OrdinalIgnoreCase)) return "HDR";
        return string.Empty;
    }

    private static string FormatVideoCodec(string? codec)
    {
        return (codec ?? string.Empty).ToLowerInvariant() switch
        {
            "" => string.Empty,
            "hevc" or "h265" => "HEVC",
            "h264" or "avc" => "H.264",
            "av1" => "AV1",
            "vp9" => "VP9",
            "mpeg2video" => "MPEG-2",
            "vc1" => "VC-1",
            var c => c.ToUpperInvariant()
        };
    }

    private static string FormatAudioCodec(StreamData audio)
    {
        var codec = (audio.Codec ?? string.Empty).ToLowerInvariant();
        var profile = audio.Profile ?? string.Empty;
        var atmos = profile.Contains("atmos", StringComparison.OrdinalIgnoreCase);

        var name = codec switch
        {
            "" => string.Empty,
            "truehd" => "TrueHD",
            "eac3" => "DD+",
            "ac3" => "DD",
            "dts" when profile.Contains("DTS:X", StringComparison.OrdinalIgnoreCase)
                || profile.Contains("DTS-X", StringComparison.OrdinalIgnoreCase) => "DTS:X",
            "dts" when profile.Contains("MA", StringComparison.OrdinalIgnoreCase) => "DTS-HD MA",
            "dts" when profile.Contains("HRA", StringComparison.OrdinalIgnoreCase)
                || profile.Contains("HR", StringComparison.OrdinalIgnoreCase) => "DTS-HD HRA",
            "dts" => "DTS",
            "aac" => "AAC",
            "flac" => "FLAC",
            "opus" => "Opus",
            "mp3" => "MP3",
            "vorbis" => "Vorbis",
            _ when codec.StartsWith("pcm", StringComparison.Ordinal) => "PCM",
            _ => codec.ToUpperInvariant()
        };

        return atmos && name.Length > 0 ? name + " Atmos" : name;
    }

    private static string FormatChannels(int channels)
    {
        return channels switch
        {
            <= 0 => string.Empty,
            1 => "Mono",
            2 => "Stereo",
            6 => "5.1",
            8 => "7.1",
            _ => channels.ToString(CultureInfo.InvariantCulture) + "ch"
        };
    }

    private static string FormatSize(long? bytes)
    {
        if (bytes is null or <= 0) return string.Empty;
        var gb = bytes.Value / 1024d / 1024d / 1024d;
        return gb >= 1
            ? gb.ToString("0.0", CultureInfo.InvariantCulture) + " GB"
            : (bytes.Value / 1024d / 1024d).ToString("0", CultureInfo.InvariantCulture) + " MB";
    }
}
