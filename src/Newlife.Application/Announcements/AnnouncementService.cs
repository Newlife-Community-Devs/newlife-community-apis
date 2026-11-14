using Microsoft.EntityFrameworkCore;
using Newlife.Application.Announcements.DTOs;
using Newlife.Application.Common.Exceptions;
using Newlife.Application.Common.Interfaces;
using Newlife.Application.Common.Models;
using Newlife.Domain.Entities;

namespace Newlife.Application.Announcements;

public sealed class AnnouncementService(IAppDbContext db)
{
    public async Task<PagedResult<AnnouncementResponse>> ListPublicAsync(
        PaginationQuery query,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        IQueryable<Announcement> source = db.Set<Announcement>()
            .AsNoTracking()
            .Where(announcement => announcement.IsPublished
                                   && (announcement.PublishAt == null || announcement.PublishAt <= now)
                                   && (announcement.ExpiresAt == null || announcement.ExpiresAt > now));

        var totalCount = await source.CountAsync(cancellationToken);
        var items = await source
            .OrderByDescending(announcement => announcement.IsPinned)
            .ThenBy(announcement => announcement.PublishAt == null)
            .ThenByDescending(announcement => announcement.PublishAt)
            .ThenByDescending(announcement => announcement.CreatedAt)
            .Skip(query.Skip)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        return PagedResult<AnnouncementResponse>.Create(
            items.Select(ToResponse).ToArray(),
            query.Page,
            query.PageSize,
            totalCount);
    }

    public async Task<AnnouncementResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;

        return ToResponse(await db.Set<Announcement>()
            .AsNoTracking()
            .FirstOrDefaultAsync(
                announcement => announcement.Id == id
                                && announcement.IsPublished
                                && (announcement.PublishAt == null || announcement.PublishAt <= now)
                                && (announcement.ExpiresAt == null || announcement.ExpiresAt > now),
                cancellationToken)
            ?? throw new NotFoundException("announcement.not_found", "Announcement was not found."));
    }

    public async Task<PagedResult<AnnouncementResponse>> ListAdminAsync(
        AnnouncementAdminQuery query,
        CancellationToken cancellationToken)
    {
        IQueryable<Announcement> source = db.Set<Announcement>().AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var pattern = $"%{query.Search.Trim().ToLowerInvariant()}%";
            source = source.Where(announcement =>
                EF.Functions.Like(announcement.Title.ToLower(), pattern)
                || EF.Functions.Like(announcement.Body.ToLower(), pattern));
        }

        var totalCount = await source.CountAsync(cancellationToken);
        var items = await source
            .OrderByDescending(announcement => announcement.CreatedAt)
            .Skip(query.Skip)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        return PagedResult<AnnouncementResponse>.Create(
            items.Select(ToResponse).ToArray(),
            query.Page,
            query.PageSize,
            totalCount);
    }

    public async Task<AnnouncementResponse> CreateAsync(
        AnnouncementUpsertRequest request,
        CancellationToken cancellationToken)
    {
        var announcement = new Announcement
        {
            Title = request.Title.Trim(),
            Body = request.Body,
            IsPinned = request.IsPinned,
            IsPublished = request.IsPublished,
            PublishAt = request.PublishAt,
            ExpiresAt = request.ExpiresAt
        };

        db.Set<Announcement>().Add(announcement);
        await db.SaveChangesAsync(cancellationToken);

        return ToResponse(announcement);
    }

    public async Task<AnnouncementResponse> UpdateAsync(
        Guid id,
        AnnouncementUpsertRequest request,
        CancellationToken cancellationToken)
    {
        var announcement = await FindAsync(id, cancellationToken);

        announcement.Title = request.Title.Trim();
        announcement.Body = request.Body;
        announcement.IsPinned = request.IsPinned;
        announcement.IsPublished = request.IsPublished;
        announcement.PublishAt = request.PublishAt;
        announcement.ExpiresAt = request.ExpiresAt;

        await db.SaveChangesAsync(cancellationToken);

        return ToResponse(announcement);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var announcement = await FindAsync(id, cancellationToken);

        db.Set<Announcement>().Remove(announcement);
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task<Announcement> FindAsync(Guid id, CancellationToken cancellationToken)
        => await db.Set<Announcement>()
               .FirstOrDefaultAsync(announcement => announcement.Id == id, cancellationToken)
           ?? throw new NotFoundException("announcement.not_found", "Announcement was not found.");

    private static AnnouncementResponse ToResponse(Announcement announcement)
        => new(
            announcement.Id,
            announcement.Title,
            announcement.Body,
            announcement.IsPinned,
            announcement.PublishAt,
            announcement.ExpiresAt,
            announcement.CreatedAt);
}
