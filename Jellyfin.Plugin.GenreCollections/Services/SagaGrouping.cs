using System;
using System.Collections.Generic;
using System.Linq;

namespace Jellyfin.Plugin.GenreCollections.Services;

/// <summary>
/// A movie with its TMDb saga ("belongs to collection") information.
/// </summary>
/// <param name="ItemId">Movie id.</param>
/// <param name="TmdbCollectionId">TMDb collection id, if any.</param>
/// <param name="CollectionName">TMDb collection name, if any.</param>
/// <param name="PremiereDate">Release date.</param>
public sealed record SagaCandidate(Guid ItemId, string? TmdbCollectionId, string? CollectionName, DateTime? PremiereDate);

/// <summary>
/// A saga to build.
/// </summary>
/// <param name="TmdbCollectionId">TMDb collection id.</param>
/// <param name="Name">Saga name.</param>
/// <param name="ItemIds">Movies, in release order.</param>
public sealed record SagaTarget(string TmdbCollectionId, string Name, IReadOnlyList<Guid> ItemIds);

/// <summary>
/// Groups movies into sagas (Harry Potter, Hunger Games, ...) using their TMDb collection.
/// </summary>
public static class SagaGrouping
{
    /// <summary>
    /// Groups movies by TMDb collection.
    /// </summary>
    /// <param name="movies">Movies.</param>
    /// <param name="minimum">Minimum number of movies on the server for a saga.</param>
    /// <returns>Sagas, ordered by name.</returns>
    public static IReadOnlyList<SagaTarget> Group(IEnumerable<SagaCandidate> movies, int minimum)
    {
        minimum = Math.Max(2, minimum);
        return movies
            .Where(m => !string.IsNullOrWhiteSpace(m.TmdbCollectionId))
            .GroupBy(m => m.TmdbCollectionId!.Trim(), StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() >= minimum)
            .Select(g =>
            {
                var ordered = g.OrderBy(m => m.PremiereDate ?? DateTime.MaxValue).ToList();
                var name = ordered.Select(m => m.CollectionName).FirstOrDefault(n => !string.IsNullOrWhiteSpace(n))?.Trim()
                    ?? "Saga " + g.Key;
                return new SagaTarget(g.Key, name, ordered.Select(m => m.ItemId).ToList());
            })
            .OrderBy(s => s.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }
}
