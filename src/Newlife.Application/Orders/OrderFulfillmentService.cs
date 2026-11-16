using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newlife.Application.Common.Interfaces;
using Newlife.Domain.Entities;
using Newlife.Domain.Enums;

namespace Newlife.Application.Orders;

public enum FulfilOutcome
{
    Fulfilled,
    AlreadyProcessed,
    LatePaymentAfterCancel,
    Unpaid,
    GatewayUnreachable
}

public sealed class OrderFulfillmentService(
    IAppDbContext db,
    IPaymentGateway gateway,
    IEmailSender emailSender,
    TicketAvailability ticketAvailability,
    IConfiguration configuration,
    ILogger<OrderFulfillmentService> logger)
{
    public async Task<FulfilOutcome> TryFulfilAsync(Guid orderId, CancellationToken cancellationToken)
    {
        var order = await db.Set<Order>()
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == orderId, cancellationToken)
            ?? throw new Common.Exceptions.NotFoundException("order.not_found", "Order was not found.");

        switch (order.Status)
        {
            case OrderStatus.Cancelled:
                await db.Set<Order>().Where(item => item.Id == orderId).ExecuteUpdateAsync(setters => setters
                    .SetProperty(item => item.RequiresReview, true)
                    .SetProperty(item => item.ReviewReason, "late_payment")
                    .SetProperty(item => item.UpdatedAt, DateTime.UtcNow),
                    cancellationToken);

                var lateLog = await db.Set<HubtelCallbackLog>()
                    .Where(log => log.ClientReference != null &&
                                  EF.Functions.Like(log.ClientReference, $"%{order.OrderNumber.Replace("-", "")}%"))
                    .OrderByDescending(log => log.ReceivedAt)
                    .FirstOrDefaultAsync(cancellationToken);

                if (lateLog is not null)
                {
                    lateLog.ProcessingResult = "late_payment_after_cancel";
                    lateLog.ProcessedAt = DateTime.UtcNow;
                    await db.SaveChangesAsync(cancellationToken);
                }

                logger.LogWarning("Late payment received for cancelled order {OrderNumber}", order.OrderNumber);
                return FulfilOutcome.LatePaymentAfterCancel;

            case OrderStatus.PendingPayment or OrderStatus.AwaitingPayment:
                break;

            default:
                return FulfilOutcome.AlreadyProcessed;
        }

        var latestAttempt = await db.Set<OrderPaymentAttempt>()
            .AsNoTracking()
            .Where(attempt => attempt.OrderId == orderId)
            .OrderByDescending(attempt => attempt.AttemptNumber)
            .FirstOrDefaultAsync(cancellationToken);

        if (latestAttempt is null)
        {
            return FulfilOutcome.Unpaid;
        }

        TransactionStatusResult status;

        try
        {
            status = await gateway.GetTransactionStatusAsync(latestAttempt.ClientReference, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(exception, "Status endpoint unreachable for {ClientReference}", latestAttempt.ClientReference);

            if (await TryFulfilCallbackOnlyAsync(order, latestAttempt, cancellationToken))
            {
                return FulfilOutcome.Fulfilled;
            }

            return FulfilOutcome.GatewayUnreachable;
        }

        return status.Status switch
        {
            TransactionStatus.Paid when status.Amount == order.TotalAmount =>
                await MarkPaidAsync(order, status.TransactionId, status.PaymentMethod, requiresReview: false,
                    reviewReason: null, cancellationToken),
            TransactionStatus.Paid =>
                await RejectAmountMismatchAsync(order, status, cancellationToken),
            TransactionStatus.Refunded =>
                await MarkRefundedAsync(order, cancellationToken),
            _ => FulfilOutcome.Unpaid
        };
    }

    private async Task<FulfilOutcome> MarkPaidAsync(
        Order order,
        string? transactionId,
        string? paymentMethod,
        bool requiresReview,
        string? reviewReason,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;

        var rows = await db.Set<Order>()
            .Where(item => item.Id == order.Id &&
                           (item.Status == OrderStatus.PendingPayment || item.Status == OrderStatus.AwaitingPayment))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.Status, OrderStatus.Paid)
                .SetProperty(item => item.PaidAt, now)
                .SetProperty(item => item.HubtelTransactionId, transactionId)
                .SetProperty(item => item.PaymentMethod, paymentMethod ?? "mobilemoney")
                .SetProperty(item => item.RequiresReview, requiresReview)
                .SetProperty(item => item.ReviewReason, reviewReason)
                .SetProperty(item => item.HoldExpiresAt, (DateTime?)null)
                .SetProperty(item => item.UpdatedAt, now),
                cancellationToken);

        if (rows == 0)
        {
            return FulfilOutcome.AlreadyProcessed;
        }

        await IssueTicketsAsync(order.Id, cancellationToken);

        var reloaded = await db.Set<Order>().FirstAsync(item => item.Id == order.Id, cancellationToken);
        await SendConfirmationAsync(reloaded, cancellationToken);

        return FulfilOutcome.Fulfilled;
    }

    private async Task<bool> TryFulfilCallbackOnlyAsync(
        Order order,
        OrderPaymentAttempt attempt,
        CancellationToken cancellationToken)
    {
        var log = await db.Set<HubtelCallbackLog>()
            .FirstOrDefaultAsync(item => item.ClientReference == attempt.ClientReference, cancellationToken);

        if (log?.ProcessingResult is null or "ip_rejected" or "unknown_reference" or "invalid_payload")
        {
            return false;
        }

        var document = JsonDocument.Parse(log.Payload);

        if (!document.RootElement.TryGetProperty("Data", out var data))
        {
            return false;
        }

        var callbackStatus = data.TryGetProperty("Status", out var statusElement) ? statusElement.GetString() : null;
        var callbackAmount = data.TryGetProperty("Amount", out var amountElement) ? amountElement.GetDecimal() : -1m;

        if (!string.Equals(callbackStatus, "Success", StringComparison.OrdinalIgnoreCase) ||
            callbackAmount != order.TotalAmount)
        {
            return false;
        }

        if (data.TryGetProperty("SalesInvoiceId", out var invoiceElement))
        {
            order.HubtelSalesInvoiceId = invoiceElement.GetString();
        }

        var outcome = await MarkPaidAsync(
            order,
            transactionId: null,
            paymentMethod: null,
            requiresReview: true,
            reviewReason: "callback_only_unverified",
            cancellationToken);

        if (outcome == FulfilOutcome.Fulfilled)
        {
            log.ProcessingResult = "callback_only";
            log.ProcessedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);

            logger.LogWarning(
                "Fulfilled order {OrderNumber} on callback evidence only; status endpoint was unreachable.",
                order.OrderNumber);
        }

        return outcome == FulfilOutcome.Fulfilled;
    }

    private async Task<FulfilOutcome> RejectAmountMismatchAsync(
        Order order,
        TransactionStatusResult status,
        CancellationToken cancellationToken)
    {
        logger.LogWarning(
            "Status reported Paid with amount {Reported} but order {OrderNumber} total is {Total}.",
            status.Amount, order.OrderNumber, order.TotalAmount);

        await db.Set<Order>()
            .Where(item => item.Id == order.Id && item.Status != OrderStatus.Paid)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.RequiresReview, true)
                .SetProperty(item => item.ReviewReason, "amount_mismatch")
                .SetProperty(item => item.UpdatedAt, DateTime.UtcNow),
                cancellationToken);

        return FulfilOutcome.Unpaid;
    }

    private async Task<FulfilOutcome> MarkRefundedAsync(Order order, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;

        await db.Set<Order>()
            .Where(item => item.Id == order.Id)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.Status, OrderStatus.Refunded)
                .SetProperty(item => item.RefundedAt, now)
                .SetProperty(item => item.UpdatedAt, now),
                cancellationToken);

        await db.Set<Ticket>()
            .Where(ticket => ticket.OrderId == order.Id)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(ticket => ticket.Status, TicketStatus.Cancelled)
                .SetProperty(ticket => ticket.UpdatedAt, now),
                cancellationToken);

        await ticketAvailability.ReleaseStockAsync(order.Id, cancellationToken);

        return FulfilOutcome.AlreadyProcessed;
    }

    public async Task IssueTicketsAsync(Guid orderId, CancellationToken cancellationToken)
    {
        if (await db.Set<Ticket>().AnyAsync(ticket => ticket.OrderId == orderId, cancellationToken))
        {
            return;
        }

        var order = await db.Set<Order>().FirstAsync(item => item.Id == orderId, cancellationToken);
        var items = await db.Set<OrderItem>().Where(item => item.OrderId == orderId).ToListAsync(cancellationToken);

        var tickets = OrderTicketIssuer.Issue(order, items);
        db.Set<Ticket>().AddRange(tickets);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task ReverifyCallbackOnlyAsync(CancellationToken cancellationToken)
    {
        var candidates = await db.Set<Order>()
            .Where(order => order.Status == OrderStatus.Paid &&
                            order.RequiresReview &&
                            order.ReviewReason == "callback_only_unverified" &&
                            order.HubtelTransactionId == null)
            .Take(20)
            .ToListAsync(cancellationToken);

        foreach (var order in candidates)
        {
            var latestAttempt = await db.Set<OrderPaymentAttempt>()
                .Where(attempt => attempt.OrderId == order.Id)
                .OrderByDescending(attempt => attempt.AttemptNumber)
                .FirstOrDefaultAsync(cancellationToken);

            if (latestAttempt is null)
            {
                continue;
            }

            TransactionStatusResult status;

            try
            {
                status = await gateway.GetTransactionStatusAsync(latestAttempt.ClientReference, cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogWarning(exception, "Re-verification unreachable for {ClientReference}", latestAttempt.ClientReference);
                continue;
            }

            if (status.Status == TransactionStatus.Paid && status.Amount == order.TotalAmount)
            {
                await db.Set<Order>()
                    .Where(item => item.Id == order.Id)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(item => item.HubtelTransactionId, status.TransactionId)
                        .SetProperty(item => item.PaymentMethod, status.PaymentMethod ?? "mobilemoney")
                        .SetProperty(item => item.RequiresReview, false)
                        .SetProperty(item => item.ReviewReason, (string?)null)
                        .SetProperty(item => item.UpdatedAt, DateTime.UtcNow),
                        cancellationToken);

                logger.LogInformation("Re-verified callback-only order {OrderNumber}", order.OrderNumber);
            }
            else if (status.Status != TransactionStatus.Unknown)
            {
                await db.Set<Order>()
                    .Where(item => item.Id == order.Id)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(item => item.ReviewReason, "callback_status_mismatch")
                        .SetProperty(item => item.UpdatedAt, DateTime.UtcNow),
                        cancellationToken);

                logger.LogWarning(
                    "Callback-only order {OrderNumber} status mismatch: {Status}",
                    order.OrderNumber, status.Status);
            }
        }
    }

    private async Task SendConfirmationAsync(Order order, CancellationToken cancellationToken)
    {
        try
        {
            var evt = await db.Set<Event>()
                .AsNoTracking()
                .FirstAsync(item => item.Id == order.EventId, cancellationToken);

            var tickets = await db.Set<Ticket>()
                .AsNoTracking()
                .Where(ticket => ticket.OrderId == order.Id)
                .ToListAsync(cancellationToken);

            var pageUrl = BuildOrderPageUrl(evt.Slug, order);
            var codes = string.Join(", ", tickets.Select(ticket => ticket.Code));

            await emailSender.SendAsync(
                order.BuyerEmail,
                $"Your tickets for {evt.Title}",
                $"<p>Thank you, {System.Net.WebUtility.HtmlEncode(order.BuyerFullName)}!</p>" +
                $"<p>Your ticket codes: {codes}</p>" +
                $"<p>Order page: <a href=\"{pageUrl}\">{order.OrderNumber}</a></p>",
                cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Confirmation email failed for order {OrderNumber}", order.OrderNumber);
        }
    }

    private string BuildOrderPageUrl(string eventSlug, Order order)
    {
        var frontendBaseUrl = configuration["Frontend:BaseUrl"] ?? string.Empty;
        return $"{frontendBaseUrl.TrimEnd('/')}/events/{eventSlug}/order/{order.OrderNumber}?token={order.OrderToken}";
    }
}
