using Microsoft.EntityFrameworkCore;
using Newlife.Application.Common.Exceptions;
using Newlife.Application.Common.Interfaces;
using Newlife.Application.Common.Models;
using Newlife.Application.Prayers.DTOs;
using Newlife.Domain.Entities;

namespace Newlife.Application.Prayers;

public sealed class PrayerService(IAppDbContext db)
{
    public async Task<PublicPrayerResponse> SubmitAsync(
        SubmitPrayerRequest request,
        CancellationToken cancellationToken)
    {
        var prayer = new PrayerRequest
        {
            Title = string.IsNullOrWhiteSpace(request.Title) ? null : request.Title.Trim(),
            Content = request.Content.Trim(),
            SubmitterName = request.IsAnonymous ? null : request.SubmitterName?.Trim(),
            PhoneNumber = string.IsNullOrWhiteSpace(request.PhoneNumber) ? null : request.PhoneNumber.Trim(),
            IsAnonymous = request.IsAnonymous
        };

        db.Set<PrayerRequest>().Add(prayer);
        await db.SaveChangesAsync(cancellationToken);

        return ToPublic(prayer);
    }

    public async Task<PagedResult<PublicPrayerResponse>> GetWallAsync(
        PrayerWallQuery query,
        CancellationToken cancellationToken)
    {
        IQueryable<PrayerRequest> source = db.Set<PrayerRequest>()
            .AsNoTracking()
            .Where(prayer => prayer.DeletedAt == null && !prayer.IsFlagged);

        var totalCount = await source.CountAsync(cancellationToken);

        source = (query.Sort, query.Order) switch
        {
            ("answeredAt", "asc") => source.OrderBy(prayer => prayer.AnsweredAt),
            ("answeredAt", _) => source.OrderByDescending(prayer => prayer.AnsweredAt),
            (_, "asc") => source.OrderBy(prayer => prayer.CreatedAt),
            _ => source.OrderByDescending(prayer => prayer.CreatedAt)
        };

        var items = await source
            .Skip(query.Skip)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        return PagedResult<PublicPrayerResponse>.Create(
            items.Select(ToPublic).ToArray(),
            query.Page,
            query.PageSize,
            totalCount);
    }

    public async Task<PagedResult<AdminPrayerResponse>> ListAsync(
        PrayerAdminQuery query,
        CancellationToken cancellationToken)
    {
        IQueryable<PrayerRequest> source = db.Set<PrayerRequest>()
            .AsNoTracking()
            .Where(prayer => prayer.DeletedAt == null);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var pattern = $"%{query.Search.Trim().ToLowerInvariant()}%";
            source = source.Where(prayer =>
                EF.Functions.Like(prayer.Content.ToLower(), pattern)
                || (prayer.Title != null && EF.Functions.Like(prayer.Title.ToLower(), pattern))
                || (prayer.SubmitterName != null && EF.Functions.Like(prayer.SubmitterName.ToLower(), pattern)));
        }

        source = query.Status switch
        {
            "open" => source.Where(prayer => !prayer.IsAnswered),
            "answered" => source.Where(prayer => prayer.IsAnswered),
            "flagged" => source.Where(prayer => prayer.IsFlagged),
            _ => source
        };

        var totalCount = await source.CountAsync(cancellationToken);
        var items = await source
            .OrderByDescending(prayer => prayer.CreatedAt)
            .Skip(query.Skip)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        return PagedResult<AdminPrayerResponse>.Create(
            items.Select(ToAdmin).ToArray(),
            query.Page,
            query.PageSize,
            totalCount);
    }

    public async Task<PrayerStatsResponse> GetStatsAsync(CancellationToken cancellationToken)
    {
        var source = db.Set<PrayerRequest>()
            .AsNoTracking()
            .Where(prayer => prayer.DeletedAt == null);

        var lastWeek = DateTime.UtcNow.AddDays(-7);

        return new PrayerStatsResponse(
            await source.CountAsync(cancellationToken),
            await source.CountAsync(prayer => !prayer.IsAnswered, cancellationToken),
            await source.CountAsync(prayer => prayer.IsAnswered, cancellationToken),
            await source.CountAsync(prayer => prayer.IsFlagged, cancellationToken),
            await source.CountAsync(prayer => prayer.IsAnonymous, cancellationToken),
            await source.CountAsync(prayer => prayer.CreatedAt >= lastWeek, cancellationToken),
            await source
                .Where(prayer => prayer.SubmitterName != null)
                .Select(prayer => prayer.SubmitterName)
                .Distinct()
                .CountAsync(cancellationToken));
    }

    public async Task<AdminPrayerResponse> AnswerAsync(Guid id, CancellationToken cancellationToken)
    {
        var prayer = await FindAsync(id, cancellationToken);

        if (!prayer.IsAnswered)
        {
            prayer.IsAnswered = true;
            prayer.AnsweredAt = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
        }

        return ToAdmin(prayer);
    }

    public async Task<AdminPrayerResponse> FlagAsync(
        Guid id,
        FlagPrayerRequest request,
        CancellationToken cancellationToken)
    {
        var prayer = await FindAsync(id, cancellationToken);

        prayer.IsFlagged = true;
        prayer.FlagReason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim();
        await db.SaveChangesAsync(cancellationToken);

        return ToAdmin(prayer);
    }

    public async Task<AdminPrayerResponse> UnflagAsync(Guid id, CancellationToken cancellationToken)
    {
        var prayer = await FindAsync(id, cancellationToken);

        prayer.IsFlagged = false;
        prayer.FlagReason = null;
        await db.SaveChangesAsync(cancellationToken);

        return ToAdmin(prayer);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var prayer = await FindAsync(id, cancellationToken);

        prayer.DeletedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task<PrayerRequest> FindAsync(Guid id, CancellationToken cancellationToken)
        => await db.Set<PrayerRequest>().FirstOrDefaultAsync(prayer => prayer.Id == id, cancellationToken)
           ?? throw new NotFoundException("prayer.not_found", "Prayer request was not found.");

    private static PublicPrayerResponse ToPublic(PrayerRequest prayer)
        => new(
            prayer.Id,
            prayer.Title,
            prayer.Content,
            prayer.IsAnonymous ? null : prayer.SubmitterName,
            prayer.IsAnonymous,
            prayer.IsAnswered,
            prayer.AnsweredAt,
            prayer.CreatedAt);

    private static AdminPrayerResponse ToAdmin(PrayerRequest prayer)
        => new(
            prayer.Id,
            prayer.Title,
            prayer.Content,
            prayer.IsAnonymous ? null : prayer.SubmitterName,
            prayer.PhoneNumber,
            prayer.IsAnonymous,
            prayer.IsAnswered,
            prayer.AnsweredAt,
            prayer.IsFlagged,
            prayer.FlagReason,
            prayer.CreatedAt,
            prayer.UpdatedAt);
}
