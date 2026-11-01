using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Jellyfin.Plugin.VersionLabels.Services;

/// <summary>
/// Rewrites the "Name" of every entry in any "MediaSources" array of a Jellyfin JSON response.
/// </summary>
public class MediaSourceJsonRewriter
{
    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private readonly VersionLabelBuilder _builder;

    /// <summary>
    /// Initializes a new instance of the <see cref="MediaSourceJsonRewriter"/> class.
    /// </summary>
    /// <param name="builder">The label builder.</param>
    public MediaSourceJsonRewriter(VersionLabelBuilder builder)
    {
        _builder = builder;
    }

    /// <summary>
    /// Rewrites the JSON document.
    /// </summary>
    /// <param name="json">The response body.</param>
    /// <param name="template">The label template.</param>
    /// <param name="onlyWhenMultiple">Only relabel items with more than one version.</param>
    /// <returns>The new body, or null when nothing changed.</returns>
    public byte[]? Rewrite(ReadOnlySpan<byte> json, string template, bool onlyWhenMultiple)
    {
        if (json.IsEmpty || json.IndexOf("\"MediaSources\""u8) < 0)
        {
            return null;
        }

        var root = JsonNode.Parse(json);
        if (root == null || !Visit(root, template, onlyWhenMultiple))
        {
            return null;
        }

        return JsonSerializer.SerializeToUtf8Bytes(root, WriteOptions);
    }

    private bool Visit(JsonNode node, string template, bool onlyWhenMultiple)
    {
        var changed = false;
        switch (node)
        {
            case JsonObject obj:
                foreach (var (key, value) in obj.ToList())
                {
                    if (value == null)
                    {
                        continue;
                    }

                    if (key == "MediaSources" && value is JsonArray sources)
                    {
                        changed |= RelabelSources(sources, template, onlyWhenMultiple);
                    }
                    else if (value is JsonObject or JsonArray)
                    {
                        changed |= Visit(value, template, onlyWhenMultiple);
                    }
                }

                break;
            case JsonArray arr:
                foreach (var child in arr)
                {
                    if (child is JsonObject or JsonArray)
                    {
                        changed |= Visit(child, template, onlyWhenMultiple);
                    }
                }

                break;
        }

        return changed;
    }

    private bool RelabelSources(JsonArray sources, string template, bool onlyWhenMultiple)
    {
        if (onlyWhenMultiple && sources.Count < 2)
        {
            return false;
        }

        var changed = false;
        foreach (var source in sources.OfType<JsonObject>())
        {
            var streams = new List<StreamData>();
            if (source["MediaStreams"] is JsonArray streamArray)
            {
                foreach (var s in streamArray.OfType<JsonObject>())
                {
                    streams.Add(new StreamData(
                        GetString(s, "Type") ?? string.Empty,
                        GetString(s, "Codec"),
                        GetString(s, "Profile"),
                        GetString(s, "Language"),
                        GetInt(s, "Width"),
                        GetInt(s, "Height"),
                        GetInt(s, "Channels"),
                        GetBool(s, "IsDefault"),
                        GetString(s, "VideoRangeType"),
                        GetString(s, "VideoRange")));
                }
            }

            if (streams.Count == 0)
            {
                continue;
            }

            var originalName = GetString(source, "Name");
            var label = _builder.Build(template, originalName, streams, GetLong(source, "Size"));
            if (label != null && label != originalName)
            {
                source["Name"] = label;
                changed = true;
            }
        }

        return changed;
    }

    private static string? GetString(JsonObject o, string key) =>
        o[key] is JsonValue v && v.TryGetValue<string>(out var s) ? s : null;

    private static int GetInt(JsonObject o, string key) =>
        o[key] is JsonValue v && v.TryGetValue<int>(out var i) ? i : 0;

    private static long? GetLong(JsonObject o, string key) =>
        o[key] is JsonValue v && v.TryGetValue<long>(out var l) ? l : null;

    private static bool GetBool(JsonObject o, string key) =>
        o[key] is JsonValue v && v.TryGetValue<bool>(out var b) && b;
}
