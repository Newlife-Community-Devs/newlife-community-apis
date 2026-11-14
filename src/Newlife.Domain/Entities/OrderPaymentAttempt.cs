using Newlife.Domain.Common;

namespace Newlife.Domain.Entities;

public sealed class OrderPaymentAttempt : BaseEntity
{
    public Guid OrderId { get; set; }

    public string ClientReference { get; set; } = string.Empty;

    public int AttemptNumber { get; set; }

    public string? HubtelCheckoutId { get; set; }

    public string? CheckoutUrl { get; set; }
}
