using System;
using System.Collections.Generic;
using System.Linq;

namespace Jellyfin.Plugin.GenreCollections.Services;

/// <summary>
/// Normalizes raw metadata genres using the user's aliases and exclusions.
/// </summary>
public sealed class GenreRules
{
    private static readonly char[] ListSeparators = [',', ';', '\n', '\r'];

    private readonly Dictionary<string, string[]> _aliases;
    private readonly HashSet<string> _excluded;
    private readonly Dictionary<string, int> _priority;

    /// <summary>
    /// Initializes a new instance of the <see cref="GenreRules"/> class.
    /// </summary>
    /// <param name="aliases">Alias lines ("Source=Cible1,Cible2").</param>
    /// <param name="excluded">Excluded genres, comma or newline separated.</param>
    /// <param name="priority">Genre priority, most specific first.</param>
    public GenreRules(string? aliases, string? excluded, string? priority = null)
    {
        _aliases = ParseAliases(aliases);
        _excluded = new HashSet<string>(SplitList(excluded), StringComparer.OrdinalIgnoreCase);
        _priority = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var genre in SplitList(priority))
        {
            _priority.TryAdd(genre, _priority.Count);
        }
    }

    /// <summary>
    /// Orders genres by relevance: genres of the priority list first (in that order), then the others in metadata order.
    /// The first genre is the one a single-genre title goes in.
    /// </summary>
    /// <param name="genres">Normalized genres in metadata order.</param>
    /// <returns>Genres, most relevant first.</returns>
    public IReadOnlyList<string> OrderByRelevance(IEnumerable<string> genres)
    {
        return genres
            .Select((genre, index) => (genre, index))
            .OrderBy(g => _priority.TryGetValue(g.genre, out var rank) ? rank : int.MaxValue)
            .ThenBy(g => g.index)
            .Select(g => g.genre)
            .ToList();
    }

    /// <summary>
    /// Splits a comma / semicolon / newline separated list.
    /// </summary>
    /// <param name="value">Raw value.</param>
    /// <returns>Trimmed, non empty entries.</returns>
    public static IEnumerable<string> SplitList(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        return value.Split(ListSeparators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    /// <summary>
    /// Builds the stable key used to identify a genre.
    /// </summary>
    /// <param name="genre">Genre display name.</param>
    /// <returns>Lower-case key.</returns>
    public static string Key(string genre) => genre.Trim().ToLowerInvariant();

    /// <summary>
    /// Maps the raw genres of an item to normalized genres (aliases applied, exclusions removed, duplicates removed).
    /// </summary>
    /// <param name="rawGenres">Genres from metadata.</param>
    /// <returns>Normalized genre display names.</returns>
    public IEnumerable<string> Normalize(IEnumerable<string>? rawGenres)
    {
        if (rawGenres is null)
        {
            yield break;
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var raw in rawGenres)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                continue;
            }

            var trimmed = raw.Trim();
            var targets = _aliases.TryGetValue(trimmed, out var mapped) ? mapped : [trimmed];
            foreach (var target in targets)
            {
                if (_excluded.Contains(target) || _excluded.Contains(trimmed))
                {
                    continue;
                }

                if (seen.Add(target))
                {
                    yield return target;
                }
            }
        }
    }

    private static Dictionary<string, string[]> ParseAliases(string? aliases)
    {
        var result = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(aliases))
        {
            return result;
        }

        foreach (var line in aliases.Split(['\n', '\r'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var idx = line.IndexOf('=', StringComparison.Ordinal);
            if (idx <= 0)
            {
                continue;
            }

            var source = line[..idx].Trim();
            var targets = line[(idx + 1)..]
                .Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToArray();
            if (source.Length > 0 && targets.Length > 0)
            {
                result[source] = targets;
            }
        }

        return result;
    }
}
