using System.Text.RegularExpressions;
using Jellyfin.Plugin.VersionLabels.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.VersionLabels.Middleware;

/// <summary>
/// Intercepts item and playback-info responses and relabels their media sources (versions).
/// Works for every client, because the names are changed in the API response itself.
/// </summary>
public partial class VersionLabelMiddleware
{
    private const long MaxBufferedBytes = 32 * 1024 * 1024;

    private readonly RequestDelegate _next;
    private readonly ILogger<VersionLabelMiddleware> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="VersionLabelMiddleware"/> class.
    /// </summary>
    /// <param name="next">The next middleware.</param>
    /// <param name="logger">The logger.</param>
    public VersionLabelMiddleware(RequestDelegate next, ILogger<VersionLabelMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    // /Items/{id} and /Users/{userId}/Items/{id}
    [GeneratedRegex(@"/Items/[0-9a-f]{32}$|/Items/[0-9a-f-]{36}$", RegexOptions.IgnoreCase)]
    private static partial Regex SingleItemRegex();

    // /Items/{id}/PlaybackInfo
    [GeneratedRegex(@"/Items/([0-9a-f]{32}|[0-9a-f-]{36})/PlaybackInfo$", RegexOptions.IgnoreCase)]
    private static partial Regex PlaybackInfoRegex();

    // /Items and /Users/{userId}/Items (only when the client asked for MediaSources)
    [GeneratedRegex(@"/Items$", RegexOptions.IgnoreCase)]
    private static partial Regex ItemListRegex();

    /// <summary>
    /// Handles a request.
    /// </summary>
    /// <param name="context">The HTTP context.</param>
    /// <param name="rewriter">The JSON rewriter.</param>
    /// <returns>A task.</returns>
    public async Task InvokeAsync(HttpContext context, MediaSourceJsonRewriter rewriter)
    {
        var config = Plugin.Instance?.Configuration;
        if (config == null || !config.Enabled || string.IsNullOrWhiteSpace(config.Template) || !ShouldHandle(context.Request))
        {
            await _next(context).ConfigureAwait(false);
            return;
        }

        // We need the plain JSON, so make sure the response isn't compressed further down the pipeline.
        context.Request.Headers.Remove("Accept-Encoding");

        var originalBody = context.Response.Body;
        using var buffer = new MemoryStream();
        context.Response.Body = buffer;

        try
        {
            await _next(context).ConfigureAwait(false);
        }
        finally
        {
            context.Response.Body = originalBody;
        }

        byte[]? rewritten = null;
        var response = context.Response;
        if (response.StatusCode == StatusCodes.Status200OK
            && (response.ContentType?.Contains("json", StringComparison.OrdinalIgnoreCase) ?? false)
            && !response.Headers.ContainsKey("Content-Encoding")
            && buffer.Length > 0
            && buffer.Length <= MaxBufferedBytes)
        {
            try
            {
                rewritten = rewriter.Rewrite(
                    new ReadOnlySpan<byte>(buffer.GetBuffer(), 0, (int)buffer.Length),
                    config.Template,
                    config.OnlyWhenMultipleVersions);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Version Labels: failed to rewrite response for {Path}", context.Request.Path);
            }
        }

        if (rewritten != null)
        {
            response.ContentLength = rewritten.Length;
            await originalBody.WriteAsync(rewritten).ConfigureAwait(false);
            return;
        }

        if (response.ContentLength.HasValue)
        {
            response.ContentLength = buffer.Length;
        }

        buffer.Position = 0;
        await buffer.CopyToAsync(originalBody).ConfigureAwait(false);
    }

    private static bool ShouldHandle(HttpRequest request)
    {
        var path = request.Path.Value;
        if (string.IsNullOrEmpty(path))
        {
            return false;
        }

        if (PlaybackInfoRegex().IsMatch(path))
        {
            return HttpMethods.IsGet(request.Method) || HttpMethods.IsPost(request.Method);
        }

        if (!HttpMethods.IsGet(request.Method))
        {
            return false;
        }

        if (SingleItemRegex().IsMatch(path))
        {
            return true;
        }

        if (ItemListRegex().IsMatch(path))
        {
            var fields = request.Query["fields"].ToString();
            return fields.Contains("MediaSources", StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }
}
