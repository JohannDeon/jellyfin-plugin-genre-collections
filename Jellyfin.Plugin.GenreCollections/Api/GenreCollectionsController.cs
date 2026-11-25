using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Jellyfin.Plugin.GenreCollections.Configuration;
using Jellyfin.Plugin.GenreCollections.Services;
using Jellyfin.Plugin.GenreCollections.Tasks;
using MediaBrowser.Common.Api;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Plugin.GenreCollections.Api;

/// <summary>
/// Endpoints used by the injected home screen script.
/// </summary>
[ApiController]
[Route("GenreCollections")]
public class GenreCollectionsController : ControllerBase
{
    private readonly ILibraryManager _libraryManager;
    private readonly ITaskManager _taskManager;
    private readonly GenreCollectionService _service;

    /// <summary>
    /// Initializes a new instance of the <see cref="GenreCollectionsController"/> class.
    /// </summary>
    /// <param name="libraryManager">Library manager.</param>
    /// <param name="taskManager">Task manager.</param>
    /// <param name="service">Genre collection service.</param>
    public GenreCollectionsController(ILibraryManager libraryManager, ITaskManager taskManager, GenreCollectionService service)
    {
        _libraryManager = libraryManager;
        _taskManager = taskManager;
        _service = service;
    }

    /// <summary>
    /// Deletes every collection created by the plugin (admin only).
    /// </summary>
    /// <returns>Number of deleted collections.</returns>
    [HttpPost("DeleteAll")]
    [Authorize(Policy = Policies.RequiresElevation)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<int>> DeleteAll()
    {
        return await _service.DeleteAllAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Queues a genre collection sync (admin only).
    /// </summary>
    /// <returns>No content.</returns>
    [HttpPost("Sync")]
    [Authorize(Policy = Policies.RequiresElevation)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public ActionResult Sync()
    {
        _taskManager.QueueScheduledTask<GenreCollectionsTask>();
        return NoContent();
    }

    /// <summary>
    /// Returns the home screen script.
    /// </summary>
    /// <returns>The JavaScript file.</returns>
    [HttpGet("ClientScript")]
    [AllowAnonymous]
    [Produces("application/javascript")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult GetClientScript()
    {
        var assembly = typeof(Plugin).Assembly;
        var stream = assembly.GetManifestResourceStream(typeof(Plugin).Namespace + ".Web.homeSections.js");
        if (stream is null)
        {
            return NotFound();
        }

        Response.Headers.CacheControl = "no-cache";
        return File(stream, "application/javascript");
    }

    /// <summary>
    /// Returns the home screen layout for the current user.
    /// </summary>
    /// <returns>Home sections description.</returns>
    [HttpGet("HomeSections")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult<HomeSectionsDto> GetHomeSections()
    {
        var config = Plugin.Instance?.Configuration ?? new PluginConfiguration();
        var dto = new HomeSectionsDto
        {
            Enabled = config.EnableHomeSections,
            Position = string.Equals(config.HomeSectionsPosition, "Bottom", StringComparison.OrdinalIgnoreCase) ? "Bottom" : "Top",
            ShowLatestMovies = config.ShowLatestMovies,
            ShowLatestSeries = config.ShowLatestSeries,
            ShowAllMovies = config.ShowAllMovies,
            ShowAllSeries = config.ShowAllSeries,
            ShowCollectionsRow = config.ShowCollectionsRow,
            ShowGenreRows = config.ShowGenreRows,
            ItemsPerSection = Math.Clamp(config.ItemsPerSection, 4, 60),
            GenreRowSort = config.GenreRowSort switch
            {
                "DateCreated" => "DateCreated",
                "CommunityRating" => "CommunityRating",
                "PremiereDate" => "PremiereDate",
                _ => "Random"
            },
            HideWatchedInGenreRows = config.HideWatchedInGenreRows
        };

        if (!config.EnableHomeSections)
        {
            return dto;
        }

        var managed = GenreCollectionService.GetManagedCollections(_libraryManager);

        CollectionRef ToRef(MediaBrowser.Controller.Entities.Movies.BoxSet b) => new()
        {
            Id = b.Id.ToString("N", System.Globalization.CultureInfo.InvariantCulture),
            Name = b.Name,
            Count = b.GetLinkedChildren().Count(GenreCollectionService.IsOnDisk)
        };

        // Sagas: the plugin's saga collections plus the TMDb saga collections the user already had.
        if (config.ShowSagasRow)
        {
            dto.Sagas = managed
                .Where(b => GenreCollectionService.IsSagaKey(b.ProviderIds[Plugin.ProviderKey]))
                .Concat(GenreCollectionService.GetUserSagaCollections(_libraryManager))
                .Select(ToRef)
                .Where(c => c.Count >= Math.Max(2, config.MinimumMoviesPerSaga))
                .OrderBy(c => c.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }

        // Genres: computed from the library, not stored as collections (see GenreCollectionService.SyncInternalAsync).
        var groups = _service.GetGenreIndex(config);
        dto.Collections = groups
            .Where(g => g.GenreId is not null)
            .Select(g => new CollectionRef
            {
                Id = g.GenreId!.Value.ToString("N", System.Globalization.CultureInfo.InvariantCulture),
                Name = g.Name,
                Count = g.ItemIds.Count
            })
            .OrderBy(c => c.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        var pinned = GenreRules.SplitList(config.PinnedGenres).ToList();
        int PinIndex(GenreGroup g)
        {
            var idx = pinned.FindIndex(p =>
                string.Equals(p, g.Genre, StringComparison.OrdinalIgnoreCase)
                || string.Equals(p, g.Name, StringComparison.OrdinalIgnoreCase));
            return idx < 0 ? int.MaxValue : idx;
        }

        // The client sorts and limits; send a bounded, shuffled sample so the request URL stays short.
        dto.GenreRows = groups
            .OrderBy(PinIndex)
            .ThenByDescending(g => g.ItemIds.Count)
            .Take(Math.Max(0, config.MaxGenreRows))
            .Select(g => new GenreRowRef
            {
                Name = g.Name,
                GenreId = g.GenreId?.ToString("N", System.Globalization.CultureInfo.InvariantCulture),
                ItemIds = g.ItemIds
                    .OrderBy(_ => Random.Shared.Next())
                    .Take(120)
                    .Select(id => id.ToString("N", System.Globalization.CultureInfo.InvariantCulture))
                    .ToList()
            })
            .ToList();

        return dto;
    }

    /// <summary>
    /// Home sections description returned to the web client.
    /// </summary>
    public class HomeSectionsDto
    {
        /// <summary>Gets or sets a value indicating whether the rows are enabled.</summary>
        public bool Enabled { get; set; }

        /// <summary>Gets or sets where the rows go: Top or Bottom.</summary>
        public string Position { get; set; } = "Top";

        /// <summary>Gets or sets a value indicating whether the new movies row is shown.</summary>
        public bool ShowLatestMovies { get; set; }

        /// <summary>Gets or sets a value indicating whether the new series row is shown.</summary>
        public bool ShowLatestSeries { get; set; }

        /// <summary>Gets or sets a value indicating whether the all movies row is shown.</summary>
        public bool ShowAllMovies { get; set; }

        /// <summary>Gets or sets a value indicating whether the all series row is shown.</summary>
        public bool ShowAllSeries { get; set; }

        /// <summary>Gets or sets a value indicating whether the collections row is shown.</summary>
        public bool ShowCollectionsRow { get; set; }

        /// <summary>Gets or sets a value indicating whether genre rows are shown.</summary>
        public bool ShowGenreRows { get; set; }

        /// <summary>Gets or sets the number of items per row.</summary>
        public int ItemsPerSection { get; set; }

        /// <summary>Gets or sets the sort of genre rows.</summary>
        public string GenreRowSort { get; set; } = "Random";

        /// <summary>Gets or sets a value indicating whether watched items are hidden in genre rows.</summary>
        public bool HideWatchedInGenreRows { get; set; }

        /// <summary>Gets or sets the movie sagas shown in the first row.</summary>
        public IReadOnlyList<CollectionRef> Sagas { get; set; } = [];

        /// <summary>Gets or sets every genre collection.</summary>
        public IReadOnlyList<CollectionRef> Collections { get; set; } = [];

        /// <summary>Gets or sets the genres shown as rows.</summary>
        public IReadOnlyList<GenreRowRef> GenreRows { get; set; } = [];

        /// <summary>Gets the row titles.</summary>
        public Dictionary<string, string> Labels { get; } = new()
        {
            ["LatestMovies"] = "Nouveaux films",
            ["LatestSeries"] = "Séries avec de nouveaux épisodes",
            ["Collections"] = "Genres",
            ["Sagas"] = "Films SAGA",
            ["AllMovies"] = "Tous les films",
            ["AllSeries"] = "Toutes les séries"
        };
    }

    /// <summary>
    /// Reference to a genre collection.
    /// </summary>
    public class CollectionRef
    {
        /// <summary>Gets or sets the collection id.</summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>Gets or sets the collection name.</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>Gets or sets the number of items.</summary>
        public int Count { get; set; }
    }

    /// <summary>
    /// A genre row: its title, native genre page and candidate titles.
    /// </summary>
    public class GenreRowRef
    {
        /// <summary>Gets or sets the row title.</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>Gets or sets the native genre id (row link), if any.</summary>
        public string? GenreId { get; set; }

        /// <summary>Gets or sets the candidate titles.</summary>
        public IReadOnlyList<string> ItemIds { get; set; } = [];
    }
}
