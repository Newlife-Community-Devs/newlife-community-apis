using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newlife.Domain.Constants;
using Newlife.Domain.Entities;

namespace Newlife.Infrastructure.Persistence.Seed;

public static class DbSeeder
{
    public static async Task SeedAsync(IServiceProvider services, IConfiguration configuration)
    {
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        var userManager = services.GetRequiredService<UserManager<AdminUser>>();
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("DbSeeder");

        foreach (var role in Roles.All)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                var result = await roleManager.CreateAsync(new IdentityRole<Guid>(role));
                if (!result.Succeeded)
                {
                    logger.LogError("Failed to seed role {Role}: {Errors}", role,
                        string.Join("; ", result.Errors.Select(error => error.Description)));
                }
            }
        }

        await SeedFirstAdminAsync(userManager, configuration, logger);

        if (configuration.GetValue<bool>("Seed:DemoData"))
        {
            await SeedDemoPrayersAsync(services, logger);
        }
    }

    private static async Task SeedFirstAdminAsync(
        UserManager<AdminUser> userManager,
        IConfiguration configuration,
        ILogger logger)
    {
        var adminEmail = configuration["Seed:AdminEmail"];
        var adminPassword = configuration["Seed:AdminPassword"];

        if (string.IsNullOrWhiteSpace(adminEmail) ||
            string.IsNullOrWhiteSpace(adminPassword) ||
            userManager.Users.Any())
        {
            return;
        }

        var admin = new AdminUser
        {
            UserName = adminEmail,
            Email = adminEmail,
            FullName = configuration["Seed:AdminFullName"] ?? "Administrator",
            EmailConfirmed = true
        };

        var createResult = await userManager.CreateAsync(admin, adminPassword);
        if (!createResult.Succeeded)
        {
            logger.LogError("Failed to seed admin {Email}: {Errors}", adminEmail,
                string.Join("; ", createResult.Errors.Select(error => error.Description)));
            return;
        }

        var roleResult = await userManager.AddToRoleAsync(admin, Roles.Admin);
        if (!roleResult.Succeeded)
        {
            logger.LogError("Failed to assign admin role to {Email}: {Errors}", adminEmail,
                string.Join("; ", roleResult.Errors.Select(error => error.Description)));
        }
    }

    private static async Task SeedDemoPrayersAsync(IServiceProvider services, ILogger logger)
    {
        var db = services.GetRequiredService<AppDbContext>();
        if (await db.Set<PrayerRequest>().AnyAsync())
        {
            return;
        }

        var now = DateTime.UtcNow;

        db.AddRange(
            new PrayerRequest
            {
                Title = "Healing for my mother",
                Content = "Please join us in praying for my mother's recovery after surgery.",
                SubmitterName = "Ama Mensah",
                IsAnswered = true,
                AnsweredAt = now.AddDays(-1)
            },
            new PrayerRequest
            {
                Title = "Wisdom for exams",
                Content = "Praying for peace and clarity during final exams this week.",
                IsAnonymous = true
            },
            new PrayerRequest
            {
                Content = "This is a demo flagged prayer request that stays hidden from the public wall.",
                SubmitterName = "Demo User",
                IsFlagged = true,
                FlagReason = "Demo flag"
            });

        await db.SaveChangesAsync();
        logger.LogInformation("Seeded demo prayer requests.");
    }
}
