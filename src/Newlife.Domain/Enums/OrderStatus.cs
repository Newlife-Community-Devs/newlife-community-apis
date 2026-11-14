namespace Newlife.Domain.Enums;

public enum OrderStatus
{
    PendingPayment,
    AwaitingPayment,
    Paid,
    RefundPending,
    Refunded,
    RefundFailed,
    Cancelled,
    Expired
}
