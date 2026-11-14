using System.Net;
using System.Net.Http.Json;
using Newlife.Application.Common.Interfaces;
using Newlife.Infrastructure.Sermons;
using Newlife.IntegrationTests.Infrastructure;
using Shouldly;

namespace Newlife.IntegrationTests;

public sealed class SermonsTests(NoRateLimitApiFactory factory) : IClassFixture<NoRateLimitApiFactory>
{
    [Fact]
    public async Task Sync_ingests_updates_and_respects_visibility()
    {
        var client = await AdminClientAsync();
        var marker = Guid.NewGuid().ToString("N");
        var firstTitle = $"First Sermon {marker}";
        var renamedTitle = $"First Sermon Renamed {marker}";
        var secondTitle = $"Second Sermon {marker}";
        var thirdTitle = $"Third Sermon {marker}";

        var baseTotal = (await client.GetFromJsonAsync<EpisodePage>("/api/v1/admin/sermons?pageSize=1"))!.TotalCount;

        factory.Feed.Episodes.Clear();
        factory.Feed.Episodes.AddRange(
            Episode($"sync-one-{marker}", firstTitle, DateTime.UtcNow.AddDays(-7), "1:00:00"),
            Episode($"sync-two-{marker}", secondTitle, DateTime.UtcNow.AddDays(-14), "30:00"));

        var firstSync = await client.PostAsync("/api/v1/admin/sermons/sync", new StringContent("{}"));
        firstSync.StatusCode.ShouldBe(HttpStatusCode.OK);
        var firstResult = await firstSync.Content.ReadFromJsonAsync<SyncResult>();
        firstResult.ShouldNotBeNull();
        firstResult.Added.ShouldBe(2);
        firstResult.Updated.ShouldBe(0);
        firstResult.Total.ShouldBe(baseTotal + 2);

        var episodes = await client.GetFromJsonAsync<EpisodePage>("/api/v1/admin/sermons?pageSize=100");
        episodes.ShouldNotBeNull();
        var one = episodes.Items.Single(episode => episode.Title == firstTitle);
        one.DurationSeconds.ShouldBe(3600);
        one.IsVisible.ShouldBeTrue();

        factory.Feed.Episodes.Clear();
        factory.Feed.Episodes.AddRange(
            Episode($"sync-one-{marker}", renamedTitle, DateTime.UtcNow.AddDays(-7), "1:15:00"),
            Episode($"sync-three-{marker}", thirdTitle, DateTime.UtcNow.AddDays(-1), "2700"));

        var secondSync = await client.PostAsync("/api/v1/admin/sermons/sync", new StringContent("{}"));
        var secondResult = await secondSync.Content.ReadFromJsonAsync<SyncResult>();
        secondResult.ShouldNotBeNull();
        secondResult.Added.ShouldBe(1);
        secondResult.Updated.ShouldBe(1);
        secondResult.Total.ShouldBe(baseTotal + 3);

        var hide = await client.PutAsJsonAsync($"/api/v1/admin/sermons/{one.Id}", new { isVisible = false });
        hide.StatusCode.ShouldBe(HttpStatusCode.OK);

        var thirdSync = await client.PostAsync("/api/v1/admin/sermons/sync", new StringContent("{}"));
        var thirdResult = await thirdSync.Content.ReadFromJsonAsync<SyncResult>();
        thirdResult.ShouldNotBeNull();
        thirdResult.Added.ShouldBe(0);
        thirdResult.Updated.ShouldBe(2);
        thirdResult.Total.ShouldBe(baseTotal + 3);

        var afterResync = await client.GetFromJsonAsync<EpisodePage>("/api/v1/admin/sermons?pageSize=100");
        afterResync.ShouldNotBeNull();
        var renamed = afterResync.Items.Single(episode => episode.Title == renamedTitle);
        renamed.IsVisible.ShouldBeFalse();
        renamed.DurationSeconds.ShouldBe(4500);
        renamed.DescriptionHtml.ShouldNotBeNull();

        var publicAnonymous = factory.CreateClient();

        var hiddenGet = await publicAnonymous.GetAsync($"/api/v1/sermons/{one.Id}");
        hiddenGet.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        var publicList = await publicAnonymous.GetFromJsonAsync<PublicEpisodePage>("/api/v1/sermons?pageSize=100");
        publicList.ShouldNotBeNull();
        publicList.Items.ShouldNotContain(episode => episode.Title == renamedTitle);
    }

    [Fact]
    public async Task Latest_returns_newest_visible_episode_with_youtube_url()
    {
        var client = await AdminClientAsync();
        var guid = Guid.NewGuid().ToString("N");

        factory.Feed.Episodes.Clear();
        factory.Feed.Episodes.AddRange(
            Episode($"latest-old-{guid}", $"Older Sermon {guid}", DateTime.UtcNow.AddDays(-30), "1:00:00"),
            Episode($"latest-new-{guid}", $"Newest Sermon {guid}", DateTime.UtcNow.AddHours(-1), "55:00"));

        var sync = await client.PostAsync("/api/v1/admin/sermons/sync", new StringContent("{}"));
        sync.EnsureSuccessStatusCode();

        var youtubeUrl = $"https://www.youtube.com/live/{guid}";
        var setting = await client.PutAsJsonAsync("/api/v1/admin/settings/sermons.latestYoutubeUrl", new { value = youtubeUrl });
        setting.EnsureSuccessStatusCode();

        var publicAnonymous = factory.CreateClient();
        var latest = await publicAnonymous.GetFromJsonAsync<LatestResponse>("/api/v1/sermons/latest");
        latest.ShouldNotBeNull();
        latest.YoutubeUrl.ShouldBe(youtubeUrl);
        latest.Episode.ShouldNotBeNull();
        latest.Episode.Title.ShouldBe($"Newest Sermon {guid}");
    }

    [Fact]
    public async Task Anonymous_sync_is_unauthorized()
    {
        var client = factory.CreateClient();

        var sync = await client.PostAsync("/api/v1/admin/sermons/sync", new StringContent("{}"));

        sync.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    private static FeedEpisode Episode(
        string guid,
        string title,
        DateTime publishedAt,
        string duration)
        => new(guid, title, $"<p>Description of {title}.</p>", $"https://cdn.test/{guid}.mp3", publishedAt, null,
            SermonFeedParser.ParseDuration(duration));

    private async Task<HttpClient> AdminClientAsync()
    {
        var client = factory.CreateClient();
        var tokens = await AuthTestHelpers.LoginAsync(
            client, AuthTestHelpers.AdminEmail, AuthTestHelpers.AdminPassword);
        return client.WithBearer(tokens.AccessToken);
    }

    private sealed record SyncResult(int Added, int Updated, int Total);

    private sealed record EpisodePage(AdminEpisode[] Items, int Page, int PageSize, int TotalCount);

    private sealed record PublicEpisodePage(PublicEpisode[] Items, int Page, int PageSize, int TotalCount);

    private sealed record AdminEpisode(
        Guid Id,
        string Title,
        string? Summary,
        string? DescriptionHtml,
        string AudioUrl,
        int? DurationSeconds,
        string? ArtworkUrl,
        DateTime PublishedAt,
        bool IsVisible);

    private sealed record PublicEpisode(
        Guid Id,
        string Title,
        string? Summary,
        string AudioUrl,
        int? DurationSeconds,
        string? ArtworkUrl,
        DateTime PublishedAt);

    private sealed record LatestResponse(PublicEpisode? Episode, string YoutubeUrl);
}
