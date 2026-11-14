using System.Net;
using Newlife.IntegrationTests.Infrastructure;
using Shouldly;

namespace Newlife.IntegrationTests;

public sealed class PingTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Ping_endpoint_responds_and_is_rate_limited()
    {
        var client = factory.CreateClient();

        for (var i = 0; i < 3; i++)
        {
            var response = await client.GetAsync("/api/v1/ping");
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        var limited = await client.GetAsync("/api/v1/ping");
        limited.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
    }
}
