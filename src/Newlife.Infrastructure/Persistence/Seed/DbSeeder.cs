using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newlife.Domain.Constants;
using Newlife.Domain.Entities;
using Newlife.Infrastructure.Persistence;

namespace Newlife.Infrastructure.Persistence.Seed;

public static class DbSeeder
{
    public static async Task SeedAsync(IServiceProvider services, IConfiguration configuration)
    {
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        var userManager = services.GetRequiredService<UserManager<AdminUser>>();
        var db = services.GetRequiredService<AppDbContext>();
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
        await SeedSettingsAsync(db, configuration);

        if (configuration.GetValue<bool>("Seed:DemoData"))
        {
            await SeedDemoPrayersAsync(db, logger);
            await SeedDemoCmsAsync(db, logger);
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

    private static async Task SeedSettingsAsync(AppDbContext db, IConfiguration configuration)
    {
        await InsertSettingIfMissingAsync(
            db, "sermons.latestYoutubeUrl", configuration["SEED_YOUTUBE_URL"] ?? string.Empty, "sermons");
        await InsertSettingIfMissingAsync(db, "home.heroAnnouncement", string.Empty, "home");
        await db.SaveChangesAsync();
    }

    private static async Task InsertSettingIfMissingAsync(
        AppDbContext db,
        string key,
        string value,
        string group)
    {
        if (!await db.Set<SiteSetting>().AnyAsync(setting => setting.Key == key))
        {
            db.Set<SiteSetting>().Add(new SiteSetting { Key = key, Value = value, Group = group });
        }
    }

    private static async Task SeedDemoPrayersAsync(AppDbContext db, ILogger logger)
    {
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

    private static async Task SeedDemoCmsAsync(AppDbContext db, ILogger logger)
    {
        var now = DateTime.UtcNow;

        if (!await db.Set<Event>().AnyAsync())
        {
            var camp = new Event
            {
                Slug = "youth-camp-2026",
                Title = "Youth Camp 2026",
                Description = "Three days of worship, teaching and fellowship.",
                Venue = "Kpone-Katamanso",
                StartAt = now.AddDays(14),
                EndAt = now.AddDays(16),
                IsPublished = true,
                IsFeatured = true,
                TicketingEnabled = true
            };

            var vigil = new Event
            {
                Slug = "all-night-vigil",
                Title = "All-Night Vigil",
                Description = "A night of prayer and praise.",
                Venue = "Church Auditorium",
                StartAt = now.AddDays(30),
                EndAt = now.AddDays(31),
                IsPublished = false,
                IsFeatured = false,
                TicketingEnabled = false
            };

            db.AddRange(
                camp,
                vigil,
                new TicketType
                {
                    EventId = camp.Id,
                    Name = "Regular",
                    Price = 50m,
                    Quantity = 100,
                    MaxPerOrder = 10,
                    IsActive = true
                },
                new TicketType
                {
                    EventId = camp.Id,
                    Name = "VIP",
                    Price = 100m,
                    Quantity = 20,
                    MaxPerOrder = 4,
                    IsActive = true
                });

            await db.SaveChangesAsync();
            logger.LogInformation("Seeded demo events.");
        }

        if (!await db.Set<Announcement>().AnyAsync())
        {
            db.AddRange(
                new Announcement
                {
                    Title = "Sunday service times updated",
                    Body = "All services now start at 7:30am.",
                    IsPinned = true,
                    IsPublished = true,
                    PublishAt = now.AddDays(-1)
                },
                new Announcement
                {
                    Title = "Harvest thanksgiving",
                    Body = "Join us for harvest thanksgiving on the last Sunday of the month.",
                    IsPinned = false,
                    IsPublished = true,
                    PublishAt = now.AddDays(-7)
                });

            await db.SaveChangesAsync();
            logger.LogInformation("Seeded demo announcements.");
        }
    }
}
