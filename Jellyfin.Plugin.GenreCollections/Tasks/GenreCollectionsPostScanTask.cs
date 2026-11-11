using System;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.GenreCollections.Services;
using MediaBrowser.Controller.Library;

namespace Jellyfin.Plugin.GenreCollections.Tasks;

/// <summary>
/// Runs the genre collection sync right after every library scan, so new movies and series land in their collections.
/// </summary>
public class GenreCollectionsPostScanTask : ILibraryPostScanTask
{
    private readonly GenreCollectionService _service;

    /// <summary>
    /// Initializes a new instance of the <see cref="GenreCollectionsPostScanTask"/> class.
    /// </summary>
    /// <param name="service">Genre collection service.</param>
    public GenreCollectionsPostScanTask(GenreCollectionService service)
    {
        _service = service;
    }

    /// <inheritdoc />
    public Task Run(IProgress<double> progress, CancellationToken cancellationToken)
        => _service.SyncAsync(progress, cancellationToken);
}
