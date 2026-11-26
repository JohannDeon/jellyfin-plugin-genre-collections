using Jellyfin.Plugin.GenreCollections.Injection;
using Jellyfin.Plugin.GenreCollections.Services;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace Jellyfin.Plugin.GenreCollections;

/// <summary>
/// Registers the plugin services.
/// </summary>
public class PluginServiceRegistrator : IPluginServiceRegistrator
{
    /// <inheritdoc />
    public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
    {
        serviceCollection.AddSingleton<GenreCollectionService>();
        serviceCollection.AddTransient<IStartupFilter, InjectionStartupFilter>();
        serviceCollection.AddHostedService<LegacyIndexCleanup>();
    }
}
