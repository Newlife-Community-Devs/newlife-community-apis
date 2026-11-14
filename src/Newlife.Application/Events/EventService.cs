using System.Text;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Newlife.Application.Common.Exceptions;
using Newlife.Application.Common.Interfaces;
using Newlife.Application.Common.Models;
using Newlife.Application.Events.DTOs;
using Newlife.Domain.Entities;

namespace Newlife.Application.Events;

public sealed class EventService(IAppDbContext db)
{
    public async Task<PagedResult<PublicEventResponse>> ListPublicAsync(
        EventPublicQuery query,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        IQueryable<Event> source = db.Set<Event>().AsNoTracking().Where(evt => evt.IsPublished);

        if (query.Upcoming == true)
        {
            source = source.Where(evt => (evt.EndAt ?? evt.StartAt) >= now);
        }

        if (query.Featured.HasValue)
        {
            source = source.Where(evt => evt.IsFeatured == query.Featured.Value);
        }

        var totalCount = await source.CountAsync(cancellationToken);

        source = query.Upcoming == true
            ? source.OrderBy(evt => evt.StartAt)
            : source.OrderByDescending(evt => evt.StartAt);

        var items = await source.Skip(query.Skip).Take(query.PageSize).ToListAsync(cancellationToken);

        return PagedResult<PublicEventResponse>.Create(
            items.Select(ToPublic).ToArray(),
            query.Page,
            query.PageSize,
            totalCount);
    }

    public async Task<PublicEventResponse> GetBySlugAsync(string slug, CancellationToken cancellationToken)
        => ToPublic(await FindPublishedAsync(slug, cancellationToken));

    public async Task<IReadOnlyList<PublicTicketTypeResponse>> GetPublicTicketTypesAsync(
        string slug,
        CancellationToken cancellationToken)
    {
        var evt = await FindPublishedAsync(slug, cancellationToken);

        if (!evt.TicketingEnabled)
        {
            throw new NotFoundException("ticketing.disabled", "Ticketing is not enabled for this event.");
        }

        var now = DateTime.UtcNow;
        var types = await db.Set<TicketType>()
            .AsNoTracking()
            .Where(type => type.EventId == evt.Id)
            .OrderBy(type => type.Name)
            .ToListAsync(cancellationToken);

        return types.Select(type => new PublicTicketTypeResponse(
            type.Id,
            type.Name,
            type.Description,
            type.Price,
            "GHS",
            type.Quantity,
            type.MaxPerOrder,
            type.SalesStartAt,
            type.SalesEndAt,
            IsOnSale(type, now, soldCount: 0))).ToArray();
    }

    public async Task<PagedResult<AdminEventResponse>> ListAdminAsync(
        EventAdminQuery query,
        CancellationToken cancellationToken)
    {
        IQueryable<Event> source = db.Set<Event>().AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var pattern = $"%{query.Search.Trim().ToLowerInvariant()}%";
            source = source.Where(evt =>
                EF.Functions.Like(evt.Title.ToLower(), pattern)
                || EF.Functions.Like(evt.Slug.ToLower(), pattern));
        }

        if (query.Published.HasValue)
        {
            source = source.Where(evt => evt.IsPublished == query.Published.Value);
        }

        var totalCount = await source.CountAsync(cancellationToken);
        var events = await source
            .OrderByDescending(evt => evt.CreatedAt)
            .Skip(query.Skip)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        var eventIds = events.Select(evt => evt.Id).ToArray();
        var typesByEvent = (await db.Set<TicketType>()
                .AsNoTracking()
                .Where(type => eventIds.Contains(type.EventId))
                .OrderBy(type => type.Name)
                .ToListAsync(cancellationToken))
            .ToLookup(type => type.EventId);

