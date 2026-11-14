using Newlife.Domain.Common;

namespace Newlife.Domain.Entities;

public sealed class HubtelCallbackLog : BaseEntity
{
    public DateTime ReceivedAt { get; set; }

    public string? SourceIp { get; set; }

    public string? ClientReference { get; set; }

    public string Payload { get; set; } = string.Empty;

    public string? ProcessingResult { get; set; }

    public DateTime? ProcessedAt { get; set; }
}
