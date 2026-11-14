using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Shouldly;

namespace Newlife.IntegrationTests.Infrastructure;

internal static class AuthTestHelpers
{
    public const string AdminEmail = "admin@newlife.test";
    public const string AdminPassword = "AdminPass123";

    public static async Task<AuthTokens> LoginAsync(HttpClient client, string email, string password)
    {
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password });
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<AuthTokensPayload>();
        payload.ShouldNotBeNull();

        return new AuthTokens(payload.AccessToken, payload.RefreshToken, payload.User.Id);
    }

    public static HttpClient WithBearer(this HttpClient client, string accessToken)
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return client;
    }

    public static async Task<string?> ReadProblemCodeAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        using var document = JsonDocument.Parse(body);
        return document.RootElement.TryGetProperty("code", out var code) ? code.GetString() : null;
    }

    internal sealed record AuthTokens(string AccessToken, string RefreshToken, Guid UserId);

    private sealed record AuthTokensPayload(string AccessToken, string RefreshToken, UserPayload User);

    private sealed record UserPayload(Guid Id, string Email, string FullName, string[] Roles);
}
