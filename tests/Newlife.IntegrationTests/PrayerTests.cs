using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Newlife.IntegrationTests.Infrastructure;
using Shouldly;

namespace Newlife.IntegrationTests;

public sealed class PrayerTests(NoRateLimitApiFactory factory) : IClassFixture<NoRateLimitApiFactory>
{
    [Fact]
    public async Task Submit_validates_content_phone_and_submitter_name()
    {
        var client = factory.CreateClient();

        var shortContent = await client.PostAsJsonAsync("/api/v1/prayers", new
        {
            title = "Short",
            content = "hi",
            submitterName = "Ama",
            isAnonymous = false
        });
        shortContent.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await AuthTestHelpers.ReadProblemCodeAsync(shortContent)).ShouldBe("validation.failed");

        var badPhone = await client.PostAsJsonAsync("/api/v1/prayers", new
        {
            title = "Phone",
            content = "Please pray for my family this week.",
            submitterName = "Ama",
            phoneNumber = "12345",
            isAnonymous = false
        });
        badPhone.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var missingName = await client.PostAsJsonAsync("/api/v1/prayers", new
        {
            title = "Name",
            content = "Please pray for my family this week.",
            isAnonymous = false
        });
        missingName.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Anonymous_submission_hides_the_submitter_name()
    {
        var client = factory.CreateClient();
        var content = $"Anonymous prayer {Guid.NewGuid():N}";

        var response = await client.PostAsJsonAsync("/api/v1/prayers", new
        {
            content,
            submitterName = "Hidden Person",
            isAnonymous = true
        });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        using var created = await ReadJsonAsync(response);
        created.RootElement.GetProperty("submitterName").ValueKind.ShouldBe(JsonValueKind.Null);
        created.RootElement.GetProperty("isAnonymous").GetBoolean().ShouldBeTrue();

        using var wall = await ReadJsonAsync(await client.GetAsync("/api/v1/prayers/wall?pageSize=100"));
        var item = FindByContent(wall, content);
        item.ShouldNotBeNull();
        item.Value.GetProperty("submitterName").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Fact]
    public async Task Admin_can_answer_flag_unflag_and_delete()
    {
        var client = factory.CreateClient();
        var admin = await AuthTestHelpers.LoginAsync(client, AuthTestHelpers.AdminEmail, AuthTestHelpers.AdminPassword);
        client.WithBearer(admin.AccessToken);
        var content = $"Lifecycle prayer {Guid.NewGuid():N}";

        var created = await client.PostAsJsonAsync("/api/v1/prayers", new
        {
            title = "Lifecycle",
            content,
            submitterName = "Kwame",
            phoneNumber = "233244000000",
            isAnonymous = false
        });
        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        using var createdJson = await ReadJsonAsync(created);
        var id = createdJson.RootElement.GetProperty("id").GetGuid();

        using var listed = await ReadJsonAsync(
            await client.GetAsync($"/api/v1/admin/prayers?pageSize=100&search={Uri.EscapeDataString(content)}"));
        var adminItem = FindByContent(listed, content);
        adminItem.ShouldNotBeNull();
        adminItem.Value.GetProperty("phoneNumber").GetString().ShouldBe("233244000000");

        var answered = await client.PostAsJsonAsync($"/api/v1/admin/prayers/{id}/answer", new { note = "Prayed" });
        answered.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var answeredJson = await ReadJsonAsync(answered);
        answeredJson.RootElement.GetProperty("isAnswered").GetBoolean().ShouldBeTrue();
        answeredJson.RootElement.GetProperty("answeredAt").ValueKind.ShouldBe(JsonValueKind.String);

        var flagged = await client.PostAsJsonAsync($"/api/v1/admin/prayers/{id}/flag", new { reason = "Needs review" });
        flagged.StatusCode.ShouldBe(HttpStatusCode.OK);

        using (var hiddenWall = await ReadJsonAsync(await client.GetAsync("/api/v1/prayers/wall?pageSize=100")))
        {
            FindById(hiddenWall, id).ShouldBeNull();
        }

        var unflagged = await client.PostAsJsonAsync($"/api/v1/admin/prayers/{id}/unflag", new { });
        unflagged.StatusCode.ShouldBe(HttpStatusCode.OK);

        using (var visibleWall = await ReadJsonAsync(await client.GetAsync("/api/v1/prayers/wall?pageSize=100")))
        {
            FindById(visibleWall, id).ShouldNotBeNull();
        }

        var deleted = await client.DeleteAsync($"/api/v1/admin/prayers/{id}");
        deleted.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        using (var finalWall = await ReadJsonAsync(await client.GetAsync("/api/v1/prayers/wall?pageSize=100")))
        {
            FindById(finalWall, id).ShouldBeNull();
        }

        var missing = await client.DeleteAsync($"/api/v1/admin/prayers/{Guid.NewGuid()}");
        missing.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await AuthTestHelpers.ReadProblemCodeAsync(missing)).ShouldBe("prayer.not_found");
    }

    [Fact]
    public async Task Admin_prayer_endpoints_require_authentication_and_role()
    {
        var anonymousClient = factory.CreateClient();

        var unauthorized = await anonymousClient.GetAsync("/api/v1/admin/prayers");
        unauthorized.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        var adminClient = factory.CreateClient();
        var admin = await AuthTestHelpers.LoginAsync(adminClient, AuthTestHelpers.AdminEmail, AuthTestHelpers.AdminPassword);
        adminClient.WithBearer(admin.AccessToken);

        var createEditor = await adminClient.PostAsJsonAsync("/api/v1/admin/users", new
        {
            email = $"prayer-manager-{Guid.NewGuid():N}@newlife.test",
            fullName = "Prayer Manager",
            password = "EditorPass123",
            roles = new[] { "EventManager" }
        });
        createEditor.StatusCode.ShouldBe(HttpStatusCode.Created);
        using var editorJson = await ReadJsonAsync(createEditor);
        var editorEmail = editorJson.RootElement.GetProperty("email").GetString();

        var editorClient = factory.CreateClient();
        var editor = await AuthTestHelpers.LoginAsync(editorClient, editorEmail!, "EditorPass123");

        var forbidden = await editorClient.WithBearer(editor.AccessToken).GetAsync("/api/v1/admin/prayers");
        forbidden.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Stats_track_new_submissions()
    {
        var client = factory.CreateClient();
        var admin = await AuthTestHelpers.LoginAsync(client, AuthTestHelpers.AdminEmail, AuthTestHelpers.AdminPassword);
        client.WithBearer(admin.AccessToken);

        using var before = await ReadJsonAsync(await client.GetAsync("/api/v1/admin/prayers/stats"));

        var submit = await client.PostAsJsonAsync("/api/v1/prayers", new
        {
            content = $"Stats prayer {Guid.NewGuid():N}",
            isAnonymous = true
        });
        submit.StatusCode.ShouldBe(HttpStatusCode.Created);

        using var after = await ReadJsonAsync(await client.GetAsync("/api/v1/admin/prayers/stats"));

        after.RootElement.GetProperty("total").GetInt32()
            .ShouldBe(before.RootElement.GetProperty("total").GetInt32() + 1);
        after.RootElement.GetProperty("open").GetInt32()
            .ShouldBe(before.RootElement.GetProperty("open").GetInt32() + 1);
        after.RootElement.GetProperty("anonymous").GetInt32()
            .ShouldBe(before.RootElement.GetProperty("anonymous").GetInt32() + 1);
        after.RootElement.GetProperty("submissionsLast7Days").GetInt32()
            .ShouldBe(before.RootElement.GetProperty("submissionsLast7Days").GetInt32() + 1);
        after.RootElement.GetProperty("uniqueSubmitters").GetInt32()
            .ShouldBe(before.RootElement.GetProperty("uniqueSubmitters").GetInt32());
    }

    [Fact]
    public async Task Wall_paginates_and_validates_sort_parameters()
    {
        var client = factory.CreateClient();

        using var firstPage = await ReadJsonAsync(await client.GetAsync("/api/v1/prayers/wall?page=1&pageSize=1"));
        firstPage.RootElement.GetProperty("items").GetArrayLength().ShouldBe(1);
        firstPage.RootElement.GetProperty("page").GetInt32().ShouldBe(1);
        firstPage.RootElement.GetProperty("pageSize").GetInt32().ShouldBe(1);
        firstPage.RootElement.GetProperty("hasMore").GetBoolean().ShouldBeTrue();
        var firstId = firstPage.RootElement.GetProperty("items")[0].GetProperty("id").GetGuid();

        using var secondPage = await ReadJsonAsync(await client.GetAsync("/api/v1/prayers/wall?page=2&pageSize=1"));
        secondPage.RootElement.GetProperty("items")[0].GetProperty("id").GetGuid().ShouldNotBe(firstId);

        var invalidSort = await client.GetAsync("/api/v1/prayers/wall?sort=name");
        invalidSort.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await AuthTestHelpers.ReadProblemCodeAsync(invalidSort)).ShouldBe("validation.failed");
    }

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response)
        => JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    private static JsonElement? FindByContent(JsonDocument document, string content)
    {
        foreach (var item in document.RootElement.GetProperty("items").EnumerateArray())
        {
            if (item.GetProperty("content").GetString() == content)
            {
                return item;
            }
        }

        return null;
    }

    private static JsonElement? FindById(JsonDocument document, Guid id)
    {
        foreach (var item in document.RootElement.GetProperty("items").EnumerateArray())
        {
            if (item.GetProperty("id").GetGuid() == id)
            {
                return item;
            }
        }

        return null;
    }
}
