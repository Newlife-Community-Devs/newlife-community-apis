using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Newlife.Application.Common.Interfaces;
using Newlife.Domain.Entities;
using Newlife.Infrastructure.Identity;
using Newlife.Infrastructure.Persistence;
using Newlife.Infrastructure.Sermons;
using Newlife.Infrastructure.Storage;

namespace Newlife.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException("ConnectionStrings:Default is not configured.");

        services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));
        services.AddScoped<IAppDbContext>(provider => provider.GetRequiredService<AppDbContext>());

        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.SigningKey) && options.SigningKey.Length >= 64,
                "JWT:SigningKey must be at least 64 characters.")
            .ValidateOnStart();

        services.AddIdentityCore<AdminUser>(options =>
            {
                options.Password.RequiredLength = 10;
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = false;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
                options.User.RequireUniqueEmail = true;
            })
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<AppDbContext>()
            .AddDefaultTokenProviders();

        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, CurrentUser>();
        services.AddScoped<IJwtTokenService, JwtTokenService>();

        var storageOptions = configuration.BindStorageOptions();
        services.Configure<StorageOptions>(configuration.GetSection(StorageOptions.SectionName));

        if (string.Equals(storageOptions.Provider, "R2", StringComparison.OrdinalIgnoreCase))
        {
            services.AddSingleton<IStorageProvider, R2StorageProvider>();
        }
        else
        {
            services.AddSingleton<IStorageProvider, LocalStorageProvider>();
        }

        services.AddHttpClient<IPodcastFeedClient, PodcastFeedClient>(client =>
            client.Timeout = TimeSpan.FromSeconds(30));

        if (configuration.GetValue("Sermons:SyncEnabled", true))
        {
            services.AddHostedService<RssSyncHostedService>();
        }

        return services;
    }
}
