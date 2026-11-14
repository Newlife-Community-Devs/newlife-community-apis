namespace Newlife.Application.Common.Models;

public sealed record PresignedUpload(
    string Url,
    string Method,
    IReadOnlyDictionary<string, string> Headers,
    string Key,
    string PublicUrl,
    DateTimeOffset ExpiresAt);
