using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Newlife.Application.Common.Interfaces;
using Testcontainers.PostgreSql;

namespace Newlife.IntegrationTests.Infrastructure;

public class ApiFactory : WebApplicationFactory<Program>
{
    private readonly PostgreSqlContainer _database = new PostgreSqlBuilder("postgres:17")
        .WithDatabase("newlife_tests")
        .WithUsername("newlife")
        .WithPassword("newlife_test")
        .Build();

    public ApiFactory()
    {
        _database.StartAsync().GetAwaiter().GetResult();
    }

    public FakeStorageProvider Storage { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("ConnectionStrings:Default", _database.GetConnectionString());
        builder.UseSetting("Database:RunMigrationsOnStartup", "true");
        builder.UseSetting("JWT:SigningKey", new string('x', 64));
        builder.UseSetting("Seed:AdminEmail", "admin@newlife.test");
        builder.UseSetting("Seed:AdminPassword", "AdminPass123");
        builder.UseSetting("Seed:AdminFullName", "Test Administrator");
        builder.UseSetting("Hubtel:ApiId", "");
        builder.UseSetting("Seed:DemoData", "true");
        builder.UseSetting("ReverseProxy:Enabled", "false");

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IStorageProvider>();
            services.AddSingleton<IStorageProvider>(Storage);
        });
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await _database.DisposeAsync();
    }
}
