using Newlife.Domain.Common;

namespace Newlife.Domain.Entities;

public sealed class Event : BaseEntity
{
    public string Slug { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string? Venue { get; set; }

    public DateTime StartAt { get; set; }

    public DateTime? EndAt { get; set; }

    public Guid? BannerMediaId { get; set; }

    public string? BannerUrl { get; set; }

    public bool IsPublished { get; set; }

    public bool IsFeatured { get; set; }

    public bool TicketingEnabled { get; set; }
}
