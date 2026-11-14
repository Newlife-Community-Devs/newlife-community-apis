using Microsoft.AspNetCore.Hosting;

namespace Newlife.IntegrationTests.Infrastructure;

public class NoRateLimitApiFactory : ApiFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseSetting("RateLimits:Enabled", "false");
    }
}
