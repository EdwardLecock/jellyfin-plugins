using Jellyfin.Plugin.VersionLabels.Middleware;
using Jellyfin.Plugin.VersionLabels.Services;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace Jellyfin.Plugin.VersionLabels;

/// <summary>
/// Registers plugin services with Jellyfin.
/// </summary>
public class PluginServiceRegistrator : IPluginServiceRegistrator
{
    /// <inheritdoc />
    public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
    {
        serviceCollection.AddSingleton<VersionLabelBuilder>();
        serviceCollection.AddSingleton<MediaSourceJsonRewriter>();
        serviceCollection.AddSingleton<IStartupFilter, VersionLabelStartupFilter>();
    }
}

/// <summary>
/// Adds the version label middleware to the ASP.NET Core pipeline.
/// </summary>
public class VersionLabelStartupFilter : IStartupFilter
{
    /// <inheritdoc />
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
    {
        return app =>
        {
            app.UseMiddleware<VersionLabelMiddleware>();
            next(app);
        };
    }
}
