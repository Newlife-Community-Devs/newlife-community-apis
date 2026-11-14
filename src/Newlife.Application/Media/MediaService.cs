using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Newlife.Application.Common.Exceptions;
using Newlife.Application.Common.Interfaces;
using Newlife.Application.Common.Models;
using Newlife.Application.Media.DTOs;
using Newlife.Domain.Entities;

namespace Newlife.Application.Media;

public sealed partial class MediaService(IAppDbContext db, IStorageProvider storage, ILogger<MediaService> logger)
{
    private static readonly Dictionary<string, string> ExtensionByContentType = new(StringComparer.OrdinalIgnoreCase)
    {
        ["image/jpeg"] = "jpg",
        ["image/png"] = "png",
        ["image/webp"] = "webp"
    };

    [GeneratedRegex(@"^media/\d{4}/\d{2}/[0-9a-f]{32}\.(jpg|png|webp)$")]
    private static partial Regex KeyPattern();

    public async Task<PresignedUpload> CreatePresignedUploadAsync(
        CreateUploadRequest request,
        CancellationToken cancellationToken)
    {
        if (!ExtensionByContentType.TryGetValue(request.ContentType, out var extension))
        {
            throw new BadRequestException(
                "media.invalid_content_type",
                "Only image/jpeg, image/png and image/webp uploads are allowed.");
        }

        var now = DateTime.UtcNow;
        var key = $"media/{now:yyyy}/{now:MM}/{Guid.NewGuid():N}.{extension}";

        return await storage.CreatePresignedUploadAsync(key, request.ContentType, TimeSpan.FromMinutes(15), cancellationToken);
    }

    public async Task<MediaAssetResponse> RegisterAsync(
        RegisterMediaRequest request,
        CancellationToken cancellationToken)
    {
        if (!KeyPattern().IsMatch(request.Key))
        {
            throw new BadRequestException("media.invalid_key", "The media key is not valid.");
        }

        if (!ExtensionByContentType.TryGetValue(request.ContentType, out var expectedExtension) ||
            !request.Key.EndsWith($".{expectedExtension}", StringComparison.OrdinalIgnoreCase))
        {
            throw new BadRequestException(
                "media.invalid_content_type",
                "The content type does not match the uploaded key.");
        }

        if (await db.Set<MediaAsset>().AnyAsync(media => media.StorageKey == request.Key, cancellationToken))
        {
            throw new ConflictException("media.duplicate_key", "This media file was already registered.");
        }

        if (!await storage.ExistsAsync(request.Key, cancellationToken))
        {
            throw new BadRequestException("media.object_missing", "The uploaded object was not found.");
        }

        var media = new MediaAsset
        {
            StorageKey = request.Key,
            Url = storage.GetPublicUrl(request.Key),
            FileName = request.FileName.Trim(),
            ContentType = request.ContentType,
            SizeBytes = request.SizeBytes,
            Title = string.IsNullOrWhiteSpace(request.Title) ? null : request.Title.Trim(),
            AltText = string.IsNullOrWhiteSpace(request.AltText) ? null : request.AltText.Trim()
        };

        db.Set<MediaAsset>().Add(media);
        await db.SaveChangesAsync(cancellationToken);

        return ToResponse(media);
    }

    public async Task<PagedResult<MediaAssetResponse>> ListAsync(
        MediaQuery query,
        CancellationToken cancellationToken)
    {
        IQueryable<MediaAsset> source = db.Set<MediaAsset>().AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var pattern = $"%{query.Search.Trim().ToLowerInvariant()}%";
            source = source.Where(media =>
                EF.Functions.Like(media.FileName.ToLower(), pattern)
                || (media.Title != null && EF.Functions.Like(media.Title.ToLower(), pattern))
                || (media.AltText != null && EF.Functions.Like(media.AltText.ToLower(), pattern)));
        }

        var totalCount = await source.CountAsync(cancellationToken);
        var items = await source
            .OrderByDescending(media => media.CreatedAt)
            .Skip(query.Skip)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        return PagedResult<MediaAssetResponse>.Create(
            items.Select(ToResponse).ToArray(),
            query.Page,
            query.PageSize,
            totalCount);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var media = await db.Set<MediaAsset>()
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken)
            ?? throw new NotFoundException("media.not_found", "Media asset was not found.");

        if (await db.Set<Event>().AnyAsync(evt => evt.BannerMediaId == id, cancellationToken))
        {
            throw new ConflictException("media.in_use", "This media asset is used as an event banner.");
        }

        try
        {
            await storage.DeleteAsync(media.StorageKey, cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Failed to delete storage object {Key}", media.StorageKey);
        }

        db.Set<MediaAsset>().Remove(media);
        await db.SaveChangesAsync(cancellationToken);
    }

    private static MediaAssetResponse ToResponse(MediaAsset media)
        => new(
            media.Id,
            media.Url,
            media.StorageKey,
            media.FileName,
            media.ContentType,
            media.SizeBytes,
            media.Title,
            media.AltText,
            media.CreatedAt);
}
