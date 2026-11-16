using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newlife.Application.Common.Exceptions;
using Newlife.Application.Common.Interfaces;
using Newlife.Application.Orders.DTOs;
using Newlife.Domain.Entities;
using Newlife.Domain.Enums;

namespace Newlife.Application.Orders;

public sealed class OrderService(
    IAppDbContext db,
    IPaymentGateway gateway,
    TicketAvailability ticketAvailability,
    IOptions<HubtelOptions> hubtelOptions,
    IConfiguration configuration,
    ILogger<OrderService> logger)
{
    public async Task<OrderCreatedResponse> CreateAsync(
        CreateOrderRequest request,
        CancellationToken cancellationToken)
    {
        var evt = await db.Set<Event>()
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == request.EventId, cancellationToken)
            ?? throw new NotFoundException("event.not_found", "Event was not found.");

        if (!evt.IsPublished || !evt.TicketingEnabled)
        {
            throw new NotFoundException("ticketing.disabled", "Ticketing is not enabled for this event.");
        }

        var typeIds = request.Items.Select(item => item.TicketTypeId).ToArray();
        var types = await db.Set<TicketType>()
            .Where(type => typeIds.Contains(type.Id))
            .ToDictionaryAsync(type => type.Id, cancellationToken);

        var now = DateTime.UtcNow;
        var lineItems = new List<OrderItem>(request.Items.Count);
        var pricingLines = new List<(decimal UnitPrice, int Quantity)>(request.Items.Count);

        foreach (var selection in request.Items.OrderBy(item => item.TicketTypeId))
        {
            if (!types.TryGetValue(selection.TicketTypeId, out var type) || type.EventId != evt.Id)
            {
                throw new NotFoundException("ticket_type.not_found", "Ticket type was not found for this event.");
            }

            if (!type.IsActive ||
                (type.SalesStartAt is not null && type.SalesStartAt > now) ||
                (type.SalesEndAt is not null && type.SalesEndAt <= now))
            {
                throw new ConflictException("order.not_on_sale", $"Ticket type '{type.Name}' is not on sale.");
            }

            if (selection.Quantity > type.MaxPerOrder)
            {
                throw new BadRequestException(
                    "order.quantity_exceeded",
                    $"A maximum of {type.MaxPerOrder} tickets of type '{type.Name}' can be ordered per purchase.");
            }

            pricingLines.Add((type.Price, selection.Quantity));
            lineItems.Add(new OrderItem
            {
                TicketTypeId = type.Id,
                Quantity = selection.Quantity,
                UnitPrice = type.Price
            });
        }

        var totalAmount = OrderPricing.ComputeTotal(pricingLines);

        await using var transaction = await db.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);

        foreach (var lineItem in lineItems)
        {
            if (!await ticketAvailability.TryReserveAsync(lineItem.TicketTypeId, lineItem.Quantity, cancellationToken))
            {
                await transaction.RollbackAsync(cancellationToken);
                throw new ConflictException(
                    "order.capacity_exceeded",
                    "The requested tickets are no longer available.");
            }
        }

        var order = new Order
        {
            OrderNumber = TicketCodeGenerator.GenerateOrderNumber(),
            EventId = evt.Id,
            BuyerFullName = request.BuyerFullName.Trim(),
            BuyerEmail = request.BuyerEmail.Trim(),
            BuyerPhone = request.BuyerPhone.Trim(),
            Status = OrderStatus.PendingPayment,
            TotalAmount = totalAmount,
            Currency = "GHS",
            HoldExpiresAt = now.AddMinutes(30)
        };

        foreach (var lineItem in lineItems)
        {
            lineItem.OrderId = order.Id;
        }

        db.Set<Order>().Add(order);
        db.Set<OrderItem>().AddRange(lineItems);
        await db.SaveChangesAsync(cancellationToken);

        if (OrderPricing.IsFree(totalAmount))
        {
            order.Status = OrderStatus.Paid;
            order.PaymentMethod = "free";
            order.PaidAt = now;

            db.Set<Ticket>().AddRange(OrderTicketIssuer.Issue(order, lineItems));
            await db.SaveChangesAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);

        IReadOnlyList<PublicTicketResponse>? tickets = null;

        if (order.Status == OrderStatus.Paid)
        {
            tickets = await LoadTicketsAsync(order.Id, cancellationToken);
        }
        else
        {
            await StartCheckoutAsync(order, 1, cancellationToken);
        }

        return new OrderCreatedResponse(
            order.OrderNumber,
            order.OrderToken,
            order.Status,
            order.TotalAmount,
            order.Currency,
            await GetLatestCheckoutUrlAsync(order.Id, cancellationToken),
            await LoadItemsAsync(order.Id, cancellationToken),
            tickets);
    }

    public async Task<RetryPaymentResponse> RetryPaymentAsync(
        string orderNumber,
        Guid orderToken,
        CancellationToken cancellationToken)
    {
        var order = await FindByTokenAsync(orderNumber, orderToken, cancellationToken);

        if (order.Status == OrderStatus.AwaitingPayment)
        {
            var currentUrl = await GetLatestCheckoutUrlAsync(order.Id, cancellationToken);
            return new RetryPaymentResponse(order.OrderNumber, order.OrderToken, order.Status, currentUrl);
        }

        if (order.Status != OrderStatus.PendingPayment)
        {
            throw new ConflictException("order.not_retryable", "This order can no longer retry payment.");
        }

        var attempts = await db.Set<OrderPaymentAttempt>()
            .CountAsync(item => item.OrderId == order.Id, cancellationToken);

        if (attempts >= 3)
        {
            throw new ConflictException("order.retry_limit_reached", "Payment retry limit reached. Please contact support.");
        }

        await StartCheckoutAsync(order, attempts + 1, cancellationToken);
        var checkoutUrl = await GetLatestCheckoutUrlAsync(order.Id, cancellationToken);

        return new RetryPaymentResponse(order.OrderNumber, order.OrderToken, order.Status, checkoutUrl);
    }

    public async Task<OrderReadResponse> ReadAsync(
        string orderNumber,
        Guid orderToken,
        CancellationToken cancellationToken)
    {
        var order = await FindByTokenAsync(orderNumber, orderToken, cancellationToken);
        var items = await LoadItemsAsync(order.Id, cancellationToken);
        var tickets = await LoadTicketsAsync(order.Id, cancellationToken);

        var checkoutUrl = order.Status == OrderStatus.AwaitingPayment
            ? await GetLatestCheckoutUrlAsync(order.Id, cancellationToken)
            : null;

        return new OrderReadResponse(
            order.OrderNumber,
            order.Status,
            order.TotalAmount,
            order.Currency,
            checkoutUrl,
            order.PaidAt,
            order.RefundedAt,
            order.PaymentMethod,
            items,
            tickets);
    }

    private async Task StartCheckoutAsync(Order order, int attemptNumber, CancellationToken cancellationToken)
    {
        var evt = await db.Set<Event>()
            .AsNoTracking()
            .FirstAsync(item => item.Id == order.EventId, cancellationToken);

        var clientReference = TicketCodeGenerator.BuildClientReference(order.OrderNumber, attemptNumber);
        var callbackBaseUrl = hubtelOptions.Value.CallbackBaseUrl.TrimEnd('/');
        var orderPageUrl = BuildOrderPageUrl(evt.Slug, order);

        var request = new CheckoutRequest(
            clientReference,
            order.TotalAmount,
            $"Tickets {order.OrderNumber}",
            $"{callbackBaseUrl}/api/v1/payments/hubtel/callback?token={Uri.EscapeDataString(hubtelOptions.Value.CallbackToken)}",
            orderPageUrl,
            orderPageUrl,
            order.BuyerFullName,
            order.BuyerPhone,
            order.BuyerEmail);

        var attemptRow = await db.Set<OrderPaymentAttempt>()
            .FirstOrDefaultAsync(item => item.OrderId == order.Id && item.ClientReference == clientReference,
                cancellationToken);

        if (attemptRow is null)
        {
            attemptRow = new OrderPaymentAttempt
            {
                OrderId = order.Id,
                ClientReference = clientReference,
                AttemptNumber = attemptNumber
            };
            db.Set<OrderPaymentAttempt>().Add(attemptRow);
            await db.SaveChangesAsync(cancellationToken);
        }

        CheckoutResult result;

        try
        {
            result = await gateway.CreateCheckoutAsync(request, cancellationToken);
        }
        catch (Exception exception) when (exception is not AppException)
        {
            logger.LogWarning(exception, "Hubtel checkout creation failed for order {OrderNumber}", order.OrderNumber);
            throw new PaymentProviderUnavailableException("Payment provider could not be reached. You can retry payment.")
            {
                OrderNumber = order.OrderNumber,
                OrderToken = order.OrderToken
            };
        }

        attemptRow.HubtelCheckoutId = result.CheckoutId;
        attemptRow.CheckoutUrl = result.CheckoutUrl;

        order.Status = OrderStatus.AwaitingPayment;
        order.HoldExpiresAt = DateTime.UtcNow.AddHours(24);
        await db.SaveChangesAsync(cancellationToken);
    }

    private string BuildOrderPageUrl(string eventSlug, Order order)
    {
        var frontendBaseUrl = configuration["Frontend:BaseUrl"] ?? string.Empty;
        return $"{frontendBaseUrl.TrimEnd('/')}/events/{eventSlug}/order/{order.OrderNumber}?token={order.OrderToken}";
    }

    private async Task<Order> FindByTokenAsync(string orderNumber, Guid orderToken, CancellationToken cancellationToken)
        => await db.Set<Order>()
               .FirstOrDefaultAsync(order => order.OrderNumber == orderNumber && order.OrderToken == orderToken, cancellationToken)
           ?? throw new NotFoundException("order.not_found", "Order was not found.");

    private async Task<string?> GetLatestCheckoutUrlAsync(Guid orderId, CancellationToken cancellationToken)
        => await db.Set<OrderPaymentAttempt>()
            .AsNoTracking()
            .Where(attempt => attempt.OrderId == orderId)
            .OrderByDescending(attempt => attempt.AttemptNumber)
            .Select(attempt => attempt.CheckoutUrl)
            .FirstOrDefaultAsync(cancellationToken);

    private async Task<IReadOnlyList<PublicOrderItemResponse>> LoadItemsAsync(
        Guid orderId,
        CancellationToken cancellationToken)
        => await db.Set<OrderItem>()
            .AsNoTracking()
            .Where(item => item.OrderId == orderId)
            .Join(db.Set<TicketType>(), item => item.TicketTypeId, type => type.Id, (item, type) =>
                new PublicOrderItemResponse(type.Id, type.Name, item.Quantity, item.UnitPrice))
            .ToListAsync(cancellationToken);

    private async Task<IReadOnlyList<PublicTicketResponse>> LoadTicketsAsync(
        Guid orderId,
        CancellationToken cancellationToken)
    {
        var tickets = await db.Set<Ticket>()
            .AsNoTracking()
            .Where(ticket => ticket.OrderId == orderId)
            .Join(db.Set<TicketType>(), ticket => ticket.TicketTypeId, type => type.Id, (ticket, type) =>
                new PublicTicketResponse(ticket.Code, type.Name, ticket.HolderName, ticket.Status, ticket.CheckedInAt))
            .ToListAsync(cancellationToken);

        return tickets.OrderBy(ticket => ticket.Code).ToArray();
    }
}
