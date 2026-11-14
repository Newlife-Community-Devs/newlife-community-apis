using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newlife.Application.Common.Interfaces;
using Newlife.Application.Orders;
using Newlife.Domain.Entities;
using Newlife.Domain.Enums;
using Newlife.Infrastructure.Persistence;

namespace Newlife.Infrastructure.HostedServices;

public sealed class PaymentReconciliationHostedService(
    IServiceScopeFactory scopeFactory,
    IOptions<HubtelOptions> hubtelOptions,
    IOptions<PaymentsTuningOptions> tuning,
    ILogger<PaymentReconciliationHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (string.IsNullOrWhiteSpace(hubtelOptions.Value.ApiId))
        {
            logger.LogInformation("Hubtel API id not configured; reconciliation job is disabled.");
            return;
        }

        var interval = TimeSpan.FromSeconds(Math.Max(5, tuning.Value.ReconciliationIntervalSeconds));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Payment reconciliation run failed.");
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

    private async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var fulfillment = scope.ServiceProvider.GetRequiredService<OrderFulfillmentService>();

        var cutoff = DateTime.UtcNow - TimeSpan.FromSeconds(Math.Max(0, tuning.Value.ReconciliationMinAgeSeconds));

        var stale = await db.Set<Domain.Entities.Order>()
            .Where(order => order.Status == OrderStatus.AwaitingPayment && order.UpdatedAt < cutoff)
            .OrderBy(order => order.UpdatedAt)
            .Take(50)
            .Select(order => order.Id)
            .ToListAsync(cancellationToken);

        foreach (var orderId in stale)
        {
            await fulfillment.TryFulfilAsync(orderId, cancellationToken);
        }

        await fulfillment.ReverifyCallbackOnlyAsync(cancellationToken);
    }
}
