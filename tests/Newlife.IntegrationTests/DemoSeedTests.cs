using System.Text.Json;
using Newlife.IntegrationTests.Infrastructure;
using Shouldly;

namespace Newlife.IntegrationTests;

public sealed class DemoSeedTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Seeded_prayers_match_the_stats_and_wall_contract()
    {
        var client = factory.CreateClient();
        var admin = await AuthTestHelpers.LoginAsync(client, AuthTestHelpers.AdminEmail, AuthTestHelpers.AdminPassword);
        client.WithBearer(admin.AccessToken);

        using var stats = JsonDocument.Parse(
            await client.GetStringAsync("/api/v1/admin/prayers/stats"));

        stats.RootElement.GetProperty("total").GetInt32().ShouldBe(3);
        stats.RootElement.GetProperty("open").GetInt32().ShouldBe(2);
        stats.RootElement.GetProperty("answered").GetInt32().ShouldBe(1);
        stats.RootElement.GetProperty("flagged").GetInt32().ShouldBe(1);
        stats.RootElement.GetProperty("anonymous").GetInt32().ShouldBe(1);
        stats.RootElement.GetProperty("submissionsLast7Days").GetInt32().ShouldBe(3);
        stats.RootElement.GetProperty("uniqueSubmitters").GetInt32().ShouldBe(2);

        using var wall = JsonDocument.Parse(
            await client.GetStringAsync("/api/v1/prayers/wall?pageSize=100"));

        wall.RootElement.GetProperty("totalCount").GetInt32().ShouldBe(2);

        foreach (var item in wall.RootElement.GetProperty("items").EnumerateArray())
        {
            item.GetProperty("content").GetString()!.ShouldNotContain("demo flagged");

            if (item.GetProperty("isAnonymous").GetBoolean())
            {
                item.GetProperty("submitterName").ValueKind.ShouldBe(JsonValueKind.Null);
            }
        }
    }
}
