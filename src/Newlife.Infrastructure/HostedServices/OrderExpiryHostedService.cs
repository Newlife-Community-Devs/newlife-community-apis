using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newlife.Application.Orders;
using Newlife.Domain.Enums;
using Newlife.Infrastructure.Persistence;

namespace Newlife.Infrastructure.HostedServices;

public sealed class OrderExpiryHostedService(
    IServiceScopeFactory scopeFactory,
    IOptions<PaymentsTuningOptions> tuning,
    ILogger<OrderExpiryHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromSeconds(Math.Max(10, tuning.Value.ExpiryIntervalSeconds));

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
                logger.LogWarning(exception, "Order expiry run failed.");
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
        var now = DateTime.UtcNow;

        await db.Set<Domain.Entities.Order>()
            .Where(order => order.Status == OrderStatus.PendingPayment &&
                            order.HoldExpiresAt != null && order.HoldExpiresAt < now)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(order => order.Status, OrderStatus.Cancelled)
                .SetProperty(order => order.HoldExpiresAt, (DateTime?)null)
                .SetProperty(order => order.UpdatedAt, now),
                cancellationToken);

        var awaiting = await db.Set<Domain.Entities.Order>()
            .Where(order => order.Status == OrderStatus.AwaitingPayment &&
                            order.HoldExpiresAt != null && order.HoldExpiresAt < now)
            .Select(order => order.Id)
            .ToListAsync(cancellationToken);

        foreach (var orderId in awaiting)
        {
            var outcome = await fulfillment.TryFulfilAsync(orderId, cancellationToken);

            if (outcome is FulfilOutcome.Unpaid or FulfilOutcome.GatewayUnreachable)
            {
                var rows = await db.Set<Domain.Entities.Order>()
                    .Where(order => order.Id == orderId && order.Status == OrderStatus.AwaitingPayment)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(order => order.Status, OrderStatus.Cancelled)
                        .SetProperty(order => order.HoldExpiresAt, (DateTime?)null)
                        .SetProperty(order => order.UpdatedAt, now),
                        cancellationToken);

                if (rows > 0)
                {
                    logger.LogInformation("Expired hold on order {OrderId} after status was {Outcome}", orderId, outcome);
                }
            }
        }
    }
}
