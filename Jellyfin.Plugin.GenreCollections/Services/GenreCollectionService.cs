using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Data.Enums;
using Jellyfin.Plugin.GenreCollections.Configuration;
using MediaBrowser.Controller.Collections;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Entities;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.GenreCollections.Services;

/// <summary>
/// Creates, updates and removes the genre collections.
/// </summary>
public class GenreCollectionService
{
    private static readonly SemaphoreSlim SyncLock = new(1, 1);

    private readonly ILibraryManager _libraryManager;
    private readonly ICollectionManager _collectionManager;
    private readonly ILogger<GenreCollectionService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="GenreCollectionService"/> class.
    /// </summary>
    /// <param name="libraryManager">Library manager.</param>
    /// <param name="collectionManager">Collection manager.</param>
    /// <param name="logger">Logger.</param>
    public GenreCollectionService(
        ILibraryManager libraryManager,
        ICollectionManager collectionManager,
        ILogger<GenreCollectionService> logger)
    {
        _libraryManager = libraryManager;
        _collectionManager = collectionManager;
        _logger = logger;
    }

    /// <summary>
    /// Builds the collection key for a genre and a scope ("all", "movies" or "series").
    /// </summary>
    /// <param name="scope">Scope.</param>
    /// <param name="genre">Genre display name.</param>
    /// <returns>Key stored as provider id.</returns>
    public static string CollectionKey(string scope, string genre) => scope + ":" + GenreRules.Key(genre);

    /// <summary>
    /// Returns every collection managed by this plugin.
    /// </summary>
    /// <param name="libraryManager">Library manager.</param>
    /// <returns>Managed collections.</returns>
    public static IReadOnlyList<BoxSet> GetManagedCollections(ILibraryManager libraryManager)
    {
        return libraryManager.GetItemList(new InternalItemsQuery
            {
                IncludeItemTypes = [BaseItemKind.BoxSet],
                Recursive = true
            })
            .OfType<BoxSet>()
            .Where(b => b.ProviderIds is not null && b.ProviderIds.ContainsKey(Plugin.ProviderKey))
            .ToList();
    }

    /// <summary>
    /// Synchronizes the genre collections with the library.
    /// </summary>
    /// <param name="progress">Progress reporter.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task.</returns>
    public async Task SyncAsync(IProgress<double> progress, CancellationToken cancellationToken)
    {
        var config = Plugin.Instance?.Configuration ?? new PluginConfiguration();
        if (!config.EnableGenreCollections)
        {
            _logger.LogInformation("Genre collections are disabled, nothing to do");
            progress.Report(100);
            return;
        }

        await SyncLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await SyncInternalAsync(config, progress, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            SyncLock.Release();
        }
    }

    private async Task SyncInternalAsync(PluginConfiguration config, IProgress<double> progress, CancellationToken cancellationToken)
    {
        var rules = new GenreRules(config.GenreAliases, config.ExcludedGenres);
        var desired = BuildDesiredCollections(config, rules);
        progress.Report(10);

        var existing = GetManagedCollections(_libraryManager);
        var existingByKey = new Dictionary<string, BoxSet>(StringComparer.OrdinalIgnoreCase);
        foreach (var boxSet in existing)
        {
            var key = boxSet.ProviderIds[Plugin.ProviderKey];
            if (!existingByKey.TryAdd(key, boxSet))
            {
                _logger.LogWarning("Duplicate genre collection {Name} ({Key}) found", boxSet.Name, key);
            }
        }

        int done = 0;
        int created = 0, updated = 0, removed = 0;
        foreach (var (key, target) in desired)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (existingByKey.TryGetValue(key, out var boxSet))
            {
                if (await UpdateCollectionAsync(boxSet, target, cancellationToken).ConfigureAwait(false))
                {
                    updated++;
                }

                existingByKey.Remove(key);
            }
            else
            {
                await CreateCollectionAsync(key, target).ConfigureAwait(false);
                created++;
            }

            done++;
            progress.Report(10 + (80.0 * done / Math.Max(1, desired.Count)));
        }

        if (config.RemoveObsoleteCollections)
        {
            foreach (var obsolete in existingByKey.Values)
            {
                cancellationToken.ThrowIfCancellationRequested();
                _logger.LogInformation("Removing obsolete genre collection {Name}", obsolete.Name);
                _libraryManager.DeleteItem(obsolete, new DeleteOptions { DeleteFileLocation = true }, true);
                removed++;
            }
        }

