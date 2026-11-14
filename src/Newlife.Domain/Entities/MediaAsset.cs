using Newlife.Domain.Common;

namespace Newlife.Domain.Entities;

public sealed class MediaAsset : BaseEntity
{
    public string StorageKey { get; set; } = string.Empty;

    public string Url { get; set; } = string.Empty;

    public string FileName { get; set; } = string.Empty;

    public string ContentType { get; set; } = string.Empty;

    public long? SizeBytes { get; set; }

    public string? Title { get; set; }

    public string? AltText { get; set; }
}
