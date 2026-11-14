using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Newlife.Application.Announcements;
using Newlife.Application.Auth;
using Newlife.Application.Events;
using Newlife.Application.Media;
using Newlife.Application.Prayers;
using Newlife.Application.Sermons;
using Newlife.Application.Settings;
using Newlife.Application.Users;

namespace Newlife.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);
        services.AddScoped<AuthService>();
        services.AddScoped<AdminUserService>();
        services.AddScoped<PrayerService>();
        services.AddScoped<MediaService>();
        services.AddScoped<EventService>();
        services.AddScoped<AnnouncementService>();
        services.AddScoped<SettingsService>();
        services.AddScoped<SermonService>();
        return services;
    }
}
