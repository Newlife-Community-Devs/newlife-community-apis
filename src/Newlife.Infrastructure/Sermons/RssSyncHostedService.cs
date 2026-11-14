using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newlife.Application.Sermons;

namespace Newlife.Infrastructure.Sermons;

public sealed class RssSyncHostedService(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<RssSyncHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromMinutes(Math.Max(1, configuration.GetValue("Sermons:SyncIntervalMinutes", 60)));

        try
        {
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var sermonService = scope.ServiceProvider.GetRequiredService<SermonService>();
                var result = await sermonService.SyncAsync(stoppingToken);

                logger.LogInformation(
                    "Sermon RSS sync finished. Added {Added}, updated {Updated}, total {Total}.",
                    result.Added,
                    result.Updated,
                    result.Total);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Sermon RSS sync failed.");
            }

            try
            {
                await Task.Delay(interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
