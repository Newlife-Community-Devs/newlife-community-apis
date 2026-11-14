using System.Net;
using System.Net.Http.Json;
using Newlife.IntegrationTests.Infrastructure;
using Shouldly;

namespace Newlife.IntegrationTests;

public sealed class PrayerRateLimitTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Sixth_rapid_submission_is_rate_limited()
    {
        var client = factory.CreateClient();

        for (var index = 0; index < 5; index++)
        {
            var response = await client.PostAsJsonAsync("/api/v1/prayers", new
            {
                content = $"Rate limit prayer number {index} {Guid.NewGuid():N}",
                isAnonymous = true
            });

            response.StatusCode.ShouldBe(HttpStatusCode.Created);
        }

        var limited = await client.PostAsJsonAsync("/api/v1/prayers", new
        {
            content = $"Rate limit prayer overflow {Guid.NewGuid():N}",
            isAnonymous = true
        });

        limited.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
    }
}
