using Microsoft.EntityFrameworkCore;
using Newlife.Application.Common.Exceptions;
using Newlife.Application.Common.Interfaces;
using Newlife.Application.Common.Models;
using Newlife.Application.Sermons.DTOs;
using Newlife.Domain.Entities;

namespace Newlife.Application.Sermons;

public sealed class SermonService(IAppDbContext db, IPodcastFeedClient feedClient)
{
    public async Task<PagedResult<SermonListResponse>> ListPublicAsync(
        PaginationQuery query,
        CancellationToken cancellationToken)
    {
        var source = db.Set<SermonEpisode>().AsNoTracking().Where(episode => episode.IsVisible);

        var totalCount = await source.CountAsync(cancellationToken);
        var items = await source
            .OrderByDescending(episode => episode.PublishedAt)
            .Skip(query.Skip)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        return PagedResult<SermonListResponse>.Create(
            items.Select(ToListResponse).ToArray(),
            query.Page,
            query.PageSize,
            totalCount);
    }

    public async Task<SermonDetailResponse> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var episode = await db.Set<SermonEpisode>()
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == id && item.IsVisible, cancellationToken)
            ?? throw new NotFoundException("sermon.not_found", "Sermon episode was not found.");

        return ToDetailResponse(episode);
    }

    public async Task<LatestSermonResponse> GetLatestAsync(CancellationToken cancellationToken)
    {
        var episode = await db.Set<SermonEpisode>()
            .AsNoTracking()
            .Where(episode => episode.IsVisible)
            .OrderByDescending(episode => episode.PublishedAt)
            .FirstOrDefaultAsync(cancellationToken);

        var youtubeUrl = await db.Set<SiteSetting>()
            .AsNoTracking()
            .Where(setting => setting.Key == "sermons.latestYoutubeUrl")
            .Select(setting => setting.Value)
            .FirstOrDefaultAsync(cancellationToken) ?? string.Empty;

        return new LatestSermonResponse(episode is null ? null : ToListResponse(episode), youtubeUrl);
    }

    public async Task<PagedResult<SermonDetailResponse>> ListAdminAsync(
        SermonAdminQuery query,
        CancellationToken cancellationToken)
    {
        IQueryable<SermonEpisode> source = db.Set<SermonEpisode>().AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var pattern = $"%{query.Search.Trim().ToLowerInvariant()}%";
            source = source.Where(episode => EF.Functions.Like(episode.Title.ToLower(), pattern));
        }

        var totalCount = await source.CountAsync(cancellationToken);
        var items = await source
            .OrderByDescending(episode => episode.PublishedAt)
            .Skip(query.Skip)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        return PagedResult<SermonDetailResponse>.Create(
            items.Select(ToDetailResponse).ToArray(),
            query.Page,
            query.PageSize,
            totalCount);
    }

    public async Task<SermonDetailResponse> UpdateVisibilityAsync(
        Guid id,
        UpdateSermonVisibilityRequest request,
        CancellationToken cancellationToken)
    {
        var episode = await db.Set<SermonEpisode>()
                   .FirstOrDefaultAsync(item => item.Id == id, cancellationToken)
               ?? throw new NotFoundException("sermon.not_found", "Sermon episode was not found.");

        episode.IsVisible = request.IsVisible;
        await db.SaveChangesAsync(cancellationToken);

        return ToDetailResponse(episode);
    }

    public async Task<SyncResultResponse> SyncAsync(CancellationToken cancellationToken)
    {
        var feedEpisodes = await feedClient.FetchAsync(cancellationToken);
        var existing = await db.Set<SermonEpisode>()
            .Where(episode => feedEpisodes.Select(feed => feed.Guid).Contains(episode.FeedGuid))
            .ToDictionaryAsync(episode => episode.FeedGuid, cancellationToken);

        var added = 0;
        var updated = 0;

        foreach (var feed in feedEpisodes)
        {
            if (existing.TryGetValue(feed.Guid, out var episode))
            {
                episode.Title = Truncate(feed.Title.Trim(), 500)!;
                episode.DescriptionHtml = feed.DescriptionHtml;
                episode.Summary = PlainSummary(feed.DescriptionHtml);
                episode.DurationSeconds = feed.DurationSeconds;
                episode.ArtworkUrl = Truncate(feed.ArtworkUrl, 1000);
                episode.PublishedAt = feed.PublishedAt;
                updated++;
            }
            else
            {
                db.Set<SermonEpisode>().Add(new SermonEpisode
                {
                    FeedGuid = feed.Guid,
                    Title = Truncate(feed.Title.Trim(), 500)!,
                    DescriptionHtml = feed.DescriptionHtml,
                    Summary = PlainSummary(feed.DescriptionHtml),
                    AudioUrl = feed.AudioUrl,
                    DurationSeconds = feed.DurationSeconds,
                    ArtworkUrl = Truncate(feed.ArtworkUrl, 1000),
                    PublishedAt = feed.PublishedAt,
                    IsVisible = true
                });
                added++;
            }
        }

        await db.SaveChangesAsync(cancellationToken);

        var total = await db.Set<SermonEpisode>().CountAsync(cancellationToken);

        return new SyncResultResponse(added, updated, total);
    }

    private static string? Truncate(string? value, int maxLength)
        => value is null || value.Length <= maxLength ? value : value[..maxLength];

    private static string PlainSummary(string html)
    {
        var text = System.Text.RegularExpressions.Regex.Replace(html, "<[^>]+>", " ");
        text = System.Text.RegularExpressions.Regex.Replace(text, @"\s+", " ").Trim();
        return text.Length <= 400 ? text : text[..400];
    }

    private static SermonListResponse ToListResponse(SermonEpisode episode)
        => new(
            episode.Id,
            episode.Title,
            episode.Summary,
            episode.AudioUrl,
            episode.DurationSeconds,
            episode.ArtworkUrl,
            episode.PublishedAt);

    private static SermonDetailResponse ToDetailResponse(SermonEpisode episode)
        => new(
            episode.Id,
            episode.Title,
            episode.Summary,
            episode.DescriptionHtml,
            episode.AudioUrl,
            episode.DurationSeconds,
            episode.ArtworkUrl,
            episode.PublishedAt,
            episode.IsVisible);
}
