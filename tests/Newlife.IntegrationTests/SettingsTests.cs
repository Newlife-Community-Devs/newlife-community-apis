using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Newlife.IntegrationTests.Infrastructure;
using Shouldly;

namespace Newlife.IntegrationTests;

public sealed class SettingsTests(NoRateLimitApiFactory factory) : IClassFixture<NoRateLimitApiFactory>
{
    [Fact]
    public async Task Public_endpoint_only_returns_whitelisted_keys()
    {
        var client = factory.CreateClient();

        using var document = JsonDocument.Parse(await client.GetStringAsync("/api/v1/settings/public"));
        var properties = document.RootElement.EnumerateObject().ToArray();

        properties.Length.ShouldBe(2);
        properties.ShouldContain(property => property.Name == "sermons.latestYoutubeUrl");
        properties.ShouldContain(property => property.Name == "home.heroAnnouncement");
    }

    [Fact]
    public async Task Admin_can_read_groups_and_update_known_keys()
    {
        var client = await AdminClientAsync();

        using (var all = JsonDocument.Parse(await client.GetStringAsync("/api/v1/admin/settings")))
        {
            all.RootElement.TryGetProperty("sermons", out var sermons).ShouldBeTrue();
            sermons.TryGetProperty("sermons.latestYoutubeUrl", out _).ShouldBeTrue();
            all.RootElement.TryGetProperty("home", out var home).ShouldBeTrue();
            home.TryGetProperty("home.heroAnnouncement", out _).ShouldBeTrue();
        }

        var value = $"https://www.youtube.com/live/{Guid.NewGuid():N}";
        var update = await client.PutAsJsonAsync("/api/v1/admin/settings/sermons.latestYoutubeUrl", new { value });
        update.StatusCode.ShouldBe(HttpStatusCode.OK);
        using (var updated = JsonDocument.Parse(await update.Content.ReadAsStringAsync()))
        {
            updated.RootElement.GetProperty("key").GetString().ShouldBe("sermons.latestYoutubeUrl");
            updated.RootElement.GetProperty("value").GetString().ShouldBe(value);
            updated.RootElement.GetProperty("group").GetString().ShouldBe("sermons");
        }

        using var publicSettings = JsonDocument.Parse(await factory.CreateClient().GetStringAsync("/api/v1/settings/public"));
        publicSettings.RootElement.GetProperty("sermons.latestYoutubeUrl").GetString().ShouldBe(value);
    }

    [Fact]
    public async Task Unknown_setting_keys_are_rejected()
    {
        var client = await AdminClientAsync();

        var update = await client.PutAsJsonAsync("/api/v1/admin/settings/evil.unknownKey", new { value = "x" });

        update.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await AuthTestHelpers.ReadProblemCodeAsync(update)).ShouldBe("setting.unknown_key");
    }

    [Fact]
    public async Task Anonymous_admin_settings_is_unauthorized()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/admin/settings");
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    private async Task<HttpClient> AdminClientAsync()
    {
        var client = factory.CreateClient();
        var tokens = await AuthTestHelpers.LoginAsync(
            client, AuthTestHelpers.AdminEmail, AuthTestHelpers.AdminPassword);
        return client.WithBearer(tokens.AccessToken);
    }
}
