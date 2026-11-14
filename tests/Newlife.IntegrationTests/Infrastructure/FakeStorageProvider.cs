using System.Collections.Concurrent;
using Newlife.Application.Common.Interfaces;
using Newlife.Application.Common.Models;

namespace Newlife.IntegrationTests.Infrastructure;

public sealed class FakeStorageProvider : IStorageProvider
{
    private readonly ConcurrentDictionary<string, byte[]> _objects = new();

    public void Upload(string key) => _objects[key] = [1, 2, 3];

    public IReadOnlyCollection<string> Keys => _objects.Keys.ToArray();

    public Task<PresignedUpload> CreatePresignedUploadAsync(
        string key,
        string contentType,
        TimeSpan expiry,
        CancellationToken cancellationToken)
    {
        var expiresAt = DateTimeOffset.UtcNow.Add(expiry);
        return Task.FromResult(new PresignedUpload(
            $"https://uploads.fake.test/{key}?token=1",
            "PUT",
            new Dictionary<string, string> { ["Content-Type"] = contentType },
            key,
            GetPublicUrl(key),
            expiresAt));
    }

    public Task<bool> ExistsAsync(string key, CancellationToken cancellationToken)
        => Task.FromResult(_objects.ContainsKey(key));

    public Task DeleteAsync(string key, CancellationToken cancellationToken)
    {
        _objects.TryRemove(key, out _);
        return Task.CompletedTask;
    }

    public string GetPublicUrl(string key) => $"https://media.fake.test/{key}";
}
