using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newlife.Application.Common.Interfaces;
using Newlife.Domain.Entities;
using Newlife.Domain.Enums;

namespace Newlife.Application.Orders;

public enum CallbackResult
{
    Queued,
    InvalidToken,
    InvalidPayload,
    IpRejected,
    UnknownReference
}

public sealed class HubtelCallbackService(
    IAppDbContext db,
    FulfillmentQueue fulfillmentQueue,
    TicketAvailability ticketAvailability,
    IOptions<HubtelOptions> options,
    ILogger<HubtelCallbackService> logger)
{
    public const string CallbackSourceIp = "108.129.40.25";

    public async Task<CallbackResult> ProcessCheckoutCallbackAsync(
        string rawJson,
        string? sourceIp,
        CancellationToken cancellationToken)
    {
        using var document = TryParse(rawJson);

        if (document is null)
        {
            await LogAsync(rawJson, sourceIp, null, "invalid_payload", cancellationToken);
            return CallbackResult.InvalidPayload;
        }

        var data = document.RootElement.TryGetProperty("Data", out var dataElement) ? dataElement : default;
        var clientReference = TryGetString(data, "ClientReference");

        if (options.Value.EnforceCallbackIp && !IsAllowedIp(sourceIp))
        {
            await LogAsync(rawJson, sourceIp, clientReference, "ip_rejected", cancellationToken);
            logger.LogWarning("Hubtel callback rejected from IP {SourceIp}", sourceIp);
            return CallbackResult.IpRejected;
        }

        var log = new HubtelCallbackLog
        {
            ReceivedAt = DateTime.UtcNow,
            SourceIp = sourceIp,
            ClientReference = clientReference,
            Payload = rawJson
        };

        db.Set<HubtelCallbackLog>().Add(log);
        await db.SaveChangesAsync(cancellationToken);

        var attempt = await db.Set<OrderPaymentAttempt>()
            .FirstOrDefaultAsync(item => item.ClientReference == clientReference, cancellationToken);

        if (attempt is null)
        {
            log.ProcessingResult = "unknown_reference";
            await db.SaveChangesAsync(cancellationToken);
            logger.LogWarning("Hubtel callback for unknown client reference {ClientReference}", clientReference);
            return CallbackResult.UnknownReference;
        }

        var salesInvoiceId = TryGetString(data, "SalesInvoiceId");
        if (!string.IsNullOrWhiteSpace(salesInvoiceId))
        {
            var order = await db.Set<Order>().FirstAsync(item => item.Id == attempt.OrderId, cancellationToken);
            order.HubtelSalesInvoiceId = salesInvoiceId;
        }

        log.ProcessingResult = "queued";
        log.ProcessedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        await fulfillmentQueue.WriteAsync(attempt.OrderId, cancellationToken);

        return CallbackResult.Queued;
    }

    public async Task<CallbackResult> ProcessRefundCallbackAsync(
        string rawJson,
        string? sourceIp,
        CancellationToken cancellationToken)
    {
        using var document = TryParse(rawJson);

        if (document is null)
        {
            await LogAsync(rawJson, sourceIp, null, "invalid_payload", cancellationToken);
            return CallbackResult.InvalidPayload;
        }

        if (options.Value.EnforceCallbackIp && !IsAllowedIp(sourceIp))
        {
            await LogAsync(rawJson, sourceIp, null, "ip_rejected", cancellationToken);
            return CallbackResult.IpRejected;
        }

        var data = document.RootElement.TryGetProperty("Data", out var dataElement) ? dataElement : document.RootElement;
        var transactionId = TryGetString(data, "TransactionId") ?? TryGetString(data, "transactionId");
        var clientReference = TryGetString(data, "ClientReference") ?? TryGetString(data, "Clientreference");
        var status = TryGetString(data, "Status") ?? TryGetString(document.RootElement, "Status");
        var responseCode = TryGetString(document.RootElement, "ResponseCode");

        var log = new HubtelCallbackLog
        {
            ReceivedAt = DateTime.UtcNow,
            SourceIp = sourceIp,
            ClientReference = clientReference,
            Payload = rawJson
        };

        db.Set<HubtelCallbackLog>().Add(log);
        await db.SaveChangesAsync(cancellationToken);

        var order = await FindRefundTargetAsync(transactionId, clientReference, cancellationToken);

        if (order is null)
        {
            log.ProcessingResult = "unknown_reference";
            await db.SaveChangesAsync(cancellationToken);
            return CallbackResult.UnknownReference;
        }

        var now = DateTime.UtcNow;
        var success = string.Equals(status, "Success", StringComparison.OrdinalIgnoreCase)
                      || string.Equals(status, "0000", StringComparison.Ordinal)
                      || string.Equals(responseCode, "0000", StringComparison.Ordinal);

        if (success)
        {
            await db.Set<Order>()
                .Where(item => item.Id == order.Id && item.Status != OrderStatus.Refunded)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(item => item.Status, OrderStatus.Refunded)
                    .SetProperty(item => item.RefundedAt, now)
                    .SetProperty(item => item.RequiresReview, false)
                    .SetProperty(item => item.ReviewReason, (string?)null)
                    .SetProperty(item => item.UpdatedAt, now),
                    cancellationToken);

            await db.Set<Ticket>()
                .Where(ticket => ticket.OrderId == order.Id)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(ticket => ticket.Status, TicketStatus.Cancelled)
                    .SetProperty(ticket => ticket.UpdatedAt, now),
                    cancellationToken);

            await ticketAvailability.ReleaseStockAsync(order.Id, cancellationToken);

            log.ProcessingResult = "refunded";
            logger.LogInformation("Refund confirmed for order {OrderNumber}", order.OrderNumber);
        }
        else
        {
            await db.Set<Order>()
                .Where(item => item.Id == order.Id && item.Status != OrderStatus.Refunded)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(item => item.Status, OrderStatus.RefundFailed)
                    .SetProperty(item => item.UpdatedAt, now),
                    cancellationToken);

            await ticketAvailability.ReleaseStockAsync(order.Id, cancellationToken);

            log.ProcessingResult = "refund_failed";
            logger.LogWarning("Refund failed for order {OrderNumber} (code {ResponseCode})", order.OrderNumber, responseCode);
        }

        log.ProcessedAt = now;
        await db.SaveChangesAsync(cancellationToken);
        return CallbackResult.Queued;
    }

    private async Task<Order?> FindRefundTargetAsync(
        string? transactionId,
        string? clientReference,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(transactionId))
        {
            var byTransaction = await db.Set<Order>()
                .FirstOrDefaultAsync(item => item.HubtelTransactionId == transactionId, cancellationToken);

            if (byTransaction is not null)
            {
                return byTransaction;
            }
        }

        if (!string.IsNullOrWhiteSpace(clientReference))
        {
            var attempt = await db.Set<OrderPaymentAttempt>()
                .FirstOrDefaultAsync(item => item.ClientReference == clientReference, cancellationToken);

            if (attempt is not null)
            {
                return await db.Set<Order>().FirstOrDefaultAsync(item => item.Id == attempt.OrderId, cancellationToken);
            }
        }

        return null;
    }

    private async Task LogAsync(
        string rawJson,
        string? sourceIp,
        string? clientReference,
        string result,
        CancellationToken cancellationToken)
    {
        db.Set<HubtelCallbackLog>().Add(new HubtelCallbackLog
        {
            ReceivedAt = DateTime.UtcNow,
            SourceIp = sourceIp,
            ClientReference = clientReference,
            Payload = rawJson,
            ProcessingResult = result,
            ProcessedAt = DateTime.UtcNow
        });

        await db.SaveChangesAsync(cancellationToken);
    }

    private static bool IsAllowedIp(string? sourceIp)
        => string.Equals(sourceIp, CallbackSourceIp, StringComparison.Ordinal)
           || sourceIp is ("::1" or "127.0.0.1" or "null");

    private static JsonDocument? TryParse(string rawJson)
    {
        try
        {
            return JsonDocument.Parse(rawJson);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? TryGetString(JsonElement element, string name)
        => element.ValueKind == JsonValueKind.Object &&
           element.TryGetProperty(name, out var value) &&
           value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
