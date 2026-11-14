using Newlife.Domain.Common;
using Newlife.Domain.Enums;

namespace Newlife.Domain.Entities;

public sealed class Order : BaseEntity
{
    public string OrderNumber { get; set; } = string.Empty;

    public Guid OrderToken { get; set; } = Guid.CreateVersion7();

    public Guid EventId { get; set; }

    public string BuyerFullName { get; set; } = string.Empty;

    public string BuyerEmail { get; set; } = string.Empty;

    public string BuyerPhone { get; set; } = string.Empty;

    public OrderStatus Status { get; set; }

    public decimal TotalAmount { get; set; }

    public string Currency { get; set; } = "GHS";

    public string? HubtelSalesInvoiceId { get; set; }

    public string? HubtelTransactionId { get; set; }

    public string? HubtelRefundId { get; set; }

    public string? PaymentMethod { get; set; }

    public DateTime? PaidAt { get; set; }

    public DateTime? RefundedAt { get; set; }

    public bool RequiresReview { get; set; }

    public string? ReviewReason { get; set; }

    public DateTime? HoldExpiresAt { get; set; }
}
