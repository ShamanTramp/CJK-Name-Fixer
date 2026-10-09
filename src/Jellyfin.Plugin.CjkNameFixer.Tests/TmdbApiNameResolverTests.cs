using System.Net;
using System.Net.Http.Headers;
using Jellyfin.Plugin.CjkNameFixer.Services;

namespace Jellyfin.Plugin.CjkNameFixer.Tests;

public sealed class TmdbApiNameResolverTests
{
    [Fact]
    public async Task ResolveAsync_UsesPersonalKeyAndRetriesRateLimit()
    {
        var handler = new SequenceHandler(
            new HttpResponseMessage(HttpStatusCode.TooManyRequests)
            {
                Headers = { RetryAfter = new RetryConditionHeaderValue(TimeSpan.Zero) }
            },
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"name\":\"Kim Soo-hyun\"}")
            });
        var resolver = new TmdbApiNameResolver(new HttpClient(handler), () => "personal-key");

        var result = await resolver.ResolveAsync("12345", CancellationToken.None);

        Assert.Equal("Kim Soo-hyun", result);
        Assert.Equal(2, handler.Requests.Count);
        Assert.Equal("https://api.themoviedb.org/3/person/12345?language=en-US&api_key=personal-key", handler.Requests[0]);
    }

    [Fact]
    public async Task ResolveAsync_RequiresThePersonalKeyWhenCalledDirectly()
    {
        var handler = new SequenceHandler();
        var resolver = new TmdbApiNameResolver(new HttpClient(handler), () => " ");

        await Assert.ThrowsAsync<InvalidOperationException>(() => resolver.ResolveAsync("12345", CancellationToken.None));
        Assert.Empty(handler.Requests);
    }

    private sealed class SequenceHandler(params HttpResponseMessage[] responses) : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses = new(responses);

        public List<string> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request.RequestUri!.ToString());
            return Task.FromResult(_responses.Dequeue());
        }
    }
}
