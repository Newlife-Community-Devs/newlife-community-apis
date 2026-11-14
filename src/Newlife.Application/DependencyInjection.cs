using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Newlife.Application.Auth;
using Newlife.Application.Prayers;
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
        return services;
    }
}
