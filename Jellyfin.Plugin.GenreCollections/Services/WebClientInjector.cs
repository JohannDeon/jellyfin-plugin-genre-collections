using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Common.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.GenreCollections.Services;

/// <summary>
/// Adds (on startup) and removes (on shutdown) a single script tag in the web client's index.html.
/// No CSS is injected: the rows reuse the web client's own classes so the active theme keeps control of the look.
/// </summary>
public class WebClientInjector : IHostedService
{
    /// <summary>
    /// Script tag inserted before &lt;/body&gt;.
    /// </summary>
    public const string ScriptTag = "<script data-plugin=\"GenreCollections\" defer src=\"../GenreCollections/ClientScript\"></script>";

    private readonly IApplicationPaths _paths;
    private readonly ILogger<WebClientInjector> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="WebClientInjector"/> class.
    /// </summary>
    /// <param name="paths">Application paths.</param>
    /// <param name="logger">Logger.</param>
    public WebClientInjector(IApplicationPaths paths, ILogger<WebClientInjector> logger)
    {
        _paths = paths;
        _logger = logger;
    }

    /// <summary>
    /// Adds the script tag to an index.html content if missing.
    /// </summary>
    /// <param name="html">index.html content.</param>
    /// <returns>Updated content.</returns>
    public static string AddTag(string html)
    {
        if (html.Contains(ScriptTag, StringComparison.Ordinal))
        {
            return html;
        }

        var idx = html.LastIndexOf("</body>", StringComparison.OrdinalIgnoreCase);
        return idx < 0 ? html + ScriptTag : html.Insert(idx, ScriptTag);
    }

    /// <summary>
    /// Removes the script tag from an index.html content.
    /// </summary>
    /// <param name="html">index.html content.</param>
    /// <returns>Updated content.</returns>
    public static string RemoveTag(string html) => html.Replace(ScriptTag, string.Empty, StringComparison.Ordinal);

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        Rewrite(AddTag, "added to");
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken)
    {
        // Removed on shutdown so that uninstalling the plugin leaves index.html untouched.
        Rewrite(RemoveTag, "removed from");
        return Task.CompletedTask;
    }

    private void Rewrite(Func<string, string> transform, string verb)
    {
        var webPath = _paths.WebPath;
        if (string.IsNullOrEmpty(webPath))
        {
            _logger.LogWarning("Web client path is unknown, home screen rows will not be shown");
            return;
        }

        var indexPath = Path.Combine(webPath, "index.html");
        try
        {
            if (!File.Exists(indexPath))
            {
                _logger.LogWarning("{Path} not found (is the web client hosted by Jellyfin?), home screen rows will not be shown", indexPath);
                return;
            }

            var original = File.ReadAllText(indexPath);
            var updated = transform(original);
            if (!string.Equals(original, updated, StringComparison.Ordinal))
            {
                File.WriteAllText(indexPath, updated);
                _logger.LogInformation("Genre Collections script {Verb} {Path}", verb, indexPath);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(
                ex,
                "Cannot write {Path}. Give the Jellyfin user write access to it to enable the home screen rows (collections still work)",
                indexPath);
        }
    }
}
