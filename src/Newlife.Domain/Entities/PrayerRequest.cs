using Newlife.Domain.Common;

namespace Newlife.Domain.Entities;

public sealed class PrayerRequest : BaseEntity
{
    public string? Title { get; set; }

    public string Content { get; set; } = string.Empty;

    public string? SubmitterName { get; set; }

    public string? PhoneNumber { get; set; }

    public bool IsAnonymous { get; set; }

    public bool IsAnswered { get; set; }

    public DateTime? AnsweredAt { get; set; }

    public bool IsFlagged { get; set; }

    public string? FlagReason { get; set; }

    public DateTime? DeletedAt { get; set; }
}
