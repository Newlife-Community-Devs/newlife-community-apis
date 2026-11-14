using Newlife.Application.Common.Models;

namespace Newlife.Application.Sermons.DTOs;

public sealed record SermonListResponse(
    Guid Id,
    string Title,
    string? Summary,
    string AudioUrl,
    int? DurationSeconds,
    string? ArtworkUrl,
    DateTime PublishedAt);

public sealed record SermonDetailResponse(
    Guid Id,
    string Title,
    string? Summary,
    string? DescriptionHtml,
    string AudioUrl,
    int? DurationSeconds,
    string? ArtworkUrl,
    DateTime PublishedAt,
    bool IsVisible);

public sealed record LatestSermonResponse(
    SermonListResponse? Episode,
    string YoutubeUrl);

public sealed record SyncResultResponse(int Added, int Updated, int Total);

public sealed record UpdateSermonVisibilityRequest(bool IsVisible);

public sealed class SermonAdminQuery : PaginationQuery
{
    public string? Search { get; set; }
}
