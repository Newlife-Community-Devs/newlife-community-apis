namespace Newlife.Application.Common.Models;

public sealed record PresignedUpload(
    string Url,
    string Method,
    string Key,
    DateTimeOffset ExpiresAt);
