using Jellyfin.Plugin.CjkNameFixer.Core;
using Jellyfin.Plugin.CjkNameFixer.Services;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using Microsoft.Extensions.DependencyInjection;

namespace Jellyfin.Plugin.CjkNameFixer;

/// <summary>Registers the plugin services with Jellyfin's dependency injection container.</summary>
public sealed class PluginServiceRegistrator : IPluginServiceRegistrator
{
    /// <inheritdoc />
    public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
    {
        serviceCollection.AddSingleton<IPersonNameRepository, JellyfinPersonNameRepository>();
        serviceCollection.AddSingleton(_ => new HttpClient());
        serviceCollection.AddSingleton<ITmdbNameResolver>(services =>
        {
            var customKey = () => Plugin.Instance?.Configuration.TmdbApiKeyOverride;
            var jellyfinResolver = new CachedTmdbNameResolver(new JellyfinTmdbNameResolver(services), TimeSpan.FromHours(12));
            var customKeyResolver = new CachedTmdbNameResolver(
                new TmdbApiNameResolver(services.GetRequiredService<HttpClient>(), customKey),
                TimeSpan.FromHours(12));
            return new ConfiguredTmdbNameResolver(jellyfinResolver, customKeyResolver, customKey);
        });
        serviceCollection.AddSingleton<PersonNameFixer>();
        serviceCollection.AddSingleton<PersonFixRunner>();
        serviceCollection.AddHostedService<NewMediaFixService>();
    }
}
