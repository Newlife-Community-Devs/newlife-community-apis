using Microsoft.AspNetCore.Hosting;

namespace Newlife.IntegrationTests.Infrastructure;

public sealed class NoGraceApiFactory : NoRateLimitApiFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseSetting("JWT:RefreshRotationGraceSeconds", "0");
    }
}
