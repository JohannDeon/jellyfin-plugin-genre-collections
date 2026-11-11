using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.GenreCollections.Services;
using MediaBrowser.Model.Tasks;

namespace Jellyfin.Plugin.GenreCollections.Tasks;

/// <summary>
/// Scheduled task that rebuilds the genre collections (also runnable by hand from the dashboard).
/// </summary>
public class GenreCollectionsTask : IScheduledTask
{
    private readonly GenreCollectionService _service;

    /// <summary>
    /// Initializes a new instance of the <see cref="GenreCollectionsTask"/> class.
    /// </summary>
    /// <param name="service">Genre collection service.</param>
    public GenreCollectionsTask(GenreCollectionService service)
    {
        _service = service;
    }

    /// <inheritdoc />
    public string Name => "Mettre à jour les collections par genre";

    /// <inheritdoc />
    public string Key => "GenreCollectionsSync";

    /// <inheritdoc />
    public string Description => "Crée et met à jour une collection par genre pour les films et les séries.";

    /// <inheritdoc />
    public string Category => "Genre Collections";

    /// <inheritdoc />
    public Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
        => _service.SyncAsync(progress, cancellationToken);

    /// <inheritdoc />
    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
    {
        yield return new TaskTriggerInfo
        {
            Type = TaskTriggerInfoType.DailyTrigger,
            TimeOfDayTicks = TimeSpan.FromHours(4).Ticks
        };
    }
}
