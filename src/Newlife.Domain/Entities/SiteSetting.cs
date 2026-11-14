using Newlife.Domain.Common;

namespace Newlife.Domain.Entities;

public sealed class SiteSetting : BaseEntity
{
    public string Key { get; set; } = string.Empty;

    public string Value { get; set; } = string.Empty;

    public string Group { get; set; } = string.Empty;
}
