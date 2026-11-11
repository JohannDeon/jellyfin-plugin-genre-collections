using System;
using System.Collections.Generic;
using System.Globalization;
using Jellyfin.Plugin.GenreCollections.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;

namespace Jellyfin.Plugin.GenreCollections;

/// <summary>
/// Genre Collections plugin: builds one collection per genre and adds home screen rows.
/// </summary>
public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
{
    /// <summary>
    /// Provider id key written on every collection managed by this plugin.
    /// </summary>
    public const string ProviderKey = "GenreCollections";

    /// <summary>
    /// Initializes a new instance of the <see cref="Plugin"/> class.
    /// </summary>
    /// <param name="applicationPaths">Application paths.</param>
    /// <param name="xmlSerializer">XML serializer.</param>
    public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer)
        : base(applicationPaths, xmlSerializer)
    {
        Instance = this;
    }

    /// <inheritdoc />
    public override string Name => "Genre Collections";

    /// <inheritdoc />
    public override Guid Id => Guid.Parse("51657d44-7198-4c95-b99d-afef3045fad9");

    /// <inheritdoc />
    public override string Description => "Crée des collections par genre (films et séries) et ajoute des rangées sur l'écran d'accueil.";

    /// <summary>
    /// Gets the current plugin instance.
    /// </summary>
    public static Plugin? Instance { get; private set; }

    /// <inheritdoc />
    public IEnumerable<PluginPageInfo> GetPages()
    {
        return
        [
            new PluginPageInfo
            {
                Name = Name,
                EmbeddedResourcePath = string.Format(CultureInfo.InvariantCulture, "{0}.Configuration.configPage.html", GetType().Namespace)
            }
        ];
    }
}
