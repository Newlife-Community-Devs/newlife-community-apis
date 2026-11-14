using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;
using Newlife.Application.Common.Interfaces;
using Newlife.Application.Common.Models;

namespace Newlife.Infrastructure.Storage;

public sealed class R2StorageProvider(IOptions<StorageOptions> options) : IStorageProvider, IDisposable
{
    private readonly StorageOptions.R2Options _r2 = options.Value.R2;
    private readonly AmazonS3Client _client = CreateClient(options.Value.R2);

    private static AmazonS3Client CreateClient(StorageOptions.R2Options r2)
        => new(
            new BasicAWSCredentials(r2.AccessKeyId, r2.SecretAccessKey),
            new AmazonS3Config
            {
                ServiceURL = r2.ServiceUrl,
                AuthenticationRegion = "auto",
                ForcePathStyle = true,
                RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED,
                ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED
            });

    public Task<PresignedUpload> CreatePresignedUploadAsync(
        string key,
        string contentType,
        TimeSpan expiry,
        CancellationToken cancellationToken)
    {
        var expiresAt = DateTimeOffset.UtcNow.Add(expiry);

        var url = _client.GetPreSignedURL(new GetPreSignedUrlRequest
        {
            BucketName = _r2.BucketName,
            Key = key,
            Verb = HttpVerb.PUT,
            Expires = expiresAt.UtcDateTime,
            ContentType = contentType
        });

        return Task.FromResult(new PresignedUpload(
            url,
            "PUT",
            new Dictionary<string, string> { ["Content-Type"] = contentType },
            key,
            GetPublicUrl(key),
            expiresAt));
    }

    public async Task<bool> ExistsAsync(string key, CancellationToken cancellationToken)
    {
        try
        {
            await _client.GetObjectMetadataAsync(_r2.BucketName, key, cancellationToken);
            return true;
        }
        catch (AmazonS3Exception exception) when (exception.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return false;
        }
    }

    public Task DeleteAsync(string key, CancellationToken cancellationToken)
        => _client.DeleteObjectAsync(_r2.BucketName, key, cancellationToken);

    public string GetPublicUrl(string key)
        => $"{_r2.PublicBaseUrl.TrimEnd('/')}/{key}";

    public void Dispose() => _client.Dispose();
}
