using Microsoft.Extensions.Configuration;
using Newlife.Application.Common.Interfaces;

namespace Newlife.Infrastructure.Sermons;

public sealed class PodcastFeedClient(HttpClient httpClient, IConfiguration configuration) : IPodcastFeedClient
{
    public async Task<IReadOnlyList<FeedEpisode>> FetchAsync(CancellationToken cancellationToken)
    {
        var feedUrl = configuration["Sermons:FeedUrl"];

        if (string.IsNullOrWhiteSpace(feedUrl))
        {
            return [];
        }

        string xml;

        try
        {
            xml = await httpClient.GetStringAsync(feedUrl, cancellationToken);
        }
        catch (Exception exception) when (IsTransient(exception) && !cancellationToken.IsCancellationRequested)
        {
            xml = await httpClient.GetStringAsync(feedUrl, cancellationToken);
        }

        return SermonFeedParser.Parse(xml);
    }

    private static bool IsTransient(Exception exception)
        => exception is HttpRequestException or IOException or TaskCanceledException;
}
