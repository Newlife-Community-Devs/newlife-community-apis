using Newlife.Application.Common.Interfaces;

namespace Newlife.IntegrationTests.Infrastructure;

public sealed class FakePodcastFeedClient : IPodcastFeedClient
{
    public List<FeedEpisode> Episodes { get; } = [];

    public Task<IReadOnlyList<FeedEpisode>> FetchAsync(CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<FeedEpisode>>(Episodes.ToArray());
}