        return PagedResult<AdminEventResponse>.Create(
            events.Select(evt => ToAdmin(evt, typesByEvent[evt.Id].ToArray())).ToArray(),
            query.Page,
            query.PageSize,
            totalCount);
    }

    public async Task<AdminEventResponse> CreateAsync(
        EventUpsertRequest request,
        CancellationToken cancellationToken)
    {
        await ValidateBannerAsync(request.BannerMediaId, cancellationToken);

        var evt = new Event
        {
            Slug = await ResolveSlugAsync(request.Slug, request.Title, existingId: null, cancellationToken),
            Title = request.Title.Trim(),
            Description = request.Description,
            Venue = string.IsNullOrWhiteSpace(request.Venue) ? null : request.Venue.Trim(),
            StartAt = request.StartAt,
            EndAt = request.EndAt,
            BannerMediaId = request.BannerMediaId,
            BannerUrl = await ResolveBannerUrlAsync(request.BannerMediaId, cancellationToken),
            IsPublished = request.IsPublished,
            IsFeatured = request.IsFeatured,
            TicketingEnabled = request.TicketingEnabled
        };

        db.Set<Event>().Add(evt);
        await db.SaveChangesAsync(cancellationToken);

        return ToAdmin(evt, []);
    }

    public async Task<AdminEventResponse> UpdateAsync(
        Guid id,
        EventUpsertRequest request,
        CancellationToken cancellationToken)
    {
        var evt = await FindAdminAsync(id, cancellationToken);
        await ValidateBannerAsync(request.BannerMediaId, cancellationToken);

        evt.Slug = await ResolveSlugAsync(request.Slug ?? evt.Slug, request.Title, existingId: evt.Id, cancellationToken);
        evt.Title = request.Title.Trim();
        evt.Description = request.Description;
        evt.Venue = string.IsNullOrWhiteSpace(request.Venue) ? null : request.Venue.Trim();
        evt.StartAt = request.StartAt;
        evt.EndAt = request.EndAt;
        evt.BannerMediaId = request.BannerMediaId;
        evt.BannerUrl = await ResolveBannerUrlAsync(request.BannerMediaId, cancellationToken);
        evt.IsPublished = request.IsPublished;
        evt.IsFeatured = request.IsFeatured;
        evt.TicketingEnabled = request.TicketingEnabled;

        await db.SaveChangesAsync(cancellationToken);

        var types = await db.Set<TicketType>()
            .AsNoTracking()
            .Where(type => type.EventId == evt.Id)
            .OrderBy(type => type.Name)
            .ToArrayAsync(cancellationToken);

        return ToAdmin(evt, types);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var evt = await FindAdminAsync(id, cancellationToken);

        var types = await db.Set<TicketType>()
            .Where(type => type.EventId == evt.Id)
            .ToListAsync(cancellationToken);

        db.Set<TicketType>().RemoveRange(types);
        db.Set<Event>().Remove(evt);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<AdminEventResponse> PublishAsync(
        Guid id,
        PublishEventRequest request,
        CancellationToken cancellationToken)
    {
        var evt = await FindAdminAsync(id, cancellationToken);

        evt.IsPublished = request.IsPublished;
        await db.SaveChangesAsync(cancellationToken);

        var types = await db.Set<TicketType>()
            .AsNoTracking()
            .Where(type => type.EventId == evt.Id)
            .OrderBy(type => type.Name)
            .ToArrayAsync(cancellationToken);

        return ToAdmin(evt, types);
    }

    public async Task<TicketTypeAdminResponse> CreateTicketTypeAsync(
        Guid eventId,
        TicketTypeUpsertRequest request,
        CancellationToken cancellationToken)
    {
        if (!await db.Set<Event>().AnyAsync(evt => evt.Id == eventId, cancellationToken))
        {
            throw new NotFoundException("event.not_found", "Event was not found.");
        }

        var type = new TicketType
        {
            EventId = eventId,
            Name = request.Name.Trim(),
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            Price = request.Price,
            Quantity = request.Quantity,
            MaxPerOrder = request.MaxPerOrder,
            SalesStartAt = request.SalesStartAt,
            SalesEndAt = request.SalesEndAt,
            IsActive = request.IsActive
        };

        db.Set<TicketType>().Add(type);
        await db.SaveChangesAsync(cancellationToken);

        return ToTicketTypeAdmin(type);
    }

    public async Task<TicketTypeAdminResponse> UpdateTicketTypeAsync(
        Guid id,
        TicketTypeUpsertRequest request,
        CancellationToken cancellationToken)
    {
        var type = await FindTicketTypeAsync(id, cancellationToken);

        type.Name = request.Name.Trim();
        type.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        type.Price = request.Price;
        type.Quantity = request.Quantity;
        type.MaxPerOrder = request.MaxPerOrder;
        type.SalesStartAt = request.SalesStartAt;
        type.SalesEndAt = request.SalesEndAt;
        type.IsActive = request.IsActive;

        await db.SaveChangesAsync(cancellationToken);

        return ToTicketTypeAdmin(type);
    }

    public async Task DeleteTicketTypeAsync(Guid id, CancellationToken cancellationToken)
    {
        var type = await FindTicketTypeAsync(id, cancellationToken);

        db.Set<TicketType>().Remove(type);
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task<Event> FindPublishedAsync(string slug, CancellationToken cancellationToken)
        => await db.Set<Event>()
               .AsNoTracking()
               .FirstOrDefaultAsync(evt => evt.Slug == slug && evt.IsPublished, cancellationToken)
           ?? throw new NotFoundException("event.not_found", "Event was not found.");

    private async Task<Event> FindAdminAsync(Guid id, CancellationToken cancellationToken)
        => await db.Set<Event>()
               .FirstOrDefaultAsync(evt => evt.Id == id, cancellationToken)
           ?? throw new NotFoundException("event.not_found", "Event was not found.");

    private async Task<TicketType> FindTicketTypeAsync(Guid id, CancellationToken cancellationToken)
        => await db.Set<TicketType>()
               .FirstOrDefaultAsync(type => type.Id == id, cancellationToken)
           ?? throw new NotFoundException("ticket_type.not_found", "Ticket type was not found.");

    private async Task ValidateBannerAsync(Guid? bannerMediaId, CancellationToken cancellationToken)
    {
        if (bannerMediaId is null)
        {
            return;
        }

        if (!await db.Set<MediaAsset>().AnyAsync(media => media.Id == bannerMediaId, cancellationToken))
        {
            throw new NotFoundException("media.not_found", "Banner media asset was not found.");
        }
    }

    private async Task<string?> ResolveBannerUrlAsync(Guid? bannerMediaId, CancellationToken cancellationToken)
    {
        if (bannerMediaId is null)
        {
            return null;
        }

        var media = await db.Set<MediaAsset>()
            .AsNoTracking()
            .FirstOrDefaultAsync(asset => asset.Id == bannerMediaId, cancellationToken);

        return media?.Url;
    }

    private static readonly Regex ValidSlugPattern = new("^[a-z0-9]+(-[a-z0-9]+)*$", RegexOptions.Compiled);

    private async Task<string> ResolveSlugAsync(
        string? requestedSlug,
        string title,
        Guid? existingId,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(requestedSlug))
        {
            var slug = requestedSlug.Trim().ToLowerInvariant();
            if (!ValidSlugPattern.IsMatch(slug))
            {
                throw new BadRequestException(
                    "event.invalid_slug",
                    "The slug must contain only lowercase letters, numbers and single hyphens.");
            }

            if (await IsTakenAsync(slug, existingId, cancellationToken))
            {
                throw new ConflictException("event.slug_taken", $"The slug '{slug}' is already in use.");
            }

            return slug;
        }

        var generated = Slugify(title);
        var candidate = generated;
        var suffix = 2;
        while (await IsTakenAsync(candidate, existingId, cancellationToken))
        {
            candidate = $"{generated}-{suffix}";
            suffix++;
        }

        return candidate;
    }

    private async Task<bool> IsTakenAsync(string slug, Guid? existingId, CancellationToken cancellationToken)
        => await db.Set<Event>().AnyAsync(
            evt => evt.Slug == slug && (existingId == null || evt.Id != existingId),
            cancellationToken);

    private static string Slugify(string value)
    {
        var builder = new StringBuilder();

        foreach (var character in value.Trim().ToLowerInvariant())
        {
            if (character is >= 'a' and <= 'z' or >= '0' and <= '9')
            {
                builder.Append(character);
            }
            else if (char.IsWhiteSpace(character) || character is '-' or '_' or '/')
            {
                builder.Append('-');
            }
        }

        return Regex.Replace(builder.ToString(), "-{2,}", "-").Trim('-');
    }

    private static bool IsOnSale(TicketType type, DateTime now, int soldCount)
    {
        var windowOpen = (type.SalesStartAt == null || type.SalesStartAt <= now)
                         && (type.SalesEndAt == null || type.SalesEndAt > now);

        return type.IsActive && windowOpen && type.Quantity - soldCount > 0;
    }

    private static PublicEventResponse ToPublic(Event evt)
        => new(
            evt.Id,
            evt.Slug,
            evt.Title,
            evt.Description,
            evt.Venue,
            evt.StartAt,
            evt.EndAt,
            evt.BannerUrl,
            evt.IsFeatured,
            evt.TicketingEnabled);

    private static AdminEventResponse ToAdmin(Event evt, IReadOnlyList<TicketType> types)
        => new(
            evt.Id,
            evt.Slug,
            evt.Title,
            evt.Description,
            evt.Venue,
            evt.StartAt,
            evt.EndAt,
            evt.BannerMediaId,
            evt.BannerUrl,
            evt.IsPublished,
            evt.IsFeatured,
            evt.TicketingEnabled,
            evt.CreatedAt,
            evt.UpdatedAt,
            types.Select(ToTicketTypeAdmin).ToArray());

    private static TicketTypeAdminResponse ToTicketTypeAdmin(TicketType type)
        => new(
            type.Id,
            type.EventId,
            type.Name,
            type.Description,
            type.Price,
            "GHS",
            type.Quantity,
            type.MaxPerOrder,
            type.SalesStartAt,
            type.SalesEndAt,
            type.IsActive,
            type.CreatedAt,
            type.UpdatedAt);
}
