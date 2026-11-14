using Microsoft.Extensions.Configuration;

namespace Newlife.Infrastructure.Storage;

public sealed class StorageOptions
{
    public const string SectionName = "Storage";

    public string Provider { get; set; } = "Local";

    public R2Options R2 { get; set; } = new();

    public sealed class R2Options
    {
        public string AccountId { get; set; } = string.Empty;

        public string AccessKeyId { get; set; } = string.Empty;

        public string SecretAccessKey { get; set; } = string.Empty;

        public string BucketName { get; set; } = string.Empty;

        public string PublicBaseUrl { get; set; } = string.Empty;

        public string ServiceUrl => $"https://{AccountId}.r2.cloudflarestorage.com";
    }
}

public static class StorageOptionsExtensions
{
    public static StorageOptions BindStorageOptions(this IConfiguration configuration)
        => configuration.GetSection(StorageOptions.SectionName).Get<StorageOptions>() ?? new StorageOptions();
}
