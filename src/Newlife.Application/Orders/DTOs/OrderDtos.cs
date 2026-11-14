using Newlife.Application.Common.Models;
using Newlife.Domain.Enums;

namespace Newlife.Application.Orders.DTOs;

public sealed record TicketTypeSelection(Guid TicketTypeId, int Quantity);

public sealed record CreateOrderRequest(
    Guid EventId,
    string BuyerFullName,
    string BuyerEmail,
    string BuyerPhone,
    IReadOnlyList<TicketTypeSelection> Items);

public sealed record PublicOrderItemResponse(
    Guid TicketTypeId,
    string Name,
    int Quantity,
    decimal UnitPrice);

public sealed record PublicTicketResponse(
    string Code,
    string TicketTypeName,
    string? HolderName,
    TicketStatus Status,
    DateTime? CheckedInAt);

public sealed record OrderCreatedResponse(
    string OrderNumber,
    Guid OrderToken,
    OrderStatus Status,
    decimal TotalAmount,
    string Currency,
    string? CheckoutUrl,
    IReadOnlyList<PublicOrderItemResponse> Items,
    IReadOnlyList<PublicTicketResponse>? Tickets);

public sealed record RetryPaymentResponse(
    string OrderNumber,
    Guid OrderToken,
    OrderStatus Status,
    string? CheckoutUrl);

public sealed record OrderReadResponse(
    string OrderNumber,
    OrderStatus Status,
    decimal TotalAmount,
    string Currency,
    string? CheckoutUrl,
    DateTime? PaidAt,
    DateTime? RefundedAt,
    string? PaymentMethod,
    IReadOnlyList<PublicOrderItemResponse> Items,
    IReadOnlyList<PublicTicketResponse> Tickets);

public sealed record AdminOrderListResponse(
    Guid Id,
    string OrderNumber,
    Guid EventId,
    string EventTitle,
    string BuyerFullName,
    string BuyerEmail,
    OrderStatus Status,
    decimal TotalAmount,
    string Currency,
    bool RequiresReview,
    DateTime CreatedAt);

public sealed record AdminOrderResponse(
    Guid Id,
    string OrderNumber,
    Guid EventId,
    string EventTitle,
    string BuyerFullName,
    string BuyerEmail,
    string BuyerPhone,
    OrderStatus Status,
    decimal TotalAmount,
    string Currency,
    string? PaymentMethod,
    bool RequiresReview,
    string? ReviewReason,
    DateTime? PaidAt,
    DateTime? RefundedAt,
    DateTime CreatedAt,
    IReadOnlyList<PublicOrderItemResponse> Items,
    IReadOnlyList<PublicTicketResponse> Tickets,
    IReadOnlyList<CallbackSummaryResponse> Callbacks);

public sealed record CallbackSummaryResponse(
    DateTime ReceivedAt,
    string? ClientReference,
    string? ProcessingResult);

public sealed class AdminOrderQuery : PaginationQuery
{
    public string? Search { get; set; }

    public string? Status { get; set; }

    public Guid? EventId { get; set; }
}

public sealed record CheckInRequest(string Code);

public sealed record CheckInTicketResponse(
    string Code,
    string? HolderName,
    string TicketTypeName,
    TicketStatus Status);

public sealed record CheckInResponse(
    CheckInResultType Result,
    CheckInTicketResponse? Ticket,
    DateTime? CheckedInAt);

public enum CheckInResultType
{
    CheckedIn,
    AlreadyCheckedIn,
    Invalid,
    Cancelled
}

public sealed record CheckInStatsResponse(int TotalTickets, int CheckedIn, int Remaining);
