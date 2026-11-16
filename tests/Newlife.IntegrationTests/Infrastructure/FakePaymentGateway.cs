using System.Collections.Concurrent;
using Newlife.Application.Common.Interfaces;

namespace Newlife.IntegrationTests.Infrastructure;

public sealed class FakePaymentGateway : IPaymentGateway
{
    public ConcurrentBag<CheckoutRequest> CreatedRequests { get; } = [];
    public ConcurrentBag<string> StatusLookups { get; } = [];
    public ConcurrentBag<string> RefundedTransactions { get; } = [];

    public bool FailCreate { get; set; }
    public bool StatusUnreachable { get; set; }
    public Func<string, TransactionStatusResult> StatusFunc { get; set; } =
        _ => new TransactionStatusResult(null, TransactionStatus.Unpaid, -1, null);
    public Func<string, RefundResult> RefundFunc { get; set; } =
        transactionId => new RefundResult(true, "0001", "Accepted", $"refund-{transactionId}");

    public Task<CheckoutResult> CreateCheckoutAsync(CheckoutRequest request, CancellationToken cancellationToken)
    {
        CreatedRequests.Add(request);

        if (FailCreate)
        {
            throw new HttpRequestException("fake gateway unavailable");
        }

        return Task.FromResult(new CheckoutResult(
            $"checkout-{CreatedRequests.Count}",
            $"https://checkout.fake.test/{request.ClientReference}",
            request.ClientReference));
    }

    public Task<TransactionStatusResult> GetTransactionStatusAsync(
        string clientReference,
        CancellationToken cancellationToken)
    {
        StatusLookups.Add(clientReference);

        if (StatusUnreachable)
        {
            throw new HttpRequestException("fake status endpoint unreachable");
        }

        return Task.FromResult(StatusFunc(clientReference));
    }

    public Task<RefundResult> RefundAsync(
        string transactionId,
        string callbackUrl,
        CancellationToken cancellationToken)
    {
        RefundedTransactions.Add(transactionId);
        return Task.FromResult(RefundFunc(transactionId));
    }
}
