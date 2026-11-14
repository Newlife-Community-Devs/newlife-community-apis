using Newlife.Application.Common.Models;

namespace Newlife.Application.Media.DTOs;

public sealed record CreateUploadRequest(string FileName, string ContentType);

public sealed record RegisterMediaRequest(
    string Key,
    string FileName,
    string ContentType,
    long? SizeBytes,
    string? Title,
    string? AltText);

public sealed record MediaAssetResponse(
    Guid Id,
    string Url,
    string Key,
    string FileName,
    string ContentType,
    long? SizeBytes,
    string? Title,
    string? AltText,
    DateTime CreatedAt);

public sealed class MediaQuery : PaginationQuery
{
    public string? Search { get; set; }
}
