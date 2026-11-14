using System.Net;
using System.Net.Http.Json;
using Newlife.IntegrationTests.Infrastructure;
using Shouldly;

namespace Newlife.IntegrationTests;

public sealed class AuthTests(NoRateLimitApiFactory factory) : IClassFixture<NoRateLimitApiFactory>
{
    [Fact]
    public async Task Login_with_seeded_admin_returns_tokens_and_user()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            email = AuthTestHelpers.AdminEmail,
            password = AuthTestHelpers.AdminPassword
        });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var payload = await response.Content.ReadFromJsonAsync<LoginPayload>();
        payload.ShouldNotBeNull();
        payload.AccessToken.ShouldNotBeNullOrWhiteSpace();
        payload.RefreshToken.ShouldNotBeNullOrWhiteSpace();
        payload.User.Email.ShouldBe(AuthTestHelpers.AdminEmail);
        payload.User.Roles.ShouldContain("Admin");
    }

    [Fact]
    public async Task Login_with_wrong_password_returns_401()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            email = AuthTestHelpers.AdminEmail,
            password = "WrongPass123"
        });

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await AuthTestHelpers.ReadProblemCodeAsync(response)).ShouldBe("auth.invalid_credentials");
    }

    [Fact]
    public async Task Me_returns_the_authenticated_user()
    {
        var client = factory.CreateClient();
        var tokens = await AuthTestHelpers.LoginAsync(client, AuthTestHelpers.AdminEmail, AuthTestHelpers.AdminPassword);

        var response = await client.WithBearer(tokens.AccessToken).GetAsync("/api/v1/auth/me");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var user = await response.Content.ReadFromJsonAsync<UserPayload>();
        user.ShouldNotBeNull();
        user.Id.ShouldBe(tokens.UserId);
    }

    [Fact]
    public async Task Me_without_token_returns_401()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/auth/me");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Refresh_rotates_the_refresh_token()
    {
        var client = factory.CreateClient();
        var tokens = await AuthTestHelpers.LoginAsync(client, AuthTestHelpers.AdminEmail, AuthTestHelpers.AdminPassword);

        var response = await client.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken = tokens.RefreshToken });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<LoginPayload>();
        payload.ShouldNotBeNull();
        payload.RefreshToken.ShouldNotBe(tokens.RefreshToken);
        payload.AccessToken.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Refresh_reuse_within_grace_period_issues_new_tokens()
    {
        var client = factory.CreateClient();
        var tokens = await AuthTestHelpers.LoginAsync(client, AuthTestHelpers.AdminEmail, AuthTestHelpers.AdminPassword);

        var first = await client.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken = tokens.RefreshToken });
        first.StatusCode.ShouldBe(HttpStatusCode.OK);

        var retried = await client.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken = tokens.RefreshToken });

        retried.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Non_admin_user_gets_403_on_admin_endpoints()
    {
        var client = factory.CreateClient();
        var adminTokens = await AuthTestHelpers.LoginAsync(client, AuthTestHelpers.AdminEmail, AuthTestHelpers.AdminPassword);

        var createResponse = await client.WithBearer(adminTokens.AccessToken).PostAsJsonAsync(
            "/api/v1/admin/users",
            new
            {
                email = "editor@newlife.test",
                fullName = "Content Editor",
                password = "EditorPass123",
                roles = new[] { "ContentEditor" }
            });
        createResponse.StatusCode.ShouldBe(HttpStatusCode.Created);

        var editorTokens = await AuthTestHelpers.LoginAsync(client, "editor@newlife.test", "EditorPass123");

        var listResponse = await client.WithBearer(editorTokens.AccessToken).GetAsync("/api/v1/admin/users");

        listResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    private sealed record LoginPayload(string AccessToken, string RefreshToken, UserPayload User);

    private sealed record UserPayload(Guid Id, string Email, string FullName, string[] Roles);
}
