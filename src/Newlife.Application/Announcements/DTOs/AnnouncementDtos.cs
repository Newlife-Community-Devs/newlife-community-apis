using Newlife.Application.Common.Models;

namespace Newlife.Application.Announcements.DTOs;

public sealed record AnnouncementUpsertRequest(
    string Title,
    string Body,
    bool IsPinned,
    bool IsPublished,
    DateTime? PublishAt,
    DateTime? ExpiresAt);

public sealed record AnnouncementResponse(
    Guid Id,
    string Title,
    string Body,
    bool IsPinned,
    DateTime? PublishAt,
    DateTime? ExpiresAt,
    DateTime CreatedAt);

public sealed class AnnouncementAdminQuery : PaginationQuery
{
    public string? Search { get; set; }
}
