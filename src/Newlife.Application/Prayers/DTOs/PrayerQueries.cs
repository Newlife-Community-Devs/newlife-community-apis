using Newlife.Application.Common.Models;

namespace Newlife.Application.Prayers.DTOs;

public sealed class PrayerWallQuery : PaginationQuery
{
    public string Sort { get; set; } = "createdAt";

    public string Order { get; set; } = "desc";
}

public sealed class PrayerAdminQuery : PaginationQuery
{
    public string? Search { get; set; }

    public string Status { get; set; } = "all";
}
