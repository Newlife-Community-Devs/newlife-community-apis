using System.Net;
using System.Net.Http.Json;
using Newlife.IntegrationTests.Infrastructure;
using Shouldly;

namespace Newlife.IntegrationTests;

public sealed class LockoutTests(NoRateLimitApiFactory factory) : IClassFixture<NoRateLimitApiFactory>
{
    [Fact]
    public async Task Five_failed_logins_lock_the_account()
    {
        var client = factory.CreateClient();

        for (var attempt = 0; attempt < 5; attempt++)
        {
            var failed = await client.PostAsJsonAsync("/api/v1/auth/login", new
            {
                email = AuthTestHelpers.AdminEmail,
                password = "WrongPass123"
            });

            failed.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        }

        var locked = await client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            email = AuthTestHelpers.AdminEmail,
            password = AuthTestHelpers.AdminPassword
        });

        locked.StatusCode.ShouldBe(HttpStatusCode.Locked);
        (await AuthTestHelpers.ReadProblemCodeAsync(locked)).ShouldBe("auth.locked_out");
    }
}
