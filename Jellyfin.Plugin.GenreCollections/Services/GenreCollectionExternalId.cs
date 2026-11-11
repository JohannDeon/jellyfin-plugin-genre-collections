using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;

namespace Jellyfin.Plugin.GenreCollections.Services;

/// <summary>
/// Declares the "GenreCollections" provider id on collections so Jellyfin keeps it
/// when it saves and re-reads collection metadata (collection.xml).
/// </summary>
public class GenreCollectionExternalId : IExternalId
{
    /// <inheritdoc />
    public string ProviderName => "Genre Collections";

    /// <inheritdoc />
    public string Key => Plugin.ProviderKey;

    /// <inheritdoc />
    public ExternalIdMediaType? Type => ExternalIdMediaType.BoxSet;

    /// <inheritdoc />
    public bool Supports(IHasProviderIds item) => item is BoxSet;
}
