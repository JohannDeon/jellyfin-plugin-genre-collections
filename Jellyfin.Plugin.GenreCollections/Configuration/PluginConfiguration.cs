using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.GenreCollections.Configuration;

/// <summary>
/// Plugin configuration.
/// </summary>
public class PluginConfiguration : BasePluginConfiguration
{
    // ---------- Collections ----------

    /// <summary>
    /// Gets or sets a value indicating whether genre collections are created and kept in sync.
    /// </summary>
    public bool EnableGenreCollections { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether movies are put in genre collections.
    /// </summary>
    public bool IncludeMovies { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether series are put in genre collections.
    /// </summary>
    public bool IncludeSeries { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether movies and series get separate collections per genre.
    /// </summary>
    public bool SeparateMoviesAndSeries { get; set; }

    /// <summary>
    /// Gets or sets the name format of a mixed collection. {0} is the genre.
    /// </summary>
    public string CollectionNameFormat { get; set; } = "{0}";

    /// <summary>
    /// Gets or sets the name format of a movie-only collection. {0} is the genre.
    /// </summary>
    public string MovieCollectionNameFormat { get; set; } = "{0} (Films)";

    /// <summary>
    /// Gets or sets the name format of a series-only collection. {0} is the genre.
    /// </summary>
    public string SeriesCollectionNameFormat { get; set; } = "{0} (Séries)";

    /// <summary>
    /// Gets or sets the minimum number of items a genre needs to get a collection.
    /// </summary>
    public int MinimumItemsPerGenre { get; set; } = 3;

    /// <summary>
    /// Gets or sets genres that never get a collection (one per line or comma separated).
    /// </summary>
    public string ExcludedGenres { get; set; } = "Téléfilm\nTV Movie";

    /// <summary>
    /// Gets or sets genre aliases, one per line: "Source=Cible" or "Source=Cible1,Cible2".
    /// </summary>
    public string GenreAliases { get; set; } =
        "Action & Adventure=Action,Aventure\n" +
        "Sci-Fi & Fantasy=Science-Fiction,Fantastique\n" +
        "Science-Fiction & Fantastique=Science-Fiction,Fantastique\n" +
        "Science Fiction=Science-Fiction\n" +
        "War & Politics=Guerre\n" +
        "Guerre & Politique=Guerre";

    /// <summary>
    /// Gets or sets a value indicating whether managed collections that no longer match anything are deleted.
    /// </summary>
    public bool RemoveObsoleteCollections { get; set; } = true;

    // ---------- Home screen ----------

    /// <summary>
    /// Gets or sets a value indicating whether the home screen rows are injected in the web client.
    /// </summary>
    public bool EnableHomeSections { get; set; } = true;

    /// <summary>
    /// Gets or sets where the rows are placed relative to the native home sections: Top or Bottom.
    /// </summary>
    public string HomeSectionsPosition { get; set; } = "Top";

    /// <summary>
    /// Gets or sets a value indicating whether the "new movies" row is shown.
    /// </summary>
    public bool ShowLatestMovies { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether the "series with new episodes" row is shown.
    /// </summary>
    public bool ShowLatestSeries { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether the row listing every genre collection is shown.
    /// </summary>
    public bool ShowCollectionsRow { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether one row per genre is shown.
    /// </summary>
    public bool ShowGenreRows { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum number of genre rows on the home screen.
    /// </summary>
    public int MaxGenreRows { get; set; } = 6;

    /// <summary>
    /// Gets or sets genres shown first on the home screen (one per line or comma separated).
    /// </summary>
    public string PinnedGenres { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the order of items in genre rows: Random, DateCreated or CommunityRating.
    /// </summary>
    public string GenreRowSort { get; set; } = "Random";

    /// <summary>
    /// Gets or sets the number of items per row.
    /// </summary>
    public int ItemsPerSection { get; set; } = 20;

    /// <summary>
    /// Gets or sets a value indicating whether watched items are hidden from genre rows.
    /// </summary>
    public bool HideWatchedInGenreRows { get; set; }
}
