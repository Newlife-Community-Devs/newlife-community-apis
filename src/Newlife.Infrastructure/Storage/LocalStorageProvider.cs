using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Newlife.Application.Common.Interfaces;
using Newlife.Application.Common.Models;

namespace Newlife.Infrastructure.Storage;

public sealed class LocalStorageProvider(IWebHostEnvironment environment, IHttpContextAccessor httpContextAccessor)
    : IStorageProvider
{
    public Task<PresignedUpload> CreatePresignedUploadAsync(
        string key,
        string contentType,
        TimeSpan expiry,
        CancellationToken cancellationToken)
    {
        var baseUrl = GetBaseUrl();
        var expiresAt = DateTimeOffset.UtcNow.Add(expiry);

        var upload = new PresignedUpload(
            $"{baseUrl}/api/v1/dev/uploads/{key}",
            HttpMethods.Put,
            new Dictionary<string, string> { ["Content-Type"] = contentType },
            key,
            GetPublicUrl(key),
            expiresAt);

        return Task.FromResult(upload);
    }

    public Task<bool> ExistsAsync(string key, CancellationToken cancellationToken)
        => Task.FromResult(File.Exists(GetLocalPath(key)));

    public Task DeleteAsync(string key, CancellationToken cancellationToken)
    {
        var path = GetLocalPath(key);
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        return Task.CompletedTask;
    }

    public string GetPublicUrl(string key) => $"{GetBaseUrl()}/uploads/{key}";

    private string GetBaseUrl()
    {
        var request = httpContextAccessor.HttpContext?.Request;
        var host = request?.Host.ToString();
        if (request is not null && !string.IsNullOrEmpty(host))
        {
            return $"{request.Scheme}://{host}";
        }

        return "http://localhost:8080";
    }

    private string GetLocalPath(string key)
    {
        var root = Path.GetFullPath(Path.Combine(environment.WebRootPath, "uploads"));
        var target = Path.GetFullPath(Path.Combine(root, key));

        if (!target.StartsWith(root, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Upload key escapes the storage root.");
        }

        return target;
    }
}
