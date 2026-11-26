using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Common.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.GenreCollections.Injection;

/// <summary>
/// Removes, once at startup, the script tag that versions before 1.4 wrote in the web client's index.html.
/// The script is now added by <see cref="IndexHtmlInjectionMiddleware"/> when the page is served.
/// </summary>
public sealed class LegacyIndexCleanup : IHostedService
{
    /// <summary>
    /// Tag written on disk by versions before 1.4.
    /// </summary>
    public const string LegacyTag = "<script data-plugin=\"GenreCollections\" defer src=\"../GenreCollections/ClientScript\"></script>";

    private readonly IApplicationPaths _paths;
    private readonly ILogger<LegacyIndexCleanup> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="LegacyIndexCleanup"/> class.
    /// </summary>
    /// <param name="paths">Application paths.</param>
    /// <param name="logger">Logger.</param>
    public LegacyIndexCleanup(IApplicationPaths paths, ILogger<LegacyIndexCleanup> logger)
    {
        _paths = paths;
        _logger = logger;
    }

    /// <summary>
    /// Removes the legacy tag from an index.html content.
    /// </summary>
    /// <param name="html">index.html content.</param>
    /// <returns>Cleaned content.</returns>
    public static string RemoveLegacyTag(string html) => html.Replace(LegacyTag, string.Empty, StringComparison.Ordinal);

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            var webPath = _paths.WebPath;
            var indexPath = string.IsNullOrEmpty(webPath) ? null : Path.Combine(webPath, "index.html");
            if (indexPath is not null && File.Exists(indexPath))
            {
                var original = File.ReadAllText(indexPath);
                var cleaned = RemoveLegacyTag(original);
                if (!string.Equals(original, cleaned, StringComparison.Ordinal))
                {
                    File.WriteAllText(indexPath, cleaned);
                    _logger.LogInformation("Genre Collections: old script tag removed from {Path}", indexPath);
                }
            }
        }
        catch (Exception ex)
        {
            // Harmless if it stays: the middleware does not add the script twice.
            _logger.LogDebug(ex, "Genre Collections: could not clean index.html");
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
