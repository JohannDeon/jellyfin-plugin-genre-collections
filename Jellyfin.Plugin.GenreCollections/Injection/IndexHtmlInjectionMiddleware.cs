using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Net.Http.Headers;

namespace Jellyfin.Plugin.GenreCollections.Injection;

/// <summary>
/// Adds the home screen script to Jellyfin Web's index.html as it is served.
/// Nothing is written on disk, so it works with read-only web folders (Docker) and leaves no trace once the plugin is removed.
/// No CSS is injected: the rows reuse the web client's own classes so the active theme keeps control of the look.
/// </summary>
public sealed class IndexHtmlInjectionMiddleware
{
    /// <summary>
    /// Attribute that identifies the plugin's script tag (also used by the old on-disk tag).
    /// </summary>
    public const string Marker = "data-plugin=\"GenreCollections\"";

    private readonly RequestDelegate _next;
    private readonly ILogger<IndexHtmlInjectionMiddleware> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="IndexHtmlInjectionMiddleware"/> class.
    /// </summary>
    /// <param name="next">Next middleware.</param>
    /// <param name="logger">Logger.</param>
    public IndexHtmlInjectionMiddleware(RequestDelegate next, ILogger<IndexHtmlInjectionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    /// <summary>
    /// Handles a request.
    /// </summary>
    /// <param name="context">HTTP context.</param>
    /// <returns>A task.</returns>
    public async Task InvokeAsync(HttpContext context)
    {
        var request = context.Request;
        var fullPath = (request.PathBase.Value ?? string.Empty) + (request.Path.Value ?? string.Empty);
        if (!HttpMethods.IsGet(request.Method) || !TryGetPrefix(fullPath, out var prefix))
        {
            await _next(context).ConfigureAwait(false);
            return;
        }

        // The full, plain body is needed: no compression, no 304.
        request.Headers.Remove(HeaderNames.AcceptEncoding);
        request.Headers.Remove(HeaderNames.IfNoneMatch);
        request.Headers.Remove(HeaderNames.IfModifiedSince);

        var originalBody = context.Response.Body;
        await using var buffer = new MemoryStream();
        context.Response.Body = buffer;
        try
        {
            await _next(context).ConfigureAwait(false);
        }
        finally
        {
            context.Response.Body = originalBody;
        }

        buffer.Position = 0;
        var response = context.Response;
        var isHtml = response.ContentType?.Contains("text/html", StringComparison.OrdinalIgnoreCase) == true;
        if (response.StatusCode == StatusCodes.Status200OK && isHtml && !response.Headers.ContainsKey(HeaderNames.ContentEncoding))
        {
            byte[] bytes;
            try
            {
                bytes = Encoding.UTF8.GetBytes(Inject(Encoding.UTF8.GetString(buffer.ToArray()), prefix));
            }
            catch (Exception ex)
            {
                // Never break the web client: serve the page untouched.
                _logger.LogWarning(ex, "Genre Collections: unable to add the home screen script");
                bytes = buffer.ToArray();
            }

            response.ContentLength = bytes.Length;
            response.Headers.Remove(HeaderNames.ETag);
            response.Headers.Remove(HeaderNames.LastModified);
            response.Headers.CacheControl = "no-cache";
            await originalBody.WriteAsync(bytes).ConfigureAwait(false);
        }
        else
        {
            await buffer.CopyToAsync(originalBody).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Returns true for Jellyfin Web's entry page and gives the URL prefix before "/web" (base URL).
    /// </summary>
    /// <param name="path">Request path, base path included.</param>
    /// <param name="prefix">Base URL.</param>
    /// <returns>Whether the path is the web client's index page.</returns>
    public static bool TryGetPrefix(string path, out string prefix)
    {
        prefix = string.Empty;
        foreach (var suffix in new[] { "/web/index.html", "/web/" })
        {
            if (path.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                prefix = path[..^suffix.Length];
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Adds the script tag before &lt;/body&gt; unless it is already there.
    /// </summary>
    /// <param name="html">index.html content.</param>
    /// <param name="prefix">Base URL.</param>
    /// <returns>Updated content.</returns>
    public static string Inject(string html, string prefix)
    {
        if (html.Contains(Marker, StringComparison.Ordinal))
        {
            return html;
        }

        var version = typeof(IndexHtmlInjectionMiddleware).Assembly.GetName().Version?.ToString() ?? "1";
        var tag = "<script " + Marker + " defer src=\"" + prefix + "/GenreCollections/ClientScript?v=" + version + "\"></script>";
        var idx = html.LastIndexOf("</body>", StringComparison.OrdinalIgnoreCase);
        return idx < 0 ? html + tag : html.Insert(idx, tag);
    }
}
