using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Jellyfin.Plugin.CjkNameFixer.Core;

namespace Jellyfin.Plugin.CjkNameFixer.Services;

/// <summary>Queries TMDb directly when the user supplies a personal API key.</summary>
public sealed class TmdbApiNameResolver(HttpClient httpClient, Func<string?> apiKeyProvider) : ITmdbNameResolver
{
    private const int MaxAttempts = 3;
    private static readonly TimeSpan MaximumRetryDelay = TimeSpan.FromSeconds(30);

    /// <inheritdoc />
    public async Task<string?> ResolveAsync(string tmdbId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tmdbId);
        var apiKey = apiKeyProvider()?.Trim();
        if (string.IsNullOrEmpty(apiKey))
        {
            throw new InvalidOperationException("A personal TMDb API key must be configured to use the direct API resolver.");
        }

        var url = $"https://api.themoviedb.org/3/person/{Uri.EscapeDataString(tmdbId)}?language=en-US&api_key={Uri.EscapeDataString(apiKey)}";
        for (var attempt = 1; ; attempt++)
        {
            using var response = await httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            if (response.StatusCode == HttpStatusCode.TooManyRequests && attempt < MaxAttempts)
            {
                await Task.Delay(GetRetryDelay(response, attempt), cancellationToken).ConfigureAwait(false);
                continue;
            }

            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<TmdbPersonResponse>(cancellationToken: cancellationToken).ConfigureAwait(false);
            return string.IsNullOrWhiteSpace(result?.Name) ? null : result.Name.Trim();
        }
    }

    private static TimeSpan GetRetryDelay(HttpResponseMessage response, int attempt)
    {
        var retryAfter = response.Headers.RetryAfter;
        var delay = retryAfter?.Delta
            ?? (retryAfter?.Date is { } date ? date - DateTimeOffset.UtcNow : TimeSpan.FromSeconds(Math.Pow(2, attempt)));
        return delay < TimeSpan.Zero ? TimeSpan.Zero : delay > MaximumRetryDelay ? MaximumRetryDelay : delay;
    }

    private sealed class TmdbPersonResponse
    {
        [JsonPropertyName("name")]
        public string? Name { get; init; }
    }
}
