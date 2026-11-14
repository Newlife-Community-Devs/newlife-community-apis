namespace Newlife.Application.Common.Interfaces;

public sealed record CheckoutRequest(
    string ClientReference,
    decimal Amount,
    string Description,
    string CallbackUrl,
    string ReturnUrl,
    string CancellationUrl,
    string PayeeName,
    string PayeeMobileNumber,
    string PayeeEmail);

public sealed record CheckoutResult(
    string CheckoutId,
    string CheckoutUrl,
    string ClientReference);

public enum TransactionStatus
{
    Unknown,
    Paid,
    Unpaid,
    Failed,
    Refunded
}

public sealed record TransactionStatusResult(
    string? TransactionId,
    TransactionStatus Status,
    decimal Amount,
    string? PaymentMethod);

public sealed record RefundResult(
    bool Accepted,
    string Code,
    string Message,
    string? RefundId);

public interface IPaymentGateway
{
    Task<CheckoutResult> CreateCheckoutAsync(CheckoutRequest request, CancellationToken cancellationToken);

    Task<TransactionStatusResult> GetTransactionStatusAsync(string clientReference, CancellationToken cancellationToken);

    Task<RefundResult> RefundAsync(string transactionId, string callbackUrl, CancellationToken cancellationToken);
}

public interface IEmailSender
{
    Task SendAsync(string to, string subject, string htmlBody, CancellationToken cancellationToken);
}
