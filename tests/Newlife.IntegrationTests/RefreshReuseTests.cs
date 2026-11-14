using System.Net;
using System.Net.Http.Json;
using Newlife.IntegrationTests.Infrastructure;
using Shouldly;

namespace Newlife.IntegrationTests;

public sealed class RefreshReuseTests(NoGraceApiFactory factory) : IClassFixture<NoGraceApiFactory>
{
    [Fact]
    public async Task Reusing_a_rotated_refresh_token_revokes_the_chain()
    {
        var client = factory.CreateClient();
        var tokens = await AuthTestHelpers.LoginAsync(client, AuthTestHelpers.AdminEmail, AuthTestHelpers.AdminPassword);

        var rotated = await client.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken = tokens.RefreshToken });
        rotated.StatusCode.ShouldBe(HttpStatusCode.OK);
        var rotatedPayload = await rotated.Content.ReadFromJsonAsync<RefreshPayload>();
        rotatedPayload.ShouldNotBeNull();

        var reuse = await client.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken = tokens.RefreshToken });

        reuse.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await AuthTestHelpers.ReadProblemCodeAsync(reuse)).ShouldBe("auth.refresh_reuse");

        var afterRevocation = await client.PostAsJsonAsync(
            "/api/v1/auth/refresh",
            new { refreshToken = rotatedPayload.RefreshToken });

        afterRevocation.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    private sealed record RefreshPayload(string AccessToken, string RefreshToken);
}
