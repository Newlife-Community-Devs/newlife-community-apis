using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newlife.Application.Common.Exceptions;
using Newlife.Application.Common.Interfaces;
using Newlife.Application.Common.Models;
using Newlife.Application.Orders.DTOs;
using Newlife.Domain.Entities;
using Newlife.Domain.Enums;

namespace Newlife.Application.Orders;

public sealed class AdminOrderService(
    IAppDbContext db,
    IPaymentGateway gateway,
    IOptions<HubtelOptions> hubtelOptions,
    ILogger<AdminOrderService> logger)
{
    public async Task<PagedResult<AdminOrderListResponse>> ListAsync(
        AdminOrderQuery query,
        CancellationToken cancellationToken)
    {
        IQueryable<Domain.Entities.Order> source = db.Set<Domain.Entities.Order>().AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var pattern = $"%{query.Search.Trim().ToLowerInvariant()}%";
            source = source.Where(order =>
                EF.Functions.Like(order.OrderNumber.ToLower(), pattern)
                || EF.Functions.Like(order.BuyerEmail.ToLower(), pattern)
                || EF.Functions.Like(order.BuyerPhone.ToLower(), pattern)
                || EF.Functions.Like(order.BuyerFullName.ToLower(), pattern));
        }

        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            if (!Enum.TryParse<OrderStatus>(query.Status, ignoreCase: true, out var status) ||
                !Enum.IsDefined(status))
            {
                throw new BadRequestException("order.invalid_status", "Unknown order status filter.");
            }

            source = source.Where(order => order.Status == status);
        }

        if (query.EventId.HasValue)
        {
            source = source.Where(order => order.EventId == query.EventId.Value);
        }

        var totalCount = await source.CountAsync(cancellationToken);
        var orders = await source
            .OrderByDescending(order => order.CreatedAt)
            .Skip(query.Skip)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        var eventIds = orders.Select(order => order.EventId).Distinct().ToArray();
        var eventTitles = await db.Set<Event>()
            .AsNoTracking()
            .Where(evt => eventIds.Contains(evt.Id))
            .ToDictionaryAsync(evt => evt.Id, evt => evt.Title, cancellationToken);

        return PagedResult<AdminOrderListResponse>.Create(
            orders.Select(order => new AdminOrderListResponse(
                order.Id,
                order.OrderNumber,
                order.EventId,
                eventTitles.GetValueOrDefault(order.EventId, string.Empty),
                order.BuyerFullName,
                order.BuyerEmail,
                order.Status,
                order.TotalAmount,
                order.Currency,
                order.RequiresReview,
                order.CreatedAt)).ToArray(),
            query.Page,
            query.PageSize,
            totalCount);
    }

    public async Task<AdminOrderResponse> GetDetailAsync(string orderNumber, CancellationToken cancellationToken)
    {
        var order = await db.Set<Domain.Entities.Order>()
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.OrderNumber == orderNumber, cancellationToken)
            ?? throw new NotFoundException("order.not_found", "Order was not found.");

        var evt = await db.Set<Event>()
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == order.EventId, cancellationToken);

        var items = await db.Set<OrderItem>()
            .AsNoTracking()
            .Where(item => item.OrderId == order.Id)
            .Join(db.Set<TicketType>(), item => item.TicketTypeId, type => type.Id, (item, type) =>
                new PublicOrderItemResponse(type.Id, type.Name, item.Quantity, item.UnitPrice))
            .ToListAsync(cancellationToken);

        var ticketRows = await db.Set<Ticket>()
            .AsNoTracking()
            .Where(ticket => ticket.OrderId == order.Id)
            .Join(db.Set<TicketType>(), ticket => ticket.TicketTypeId, type => type.Id, (ticket, type) =>
                new PublicTicketResponse(ticket.Code, type.Name, ticket.HolderName, ticket.Status, ticket.CheckedInAt))
            .ToListAsync(cancellationToken);

        var tickets = ticketRows.OrderBy(ticket => ticket.Code).ToArray();

        var references = await db.Set<OrderPaymentAttempt>()
            .AsNoTracking()
            .Where(attempt => attempt.OrderId == order.Id)
            .Select(attempt => attempt.ClientReference)
            .ToListAsync(cancellationToken);

        var callbacks = await db.Set<HubtelCallbackLog>()
            .AsNoTracking()
            .Where(log => log.ClientReference != null && references.Contains(log.ClientReference))
            .OrderByDescending(log => log.ReceivedAt)
            .Take(20)
            .Select(log => new CallbackSummaryResponse(log.ReceivedAt, log.ClientReference, log.ProcessingResult))
            .ToListAsync(cancellationToken);

        return new AdminOrderResponse(
            order.Id,
            order.OrderNumber,
            order.EventId,
            evt?.Title ?? string.Empty,
            order.BuyerFullName,
            order.BuyerEmail,
            order.BuyerPhone,
            order.Status,
            order.TotalAmount,
            order.Currency,
            order.PaymentMethod,
            order.RequiresReview,
            order.ReviewReason,
            order.PaidAt,
            order.RefundedAt,
            order.CreatedAt,
            items,
            tickets,
            callbacks);
    }

    public async Task<AdminOrderResponse> RefundAsync(string orderNumber, CancellationToken cancellationToken)
    {
        var order = await db.Set<Domain.Entities.Order>()
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.OrderNumber == orderNumber, cancellationToken)
            ?? throw new NotFoundException("order.not_found", "Order was not found.");

        if (order.Status != OrderStatus.Paid && order.Status != OrderStatus.RefundFailed)
        {
            throw new ConflictException("order.not_refundable", "Only paid orders can be refunded.");
        }

        if (string.Equals(order.PaymentMethod, "card", StringComparison.OrdinalIgnoreCase))
        {
            throw new UnprocessableEntityException(
                "refund.unsupported_channel",
                "Card refunds are not supported; process them in the Hubtel dashboard.");
        }

        if (string.IsNullOrWhiteSpace(order.HubtelTransactionId))
        {
            throw new ConflictException(
                "order.not_refundable",
                "This order cannot be refunded yet: the transaction is not verified.");
        }

        var callbackBaseUrl = hubtelOptions.Value.CallbackBaseUrl.TrimEnd('/');
        var refundCallbackUrl =
            $"{callbackBaseUrl}/api/v1/payments/hubtel/refund-callback?token={Uri.EscapeDataString(hubtelOptions.Value.CallbackToken)}";

        RefundResult result;

        try
        {
            result = await gateway.RefundAsync(order.HubtelTransactionId, refundCallbackUrl, cancellationToken);
        }
        catch (Exception exception) when (exception is not AppException)
        {
            logger.LogWarning(exception, "Hubtel refund request failed for {OrderNumber}", orderNumber);
            throw new PaymentProviderUnavailableException("Payment provider could not be reached for the refund.");
        }

        var now = DateTime.UtcNow;

        if (result.Accepted)
        {
            await db.Set<Domain.Entities.Order>()
                .Where(item => item.Id == order.Id)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(item => item.Status, OrderStatus.RefundPending)
                    .SetProperty(item => item.HubtelRefundId, result.RefundId)
                    .SetProperty(item => item.UpdatedAt, now),
                    cancellationToken);
        }
        else
        {
            throw new ConflictException(
                "refund.rejected",
                $"Hubtel rejected the refund (code {result.Code}): {result.Message}");
        }

        return await GetDetailAsync(orderNumber, cancellationToken);
    }

    public async Task<AdminOrderResponse> MarkReviewedAsync(string orderNumber, CancellationToken cancellationToken)
    {
        var order = await db.Set<Domain.Entities.Order>()
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.OrderNumber == orderNumber, cancellationToken)
            ?? throw new NotFoundException("order.not_found", "Order was not found.");

        await db.Set<Domain.Entities.Order>()
            .Where(item => item.Id == order.Id)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.RequiresReview, false)
                .SetProperty(item => item.ReviewReason, (string?)null)
                .SetProperty(item => item.UpdatedAt, DateTime.UtcNow),
                cancellationToken);

        return await GetDetailAsync(orderNumber, cancellationToken);
    }
}
