using Newlife.Application.Common.Models;

namespace Newlife.Application.Common.Interfaces;

public interface IStorageProvider
{
    Task<PresignedUpload> CreatePresignedUploadAsync(
        string key,
        string contentType,
        TimeSpan expiry,
        CancellationToken cancellationToken);

    Task<bool> ExistsAsync(string key, CancellationToken cancellationToken);

    Task DeleteAsync(string key, CancellationToken cancellationToken);

    string GetPublicUrl(string key);
}
