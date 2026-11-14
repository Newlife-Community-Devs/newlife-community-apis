using Newlife.Domain.Common;

namespace Newlife.Domain.Entities;

public sealed class SermonEpisode : BaseEntity
{
    public string FeedGuid { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string? DescriptionHtml { get; set; }

    public string? Summary { get; set; }

    public string AudioUrl { get; set; } = string.Empty;

    public int? DurationSeconds { get; set; }

    public string? ArtworkUrl { get; set; }

    public DateTime PublishedAt { get; set; }

    public bool IsVisible { get; set; } = true;
}