        _logger.LogInformation(
            "Genre collections synced: {Created} created, {Updated} updated, {Removed} removed, {Total} total",
            created,
            updated,
            removed,
            desired.Count);
        progress.Report(100);
    }

    private Dictionary<string, TargetCollection> BuildDesiredCollections(PluginConfiguration config, GenreRules rules)
    {
        var candidates = new List<GenreCandidate>();
        var formats = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["all"] = config.CollectionNameFormat,
            ["movies"] = config.MovieCollectionNameFormat,
            ["series"] = config.SeriesCollectionNameFormat
        };

        void Collect(BaseItemKind kind, string scope, bool singleGenre)
        {
            // Only top-level titles: movies and whole series, never seasons or episodes.
            var items = _libraryManager.GetItemList(new InternalItemsQuery
            {
                IncludeItemTypes = [kind],
                Recursive = true,
                IsVirtualItem = false
            });

            foreach (var item in items)
            {
                if (!IsOnDisk(item))
                {
                    continue;
                }

                var genres = rules.Normalize(item.Genres).ToList();
                if (genres.Count > 0)
                {
                    candidates.Add(new GenreCandidate(item.Id, scope, genres, singleGenre));
                }
            }
        }

        var separate = config.SeparateMoviesAndSeries;
        if (config.IncludeMovies)
        {
            Collect(BaseItemKind.Movie, separate ? "movies" : "all", config.SingleGenrePerMovie);
        }

        if (config.IncludeSeries)
        {
            Collect(BaseItemKind.Series, separate ? "series" : "all", config.SingleGenrePerSeries);
        }

        var desired = new Dictionary<string, TargetCollection>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, target) in GenreAssignment.Assign(candidates, config.MinimumItemsPerGenre))
        {
            var scope = key[..key.IndexOf(':', StringComparison.Ordinal)];
            var collection = new TargetCollection(FormatName(formats[scope], target.Genre));
            collection.ItemIds.UnionWith(target.ItemIds);
            desired[key] = collection;
        }

        return desired;
    }

    /// <summary>
    /// Returns true when the item really exists on the server (not a placeholder, files still present).
    /// </summary>
    /// <param name="item">Library item.</param>
    /// <returns>True if the item is on disk.</returns>
    public static bool IsOnDisk(BaseItem item)
    {
        if (item.IsVirtualItem)
        {
            return false;
        }

        if (!item.IsFileProtocol)
        {
            return true; // remote/streamed item, nothing to check on disk
        }

        return !string.IsNullOrEmpty(item.Path) && (File.Exists(item.Path) || Directory.Exists(item.Path));
    }

    /// <summary>
    /// Deletes every collection created by this plugin.
    /// </summary>
    /// <returns>Number of deleted collections.</returns>
    public async Task<int> DeleteAllAsync()
    {
        await SyncLock.WaitAsync().ConfigureAwait(false);
        try
        {
            var collections = GetManagedCollections(_libraryManager);
            foreach (var boxSet in collections)
            {
                _logger.LogInformation("Deleting genre collection {Name}", boxSet.Name);
                _libraryManager.DeleteItem(boxSet, new DeleteOptions { DeleteFileLocation = true }, true);
            }

            return collections.Count;
        }
        finally
        {
            SyncLock.Release();
        }
    }

    private static string FormatName(string? format, string genre)
    {
        if (string.IsNullOrWhiteSpace(format) || !format.Contains("{0}", StringComparison.Ordinal))
        {
            return genre;
        }

        try
        {
            return string.Format(CultureInfo.InvariantCulture, format, genre).Trim();
        }
        catch (FormatException)
        {
            return genre;
        }
    }

    private async Task CreateCollectionAsync(string key, TargetCollection target)
    {
        _logger.LogInformation("Creating genre collection {Name} with {Count} items", target.Name, target.ItemIds.Count);

        var options = new CollectionCreationOptions
        {
            Name = target.Name,
            IsLocked = false,
            ItemIdList = target.ItemIds.Select(id => id.ToString("N", CultureInfo.InvariantCulture)).ToArray()
        };
        options.ProviderIds[Plugin.ProviderKey] = key;

        var boxSet = await _collectionManager.CreateCollectionAsync(options).ConfigureAwait(false);

        // Keep the name stable: remote metadata providers must not rename a genre collection.
        if (!boxSet.LockedFields.Contains(MetadataField.Name))
        {
            boxSet.LockedFields = [.. boxSet.LockedFields, MetadataField.Name];
            await boxSet.UpdateToRepositoryAsync(ItemUpdateType.MetadataEdit, CancellationToken.None).ConfigureAwait(false);
        }
    }

    private async Task<bool> UpdateCollectionAsync(BoxSet boxSet, TargetCollection target, CancellationToken cancellationToken)
    {
        var changed = false;

        if (!string.Equals(boxSet.Name, target.Name, StringComparison.Ordinal))
        {
            _logger.LogInformation("Renaming genre collection {Old} to {New}", boxSet.Name, target.Name);
            boxSet.Name = target.Name;
            await boxSet.UpdateToRepositoryAsync(ItemUpdateType.MetadataEdit, cancellationToken).ConfigureAwait(false);
            changed = true;
        }

        var current = boxSet.GetLinkedChildren()
            .Select(c => c.Id)
            .ToHashSet();

        var toAdd = target.ItemIds.Where(id => !current.Contains(id)).ToList();
        var toRemove = current.Where(id => !target.ItemIds.Contains(id)).ToList();

        if (toRemove.Count > 0)
        {
            await _collectionManager.RemoveFromCollectionAsync(boxSet.Id, toRemove).ConfigureAwait(false);
            changed = true;
        }

        if (toAdd.Count > 0)
        {
            await _collectionManager.AddToCollectionAsync(boxSet.Id, toAdd).ConfigureAwait(false);
            changed = true;
        }

        if (changed)
        {
            _logger.LogDebug("Genre collection {Name}: +{Added} / -{Removed}", boxSet.Name, toAdd.Count, toRemove.Count);
        }

        return changed;
    }

    private sealed class TargetCollection
    {
        public TargetCollection(string name)
        {
            Name = name;
        }

        public string Name { get; }

        public HashSet<Guid> ItemIds { get; } = [];
    }
}
