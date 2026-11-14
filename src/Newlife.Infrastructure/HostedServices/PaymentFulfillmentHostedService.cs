using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newlife.Application.Orders;

namespace Newlife.Infrastructure.HostedServices;

public sealed class PaymentFulfillmentHostedService(
    FulfillmentQueue queue,
    IServiceScopeFactory scopeFactory,
    ILogger<PaymentFulfillmentHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var orderId in queue.Reader.WithCancellation(stoppingToken))
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var fulfillment = scope.ServiceProvider.GetRequiredService<OrderFulfillmentService>();
                var outcome = await fulfillment.TryFulfilAsync(orderId, stoppingToken);

                logger.LogInformation("Fulfilment for order {OrderId} finished: {Outcome}", orderId, outcome);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Fulfilment processing failed for order {OrderId}", orderId);
            }
        }
    }
}
