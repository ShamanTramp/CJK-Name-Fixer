using System.Globalization;
using System.Reflection;
using System.Runtime.ExceptionServices;
using Jellyfin.Plugin.CjkNameFixer.Core;
using Microsoft.Extensions.DependencyInjection;

namespace Jellyfin.Plugin.CjkNameFixer.Services;

/// <summary>Adapts Jellyfin's own TMDb client manager without shipping or storing a second API key.</summary>
public sealed class JellyfinTmdbNameResolver : ITmdbNameResolver
{
    private const string ManagerTypeName = "MediaBrowser.Providers.Plugins.Tmdb.TmdbClientManager, MediaBrowser.Providers";
    private readonly object _manager;
    private readonly MethodInfo _getPersonMethod;

    /// <summary>Initializes the adapter from Jellyfin's dependency injection container.</summary>
    /// <param name="services">The server service provider.</param>
    public JellyfinTmdbNameResolver(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);

        var managerType = Type.GetType(ManagerTypeName, throwOnError: false)
            ?? throw new InvalidOperationException("Jellyfin's TMDb client manager could not be found. Ensure the built-in TMDb provider is available.");
        _manager = services.GetService(managerType)
            ?? throw new InvalidOperationException("Jellyfin's TMDb client manager is not registered in the server service provider.");
        _getPersonMethod = managerType.GetMethod("GetPersonAsync", BindingFlags.Instance | BindingFlags.Public, [typeof(int), typeof(string), typeof(string), typeof(CancellationToken)])
            ?? throw new InvalidOperationException("Jellyfin's TMDb client manager does not expose the expected GetPersonAsync method.");
    }

    /// <inheritdoc />
    public async Task<string?> ResolveAsync(string tmdbId, CancellationToken cancellationToken)
    {
        if (!int.TryParse(tmdbId, NumberStyles.None, CultureInfo.InvariantCulture, out var personId) || personId <= 0)
        {
            throw new ArgumentException("The TMDb person identifier must be a positive integer.", nameof(tmdbId));
        }

        object? invocation;
        try
        {
            invocation = _getPersonMethod.Invoke(_manager, [personId, "en-US", null, cancellationToken]);
        }
        catch (TargetInvocationException exception) when (exception.InnerException is not null)
        {
            ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
            throw;
        }

        if (invocation is not Task task)
        {
            throw new InvalidOperationException("Jellyfin's TMDb client manager returned an unexpected result.");
        }

        await task.ConfigureAwait(false);
        var person = task.GetType().GetProperty("Result")?.GetValue(task);
        var name = person?.GetType().GetProperty("Name")?.GetValue(person) as string;
        return string.IsNullOrWhiteSpace(name) ? null : name.Trim();
    }
}
