using Newlife.Domain.Common;

namespace Newlife.Domain.Entities;

public sealed class Announcement : BaseEntity
{
    public string Title { get; set; } = string.Empty;

    public string Body { get; set; } = string.Empty;

    public bool IsPinned { get; set; }

    public bool IsPublished { get; set; }

    public DateTime? PublishAt { get; set; }

    public DateTime? ExpiresAt { get; set; }
}
