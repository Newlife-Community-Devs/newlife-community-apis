using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Newlife.IntegrationTests.Infrastructure;
using Shouldly;

namespace Newlife.IntegrationTests;

public sealed class MediaTests(NoRateLimitApiFactory factory) : IClassFixture<NoRateLimitApiFactory>
{
    private static readonly Regex GeneratedKeyPattern = new(@"^media/\d{4}/\d{2}/[0-9a-f]{32}\.jpg$");

    [Fact]
    public async Task Presign_upload_register_list_and_delete_cycle()
    {
        var client = await AdminClientAsync();

        var presign = await client.PostAsJsonAsync("/api/v1/admin/media/uploads", new
        {
            fileName = "youth-camp.jpg",
            contentType = "image/jpeg"
        });
        presign.StatusCode.ShouldBe(HttpStatusCode.OK);
        var upload = await presign.Content.ReadFromJsonAsync<PresignResponse>();
        upload.ShouldNotBeNull();
        GeneratedKeyPattern.IsMatch(upload.Key).ShouldBeTrue();
        upload.Url.ShouldContain(upload.Key);
        upload.Method.ShouldBe("PUT");
        upload.Headers["Content-Type"].ShouldBe("image/jpeg");

        factory.Storage.Upload(upload.Key);

        var register = await client.PostAsJsonAsync("/api/v1/admin/media", new
        {
            key = upload.Key,
            fileName = "youth-camp.jpg",
            contentType = "image/jpeg",
            sizeBytes = 204800,
            title = "Youth Camp",
            altText = "Youth camping trip"
        });
        register.StatusCode.ShouldBe(HttpStatusCode.Created);
        var media = await register.Content.ReadFromJsonAsync<MediaResponse>();
        media.ShouldNotBeNull();
        media.Key.ShouldBe(upload.Key);
        media.Url.ShouldBe($"https://media.fake.test/{upload.Key}");

        var list = await client.GetFromJsonAsync<PagedResponse<MediaResponse>>("/api/v1/admin/media?pageSize=100");
        list.ShouldNotBeNull();
        list.Items.ShouldContain(item => item.Key == upload.Key);

        var delete = await client.DeleteAsync($"/api/v1/admin/media/{media.Id}");
        delete.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        factory.Storage.Keys.ShouldNotContain(upload.Key);

        var afterDelete = await client.GetFromJsonAsync<PagedResponse<MediaResponse>>("/api/v1/admin/media?pageSize=100");
        afterDelete.ShouldNotBeNull();
        afterDelete.Items.ShouldNotContain(item => item.Key == upload.Key);
    }

    [Fact]
    public async Task Register_rejects_invalid_key_and_missing_object()
    {
        var client = await AdminClientAsync();

        var invalidKey = await client.PostAsJsonAsync("/api/v1/admin/media", new
        {
            key = "../evil.jpg",
            fileName = "evil.jpg",
            contentType = "image/jpeg"
        });
        invalidKey.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await AuthTestHelpers.ReadProblemCodeAsync(invalidKey)).ShouldBe("media.invalid_key");

        var missingObject = await client.PostAsJsonAsync("/api/v1/admin/media", new
        {
            key = $"media/2026/11/{Guid.NewGuid():N}.jpg",
            fileName = "never-uploaded.jpg",
            contentType = "image/jpeg"
        });
        missingObject.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await AuthTestHelpers.ReadProblemCodeAsync(missingObject)).ShouldBe("media.object_missing");
    }

    [Fact]
    public async Task Register_rejects_duplicate_key_and_bad_content_type()
    {
        var client = await AdminClientAsync();
        var key = await RegisterAsync(client);

        var duplicate = await client.PostAsJsonAsync("/api/v1/admin/media", new
        {
            key,
            fileName = "again.jpg",
            contentType = "image/jpeg"
        });
        duplicate.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await AuthTestHelpers.ReadProblemCodeAsync(duplicate)).ShouldBe("media.duplicate_key");

        var svg = await client.PostAsJsonAsync("/api/v1/admin/media/uploads", new
        {
            fileName = "icon.svg",
            contentType = "image/svg+xml"
        });
        svg.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await AuthTestHelpers.ReadProblemCodeAsync(svg)).ShouldBe("validation.failed");
    }

    [Fact]
    public async Task Deleting_media_used_as_event_banner_conflicts()
    {
        var client = await AdminClientAsync();
        var key = await RegisterAsync(client);

        var registered = await client.GetFromJsonAsync<PagedResponse<MediaResponse>>("/api/v1/admin/media?pageSize=100");
        registered.ShouldNotBeNull();
        var mediaId = registered.Items.Single(item => item.Key == key).Id;

        var startAt = DateTime.UtcNow.AddDays(10);
        var evt = await client.PostAsJsonAsync("/api/v1/admin/events", new
        {
            title = $"Banner event {Guid.NewGuid():N}",
            startAt,
            isPublished = true,
            bannerMediaId = mediaId
        });
        evt.StatusCode.ShouldBe(HttpStatusCode.Created);

        var delete = await client.DeleteAsync($"/api/v1/admin/media/{mediaId}");
        delete.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await AuthTestHelpers.ReadProblemCodeAsync(delete)).ShouldBe("media.in_use");
    }

    [Fact]
    public async Task Anonymous_media_requests_are_unauthorized()
    {
        var client = factory.CreateClient();

        var list = await client.GetAsync("/api/v1/admin/media");
        list.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    private async Task<string> RegisterAsync(HttpClient client)
    {
        var presign = await client.PostAsJsonAsync("/api/v1/admin/media/uploads", new
        {
            fileName = "sample.jpg",
            contentType = "image/jpeg"
        });
        presign.EnsureSuccessStatusCode();
        var upload = await presign.Content.ReadFromJsonAsync<PresignResponse>();
        upload.ShouldNotBeNull();
        factory.Storage.Upload(upload.Key);

        var register = await client.PostAsJsonAsync("/api/v1/admin/media", new
        {
            key = upload.Key,
            fileName = "sample.jpg",
            contentType = "image/jpeg"
        });
        register.EnsureSuccessStatusCode();
        return upload.Key;
    }

    private async Task<HttpClient> AdminClientAsync()
    {
        var client = factory.CreateClient();
        var tokens = await AuthTestHelpers.LoginAsync(
            client, AuthTestHelpers.AdminEmail, AuthTestHelpers.AdminPassword);
        return client.WithBearer(tokens.AccessToken);
    }

    private sealed record PresignResponse(
        string Url,
        string Method,
        Dictionary<string, string> Headers,
        string Key,
        string PublicUrl,
        DateTimeOffset ExpiresAt);

    private sealed record MediaResponse(
        Guid Id,
        string Url,
        string Key,
        string FileName,
        string ContentType,
        long? SizeBytes,
        string? Title,
        string? AltText,
        DateTime CreatedAt);

    private sealed record PagedResponse<T>(T[] Items, int Page, int PageSize, int TotalCount);
}
