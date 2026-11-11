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
