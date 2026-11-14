using Newlife.Application.Common.Models;

namespace Newlife.Application.Events.DTOs;

public sealed record EventUpsertRequest(
    string Title,
    string? Slug,
    string? Description,
    string? Venue,
    DateTime StartAt,
    DateTime? EndAt,
    Guid? BannerMediaId,
    bool IsPublished,
    bool IsFeatured,
    bool TicketingEnabled);

public sealed record PublishEventRequest(bool IsPublished);

public sealed record TicketTypeUpsertRequest(
    string Name,
    string? Description,
    decimal Price,
    int Quantity,
    int MaxPerOrder,
    DateTime? SalesStartAt,
    DateTime? SalesEndAt,
    bool IsActive);

public sealed record PublicEventResponse(
    Guid Id,
    string Slug,
    string Title,
    string? Description,
    string? Venue,
    DateTime StartAt,
    DateTime? EndAt,
    string? BannerUrl,
    bool IsFeatured,
    bool TicketingEnabled);

public sealed record AdminEventResponse(
    Guid Id,
    string Slug,
    string Title,
    string? Description,
    string? Venue,
    DateTime StartAt,
    DateTime? EndAt,
    Guid? BannerMediaId,
    string? BannerUrl,
    bool IsPublished,
    bool IsFeatured,
    bool TicketingEnabled,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    IReadOnlyList<TicketTypeAdminResponse> TicketTypes);

public sealed record PublicTicketTypeResponse(
    Guid Id,
    string Name,
    string? Description,
    decimal Price,
    string Currency,
    int Remaining,
    int MaxPerOrder,
    DateTime? SalesStartAt,
    DateTime? SalesEndAt,
    bool IsOnSale);

public sealed record TicketTypeAdminResponse(
    Guid Id,
    Guid EventId,
    string Name,
    string? Description,
    decimal Price,
    string Currency,
    int Quantity,
    int MaxPerOrder,
    DateTime? SalesStartAt,
    DateTime? SalesEndAt,
    bool IsActive,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public sealed class EventPublicQuery : PaginationQuery
{
    public bool? Upcoming { get; set; }

    public bool? Featured { get; set; }
}

public sealed class EventAdminQuery : PaginationQuery
{
    public string? Search { get; set; }

    public bool? Published { get; set; }
}
