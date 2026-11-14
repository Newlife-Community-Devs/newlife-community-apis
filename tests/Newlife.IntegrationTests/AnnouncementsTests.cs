using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Newlife.IntegrationTests.Infrastructure;
using Shouldly;

namespace Newlife.IntegrationTests;

public sealed class AnnouncementsTests(NoRateLimitApiFactory factory) : IClassFixture<NoRateLimitApiFactory>
{
    [Fact]
    public async Task Visibility_windows_and_admin_crud()
    {
        var client = await AdminClientAsync();
        var title = $"Future announcement {Guid.NewGuid():N}";

        var future = await client.PostAsJsonAsync("/api/v1/admin/announcements", new
        {
            title,
            body = "This will be visible later.",
            isPinned = false,
            isPublished = true,
            publishAt = DateTime.UtcNow.AddDays(1),
            expiresAt = (DateTime?)null
        });
        future.StatusCode.ShouldBe(HttpStatusCode.Created);
        var announcement = await future.Content.ReadFromJsonAsync<AnnouncementResponse>();
        announcement.ShouldNotBeNull();

        var publicAnonymous = factory.CreateClient();
        using var publicList = await GetJson(publicAnonymous, "/api/v1/announcements?pageSize=100");
        ContainsId(publicList, announcement.Id).ShouldBeFalse();

        var hiddenGet = await publicAnonymous.GetAsync($"/api/v1/announcements/{announcement.Id}");
        hiddenGet.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        using var adminList = await GetJson(client, "/api/v1/admin/announcements?pageSize=100");
        ContainsId(adminList, announcement.Id).ShouldBeTrue();

        var update = await client.PutAsJsonAsync($"/api/v1/admin/announcements/{announcement.Id}", new
        {
            title,
            body = "Now visible.",
            isPinned = false,
            isPublished = true,
            publishAt = DateTime.UtcNow.AddDays(-1),
            expiresAt = (DateTime?)null
        });
        update.StatusCode.ShouldBe(HttpStatusCode.OK);

        using var visibleList = await GetJson(publicAnonymous, "/api/v1/announcements?pageSize=100");
        ContainsId(visibleList, announcement.Id).ShouldBeTrue();

        var expire = await client.PutAsJsonAsync($"/api/v1/admin/announcements/{announcement.Id}", new
        {
            title,
            body = "Expired.",
            isPinned = false,
            isPublished = true,
            publishAt = DateTime.UtcNow.AddDays(-2),
            expiresAt = DateTime.UtcNow.AddMinutes(-1)
        });
        expire.StatusCode.ShouldBe(HttpStatusCode.OK);

        var expiredGet = await publicAnonymous.GetAsync($"/api/v1/announcements/{announcement.Id}");
        expiredGet.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        var delete = await client.DeleteAsync($"/api/v1/admin/announcements/{announcement.Id}");
        delete.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        using var afterDelete = await GetJson(client, "/api/v1/admin/announcements?pageSize=100");
        ContainsId(afterDelete, announcement.Id).ShouldBeFalse();
    }

    [Fact]
    public async Task Pinned_announcements_come_first()
    {
        var client = await AdminClientAsync();
        var publicAnonymous = factory.CreateClient();

        var unpinned = await client.PostAsJsonAsync("/api/v1/admin/announcements", new
        {
            title = $"Unpinned {Guid.NewGuid():N}",
            body = "Body",
            isPinned = false,
            isPublished = true,
            publishAt = DateTime.UtcNow.AddDays(-5),
            expiresAt = (DateTime?)null
        });
        unpinned.EnsureSuccessStatusCode();

        var pinned = await client.PostAsJsonAsync("/api/v1/admin/announcements", new
        {
            title = $"Pinned {Guid.NewGuid():N}",
            body = "Body",
            isPinned = true,
            isPublished = true,
            publishAt = DateTime.UtcNow.AddDays(-6),
            expiresAt = (DateTime?)null
        });
        pinned.EnsureSuccessStatusCode();

        using var list = await GetJson(publicAnonymous, "/api/v1/announcements?pageSize=100");
        var items = list.RootElement.GetProperty("items").EnumerateArray().ToArray();
        items[0].GetProperty("isPinned").GetBoolean().ShouldBeTrue();
        items.Count(item => item.GetProperty("isPinned").GetBoolean())
            .ShouldBe(items.TakeWhile(item => item.GetProperty("isPinned").GetBoolean()).Count());
    }

    [Fact]
    public async Task Validation_rules()
    {
        var client = await AdminClientAsync();

        var missingBody = await client.PostAsJsonAsync("/api/v1/admin/announcements", new
        {
            title = "No body",
            body = "",
            isPinned = false,
            isPublished = true,
            publishAt = (DateTime?)null,
            expiresAt = (DateTime?)null
        });
        missingBody.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await AuthTestHelpers.ReadProblemCodeAsync(missingBody)).ShouldBe("validation.failed");

        var badWindow = await client.PostAsJsonAsync("/api/v1/admin/announcements", new
        {
            title = "Bad window",
            body = "Body text here",
            isPinned = false,
            isPublished = true,
            publishAt = DateTime.UtcNow.AddDays(2),
            expiresAt = DateTime.UtcNow.AddDays(1)
        });
        badWindow.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await AuthTestHelpers.ReadProblemCodeAsync(badWindow)).ShouldBe("validation.failed");
    }

    [Fact]
    public async Task Anonymous_admin_access_is_unauthorized()
    {
        var client = factory.CreateClient();

        var list = await client.GetAsync("/api/v1/admin/announcements");
        list.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    private async Task<JsonDocument> GetJson(HttpClient client, string path)
        => JsonDocument.Parse(await client.GetStringAsync(path));

    private static bool ContainsId(JsonDocument document, Guid id)
        => document.RootElement.GetProperty("items")
            .EnumerateArray()
            .Any(item => item.GetProperty("id").GetGuid() == id);

    private async Task<HttpClient> AdminClientAsync()
    {
        var client = factory.CreateClient();
        var tokens = await AuthTestHelpers.LoginAsync(
            client, AuthTestHelpers.AdminEmail, AuthTestHelpers.AdminPassword);
        return client.WithBearer(tokens.AccessToken);
    }

    private sealed record AnnouncementResponse(
        Guid Id,
        string Title,
        string Body,
        bool IsPinned,
        DateTime? PublishAt,
        DateTime? ExpiresAt,
        DateTime CreatedAt);
}
