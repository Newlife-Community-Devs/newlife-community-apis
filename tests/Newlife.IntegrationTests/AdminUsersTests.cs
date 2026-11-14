using System.Net;
using System.Net.Http.Json;
using Newlife.IntegrationTests.Infrastructure;
using Shouldly;

namespace Newlife.IntegrationTests;

public sealed class AdminUsersTests(NoRateLimitApiFactory factory) : IClassFixture<NoRateLimitApiFactory>
{
    [Fact]
    public async Task Create_list_and_password_reset_revokes_sessions()
    {
        var client = factory.CreateClient();
        var adminTokens = await AuthTestHelpers.LoginAsync(client, AuthTestHelpers.AdminEmail, AuthTestHelpers.AdminPassword);

        var createResponse = await client.WithBearer(adminTokens.AccessToken).PostAsJsonAsync(
            "/api/v1/admin/users",
            new
            {
                email = "leader@newlife.test",
                fullName = "Small Group Leader",
                password = "LeaderPass123",
                roles = new[] { "ContentEditor" }
            });
        createResponse.StatusCode.ShouldBe(HttpStatusCode.Created);

        var listResponse = await client.WithBearer(adminTokens.AccessToken)
            .GetAsync("/api/v1/admin/users?page=1&pageSize=20");
        listResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var list = await listResponse.Content.ReadFromJsonAsync<UserListPayload>();
        list.ShouldNotBeNull();
        list.Items.ShouldContain(user => user.Email == "leader@newlife.test");

        var leaderTokens = await AuthTestHelpers.LoginAsync(client, "leader@newlife.test", "LeaderPass123");

        var resetResponse = await client.WithBearer(adminTokens.AccessToken).PostAsJsonAsync(
            $"/api/v1/admin/users/{leaderTokens.UserId}/reset-password",
            new { newPassword = "NewLeaderPass123" });
        resetResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var staleRefresh = await client.PostAsJsonAsync(
            "/api/v1/auth/refresh",
            new { refreshToken = leaderTokens.RefreshToken });
        staleRefresh.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        var relogin = await client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            email = "leader@newlife.test",
            password = "NewLeaderPass123"
        });
        relogin.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Cannot_remove_the_last_admin_role()
    {
        var client = factory.CreateClient();
        var adminTokens = await AuthTestHelpers.LoginAsync(client, AuthTestHelpers.AdminEmail, AuthTestHelpers.AdminPassword);

        var response = await client.WithBearer(adminTokens.AccessToken).PutAsJsonAsync(
            $"/api/v1/admin/users/{adminTokens.UserId}/roles",
            new { roles = new[] { "ContentEditor" } });

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await AuthTestHelpers.ReadProblemCodeAsync(response)).ShouldBe("user.last_admin");
    }

    private sealed record UserListPayload(UserPayload[] Items, int Page, int PageSize, int TotalCount);

    private sealed record UserPayload(Guid Id, string Email, string FullName, string[] Roles);
}
