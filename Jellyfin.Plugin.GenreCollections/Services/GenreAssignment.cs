using System;
using System.Collections.Generic;
using System.Linq;

namespace Jellyfin.Plugin.GenreCollections.Services;

/// <summary>
/// A library item (movie or series) with its normalized genres, in metadata order (main genre first).
/// </summary>
/// <param name="ItemId">Item id.</param>
/// <param name="Scope">Collection scope: "all", "movies" or "series".</param>
/// <param name="Genres">Normalized genres, main genre first.</param>
/// <param name="SingleGenre">True to put the item in its main genre collection only.</param>
public sealed record GenreCandidate(Guid ItemId, string Scope, IReadOnlyList<string> Genres, bool SingleGenre);

/// <summary>
/// A genre collection to build.
/// </summary>
/// <param name="Genre">Genre display name.</param>
/// <param name="ItemIds">Items in the collection.</param>
public sealed record GenreTarget(string Genre, HashSet<Guid> ItemIds);

/// <summary>
/// Decides which genre collections exist and which items go in each.
/// </summary>
public static class GenreAssignment
{
    /// <summary>
    /// Assigns items to genre collections.
    /// An item in single-genre mode goes only in the first of its genres that has a collection,
    /// so it never ends up without a collection just because its first genre is too small.
    /// </summary>
    /// <param name="items">Candidates.</param>
    /// <param name="minimum">Minimum number of items for a collection to exist.</param>
    /// <returns>Collections by key (see <see cref="GenreCollectionService.CollectionKey"/>).</returns>
    public static Dictionary<string, GenreTarget> Assign(IReadOnlyList<GenreCandidate> items, int minimum)
    {
        minimum = Math.Max(1, minimum);

        var names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in items)
        {
            foreach (var genre in item.Genres)
            {
                var key = GenreCollectionService.CollectionKey(item.Scope, genre);
                names.TryAdd(key, genre);
                counts[key] = counts.GetValueOrDefault(key) + 1;
            }
        }

        // Start with every genre big enough when counting all genres of every item,
        // then drop the ones that become too small once single-genre items pick their main genre.
        var eligible = new HashSet<string>(counts.Where(kv => kv.Value >= minimum).Select(kv => kv.Key), StringComparer.OrdinalIgnoreCase);

        while (true)
        {
            var result = new Dictionary<string, GenreTarget>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in items)
            {
                foreach (var genre in item.Genres)
                {
                    var key = GenreCollectionService.CollectionKey(item.Scope, genre);
                    if (!eligible.Contains(key))
                    {
                        continue;
                    }

                    if (!result.TryGetValue(key, out var target))
                    {
                        target = new GenreTarget(names[key], []);
                        result[key] = target;
                    }

                    target.ItemIds.Add(item.ItemId);
                    if (item.SingleGenre)
                    {
                        break;
                    }
                }
            }

            var tooSmall = result.Where(kv => kv.Value.ItemIds.Count < minimum).Select(kv => kv.Key).ToList();
            if (tooSmall.Count == 0)
            {
                return result;
            }

            // Drop only the smallest one(s) and retry: their items fall back to their next genre.
            var smallest = tooSmall.Min(k => result[k].ItemIds.Count);
            eligible.ExceptWith(tooSmall.Where(k => result[k].ItemIds.Count == smallest));
        }
    }
}
