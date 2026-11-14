namespace Newlife.Application.Common.Interfaces;

public sealed record FeedEpisode(
    string Guid,
    string Title,
    string DescriptionHtml,
    string AudioUrl,
    DateTime PublishedAt,
    string? ArtworkUrl,
    int? DurationSeconds);

public interface IPodcastFeedClient
{
    Task<IReadOnlyList<FeedEpisode>> FetchAsync(CancellationToken cancellationToken);
}
