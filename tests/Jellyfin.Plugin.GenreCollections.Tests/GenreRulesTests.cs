using System.Linq;
using Jellyfin.Plugin.GenreCollections.Configuration;
using Jellyfin.Plugin.GenreCollections.Services;
using Xunit;

namespace Jellyfin.Plugin.GenreCollections.Tests;

public class GenreRulesTests
{
    private static GenreRules Defaults()
    {
        var c = new PluginConfiguration();
        return new GenreRules(c.GenreAliases, c.ExcludedGenres);
    }

    [Fact]
    public void KeepsPlainGenres()
    {
        Assert.Equal(new[] { "Horreur", "Comédie" }, Defaults().Normalize(new[] { "Horreur", " Comédie " }).ToArray());
    }

    [Fact]
    public void SplitsTvGenresWithAliases()
    {
        var result = Defaults().Normalize(new[] { "Action & Adventure", "Sci-Fi & Fantasy" }).ToArray();
        Assert.Equal(new[] { "Action", "Aventure", "Science-Fiction", "Fantastique" }, result);
    }

    [Fact]
    public void RemovesDuplicatesAfterAliasing()
    {
        var result = Defaults().Normalize(new[] { "Action", "Action & Adventure" }).ToArray();
        Assert.Equal(new[] { "Action", "Aventure" }, result);
    }

    [Fact]
    public void DropsExcludedGenres()
    {
        Assert.Empty(Defaults().Normalize(new[] { "Téléfilm", "tv movie" }));
    }

    [Fact]
    public void AliasMatchIsCaseInsensitive()
    {
        Assert.Equal(new[] { "Guerre" }, Defaults().Normalize(new[] { "war & politics" }).ToArray());
    }

    [Fact]
    public void CollectionKeyIsStable()
    {
        Assert.Equal("all:horreur", GenreCollectionService.CollectionKey("all", " Horreur "));
    }
}

public class WebClientInjectorTests
{
    private const string Html = "<html><body><div></div></body></html>";

    [Fact]
    public void AddsTagOnceBeforeBody()
    {
        var once = WebClientInjector.AddTag(Html);
        var twice = WebClientInjector.AddTag(once);
        Assert.Equal(once, twice);
        Assert.EndsWith(WebClientInjector.ScriptTag + "</body></html>", once);
    }

    [Fact]
    public void RemoveRestoresOriginal()
    {
        Assert.Equal(Html, WebClientInjector.RemoveTag(WebClientInjector.AddTag(Html)));
    }
}

public class GenreAssignmentTests
{
    private static GenreCandidate C(int id, bool single, params string[] genres)
        => new(new System.Guid(id, 0, 0, new byte[8]), "all", genres, single);

    [Fact]
    public void SingleGenreItemGoesInMainGenreOnly()
    {
        var items = new[] { C(1, true, "Drame", "Crime"), C(2, false, "Drame"), C(3, false, "Crime") };
        var result = GenreAssignment.Assign(items, 1);
        Assert.Contains(items[0].ItemId, result["all:drame"].ItemIds);
        Assert.DoesNotContain(items[0].ItemId, result["all:crime"].ItemIds);
    }

    [Fact]
    public void MultiGenreItemGoesEverywhere()
    {
        var items = new[] { C(1, false, "Drame", "Crime") };
        var result = GenreAssignment.Assign(items, 1);
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void FallsBackToNextGenreWhenMainGenreIsTooSmall()
    {
        // "Western" only has one series: it does not get a collection, so the series goes in "Drame".
        var items = new[]
        {
            C(1, true, "Western", "Drame"),
            C(2, true, "Drame"),
            C(3, true, "Drame"),
        };
        var result = GenreAssignment.Assign(items, 3);
        Assert.Single(result);
        Assert.Equal(3, result["all:drame"].ItemIds.Count);
    }

    [Fact]
    public void DropsCollectionsThatBecomeTooSmall()
    {
        // Counting every genre, "Crime" has 2 items, but single-genre series all pick "Drame".
        var items = new[] { C(1, true, "Drame", "Crime"), C(2, true, "Drame", "Crime") };
        var result = GenreAssignment.Assign(items, 2);
        Assert.Equal(new[] { "all:drame" }, result.Keys);
    }
}

public class GenrePriorityTests
{
    [Fact]
    public void PriorityGenreComesFirst()
    {
        var c = new PluginConfiguration();
        var rules = new GenreRules(c.GenreAliases, c.ExcludedGenres, c.GenrePriority);
        Assert.Equal("Horreur", rules.OrderByRelevance(new[] { "Drame", "Horreur" })[0]);
        Assert.Equal("Animation", rules.OrderByRelevance(new[] { "Comédie", "Familial", "Animation" })[0]);
    }

    [Fact]
    public void UnknownGenresKeepMetadataOrderAfterKnownOnes()
    {
        var rules = new GenreRules(null, null, "Horreur");
        Assert.Equal(new[] { "Horreur", "Kung-fu", "Policier" }, rules.OrderByRelevance(new[] { "Kung-fu", "Policier", "Horreur" }));
    }
}
