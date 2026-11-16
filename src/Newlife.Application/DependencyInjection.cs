using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Newlife.Application.Announcements;
using Newlife.Application.Auth;
using Newlife.Application.Events;
using Newlife.Application.Media;
using Newlife.Application.Orders;
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
        services.AddSingleton<FulfillmentQueue>();
        services.AddScoped<OrderService>();
        services.AddScoped<OrderFulfillmentService>();
        services.AddScoped<HubtelCallbackService>();
        services.AddScoped<AdminOrderService>();
        services.AddScoped<CheckInService>();
        services.AddScoped<TicketQrService>();
        services.AddScoped<TicketAvailability>();
        return services;
    }
}
