using Microsoft.EntityFrameworkCore;
using Newlife.Application.Common.Exceptions;
using Newlife.Application.Common.Interfaces;
using Newlife.Application.Settings.DTOs;

namespace Newlife.Application.Settings;

public sealed class SettingsService(IAppDbContext db)
{
    public static readonly IReadOnlyList<string> PublicKeys = ["sermons.latestYoutubeUrl", "home.heroAnnouncement"];

    public async Task<Dictionary<string, string>> GetPublicAsync(CancellationToken cancellationToken)
    {
        var settings = await db.Set<Domain.Entities.SiteSetting>()
            .AsNoTracking()
            .Where(setting => PublicKeys.Contains(setting.Key))
            .ToListAsync(cancellationToken);

        return PublicKeys
            .Select(key => (key, value: settings.FirstOrDefault(setting => setting.Key == key)?.Value ?? string.Empty))
            .ToDictionary(entry => entry.key, entry => entry.value);
    }

    public async Task<Dictionary<string, Dictionary<string, string>>> GetAllAsync(CancellationToken cancellationToken)
    {
        var settings = await db.Set<Domain.Entities.SiteSetting>()
            .AsNoTracking()
            .OrderBy(setting => setting.Group)
            .ThenBy(setting => setting.Key)
            .ToListAsync(cancellationToken);

        return settings
            .GroupBy(setting => setting.Group)
            .ToDictionary(
                group => group.Key,
                group => group.ToDictionary(setting => setting.Key, setting => setting.Value));
    }

    public async Task<SettingResponse> UpsertAsync(
        string key,
        UpdateSettingRequest request,
        CancellationToken cancellationToken)
    {
        var setting = await db.Set<Domain.Entities.SiteSetting>()
                   .FirstOrDefaultAsync(item => item.Key == key, cancellationToken)
               ?? throw new BadRequestException("setting.unknown_key", $"Setting '{key}' does not exist.");

        setting.Value = request.Value;
        await db.SaveChangesAsync(cancellationToken);

        return new SettingResponse(setting.Key, setting.Value, setting.Group);
    }
}
