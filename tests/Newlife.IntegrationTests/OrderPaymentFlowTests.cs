using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Newlife.Application.Common.Interfaces;
using Newlife.IntegrationTests.Infrastructure;
using Shouldly;

namespace Newlife.IntegrationTests;

public sealed class OrderPaymentFlowTests(NoRateLimitApiFactory factory) : IClassFixture<NoRateLimitApiFactory>
{
    [Fact]
    public async Task Callback_plus_status_verification_issues_tickets_exactly_once()
    {
        var admin = await AdminClientAsync();
        var (eventId, _, ticketTypeId) = await OrderTestHelpers.CreateTicketedEventAsync(admin);

        var order = await OrderTestHelpers.CreateOrderAsync(factory.CreateClient(), eventId, ticketTypeId, 2);
        var clientReference = OrderTestHelpers.ClientReferenceFor(order.OrderNumber);
        var transactionId = $"TX-{Guid.NewGuid():N}";

        factory.Gateway.StatusFunc = reference =>
            reference == clientReference
                ? new TransactionStatusResult(transactionId, TransactionStatus.Paid, 100.00m, "mobilemoney")
                : new TransactionStatusResult(null, TransactionStatus.Unpaid, -1, null);

        var callback = await SendCallbackAsync(clientReference, 100.00m);
        callback.StatusCode.ShouldBe(HttpStatusCode.OK);

        using var document = await WaitForStatusAsync(admin, order.OrderNumber, "Paid");

        document.RootElement.GetProperty("tickets").GetArrayLength().ShouldBe(2);
        document.RootElement.GetProperty("requiresReview").GetBoolean().ShouldBeFalse();

        var duplicate = await SendCallbackAsync(clientReference, 100.00m);
        duplicate.StatusCode.ShouldBe(HttpStatusCode.OK);

        await Task.Delay(1000);

        using var afterDuplicate = await GetAdminOrderAsync(admin, order.OrderNumber);
        afterDuplicate.RootElement.GetProperty("status").GetString().ShouldBe("Paid");
        afterDuplicate.RootElement.GetProperty("tickets").GetArrayLength().ShouldBe(2);
    }

    [Fact]
    public async Task Callback_with_invalid_token_is_not_processed()
    {
        var admin = await AdminClientAsync();
        var (eventId, _, ticketTypeId) = await OrderTestHelpers.CreateTicketedEventAsync(admin);
        var order = await OrderTestHelpers.CreateOrderAsync(factory.CreateClient(), eventId, ticketTypeId, 1);

        var client = factory.CreateClient();
        var response = await client.PostAsync(
            "/api/v1/payments/hubtel/callback?token=wrong-token",
            new StringContent(
                OrderTestHelpers.CheckoutCallbackBody(
                    OrderTestHelpers.ClientReferenceFor(order.OrderNumber), 50.00m),
                null,
                "application/json"));

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Callback_for_unknown_reference_returns_ok_without_fulfilment()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsync(
            "/api/v1/payments/hubtel/callback?token=test-callback-token",
            new StringContent(
                OrderTestHelpers.CheckoutCallbackBody("NLUNKNOWN000000001", 10.00m),
                null,
                "application/json"));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Callback_only_fallback_flags_review_and_reconciliation_clears_it()
    {
        var admin = await AdminClientAsync();
        var (eventId, _, ticketTypeId) = await OrderTestHelpers.CreateTicketedEventAsync(admin);
        var order = await OrderTestHelpers.CreateOrderAsync(factory.CreateClient(), eventId, ticketTypeId, 2);
        var clientReference = OrderTestHelpers.ClientReferenceFor(order.OrderNumber);
        var transactionId = $"TX-{Guid.NewGuid():N}";

        factory.Gateway.StatusUnreachable = true;
        try
        {
            var callback = await SendCallbackAsync(clientReference, 100.00m);
            callback.StatusCode.ShouldBe(HttpStatusCode.OK);

            using var document = await WaitForStatusAsync(admin, order.OrderNumber, "Paid");
            document.RootElement.GetProperty("requiresReview").GetBoolean().ShouldBeTrue();
            document.RootElement.GetProperty("reviewReason").GetString().ShouldBe("callback_only_unverified");
            document.RootElement.GetProperty("tickets").GetArrayLength().ShouldBe(2);
        }
        finally
        {
            factory.Gateway.StatusUnreachable = false;
        }

        factory.Gateway.StatusFunc = reference =>
            reference == clientReference
                ? new TransactionStatusResult(transactionId, TransactionStatus.Paid, 100.00m, "mobilemoney")
                : new TransactionStatusResult(null, TransactionStatus.Unknown, -1, null);

        await OrderTestHelpers.WaitUntilAsync(async () =>
        {
            using var document = await GetAdminOrderAsync(admin, order.OrderNumber);
            return !document.RootElement.GetProperty("requiresReview").GetBoolean();
        }, TimeSpan.FromSeconds(20));
    }

    [Fact]
    public async Task Retry_payment_recovers_after_gateway_comes_back()
    {
        var admin = await AdminClientAsync();
        var (eventId, _, ticketTypeId) = await OrderTestHelpers.CreateTicketedEventAsync(admin);
        var client = factory.CreateClient();

        factory.Gateway.FailCreate = true;

        try
        {
            var failed = await client.PostAsJsonAsync("/api/v1/orders", new
            {
                eventId,
                buyerFullName = "Ama Mensah",
                buyerEmail = "ama@example.com",
                buyerPhone = "233244000000",
                items = new[] { new { ticketTypeId, quantity = 1 } }
            });
            failed.StatusCode.ShouldBe(HttpStatusCode.BadGateway);

            using var body = JsonDocument.Parse(await failed.Content.ReadAsStringAsync());
            var orderNumber = body.RootElement.GetProperty("orderNumber").GetString()!;
            var orderToken = body.RootElement.GetProperty("orderToken").GetString()!;

            factory.Gateway.FailCreate = false;

            var retry = await client.PostAsync(
                $"/api/v1/orders/{orderNumber}/retry-payment?token={orderToken}", null);
            retry.StatusCode.ShouldBe(HttpStatusCode.OK);
            var retried = await retry.Content.ReadFromJsonAsync<RetryShape>();
            retried.ShouldNotBeNull();
            retried.Status.ShouldBe("AwaitingPayment");
            retried.CheckoutUrl.ShouldNotBeNull();
        }
        finally
        {
            factory.Gateway.FailCreate = false;
        }
    }

    [Fact]
    public async Task Callback_for_cancelled_order_requires_review_without_tickets()
    {
        var admin = await AdminClientAsync();
        var (eventId, _, ticketTypeId) = await OrderTestHelpers.CreateTicketedEventAsync(admin);
        var order = await OrderTestHelpers.CreateOrderAsync(factory.CreateClient(), eventId, ticketTypeId, 2);
        var clientReference = OrderTestHelpers.ClientReferenceFor(order.OrderNumber);

        ExpireHold(order.OrderNumber);
        using var cancelled = await WaitForStatusAsync(admin, order.OrderNumber, "Cancelled");

        var callback = await SendCallbackAsync(clientReference, 100.00m);
        callback.StatusCode.ShouldBe(HttpStatusCode.OK);

        using var document = await WaitForReviewAsync(admin, order.OrderNumber, "late_payment");
        document.RootElement.GetProperty("status").GetString().ShouldBe("Cancelled");
        document.RootElement.GetProperty("tickets").GetArrayLength().ShouldBe(0);
    }

    [Fact]
    public async Task Expired_paid_payment_is_never_cancelled_by_hold_release()
    {
        var admin = await AdminClientAsync();
        var (eventId, _, ticketTypeId) = await OrderTestHelpers.CreateTicketedEventAsync(admin);
        var order = await OrderTestHelpers.CreateOrderAsync(factory.CreateClient(), eventId, ticketTypeId, 1);
        var clientReference = OrderTestHelpers.ClientReferenceFor(order.OrderNumber);
        var transactionId = $"TX-{Guid.NewGuid():N}";

        factory.Gateway.StatusFunc = reference =>
            reference == clientReference
                ? new TransactionStatusResult(transactionId, TransactionStatus.Paid, 50.00m, "mobilemoney")
                : new TransactionStatusResult(null, TransactionStatus.Unpaid, -1, null);

        ExpireHold(order.OrderNumber);

        using var document = await WaitForStatusAsync(admin, order.OrderNumber, "Paid");
        document.RootElement.GetProperty("tickets").GetArrayLength().ShouldBe(1);
    }

    private void ExpireHold(string orderNumber)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<Newlife.Infrastructure.Persistence.AppDbContext>();

        db.Set<Newlife.Domain.Entities.Order>()
            .Where(order => order.OrderNumber == orderNumber)
            .ExecuteUpdate(setters => setters
                .SetProperty(order => order.HoldExpiresAt, DateTime.UtcNow.AddMinutes(-5)));
    }

    private async Task<HttpResponseMessage> SendCallbackAsync(string clientReference, decimal amount)
    {
        var client = factory.CreateClient();
        return await client.PostAsync(
            "/api/v1/payments/hubtel/callback?token=test-callback-token",
            new StringContent(
                OrderTestHelpers.CheckoutCallbackBody(clientReference, amount),
                null,
                "application/json"));
    }

    private async Task<JsonDocument> GetAdminOrderAsync(HttpClient admin, string orderNumber)
        => JsonDocument.Parse(await admin.GetStringAsync($"/api/v1/admin/orders/{orderNumber}"));

    private async Task<JsonDocument> WaitForStatusAsync(HttpClient admin, string orderNumber, string status)
    {
        JsonDocument? last = null;
        JsonDocument? captured = null;

        await OrderTestHelpers.WaitUntilAsync(async () =>
        {
            last?.Dispose();
            last = await GetAdminOrderAsync(admin, orderNumber);

            if (last.RootElement.GetProperty("status").GetString() == status)
            {
                captured = last;
                last = null;
                return true;
            }

            return false;
        }, TimeSpan.FromSeconds(20));

        captured.ShouldNotBeNull();
        return captured;
    }

    private async Task<JsonDocument> WaitForReviewAsync(HttpClient admin, string orderNumber, string reason)
    {
        JsonDocument? last = null;
        JsonDocument? captured = null;

        await OrderTestHelpers.WaitUntilAsync(async () =>
        {
            last?.Dispose();
            last = await GetAdminOrderAsync(admin, orderNumber);

            if (last.RootElement.GetProperty("requiresReview").GetBoolean()
                && last.RootElement.GetProperty("reviewReason").GetString() == reason)
            {
                captured = last;
                last = null;
                return true;
            }

            return false;
        }, TimeSpan.FromSeconds(20));

        captured.ShouldNotBeNull();
        return captured;
    }

    private async Task<HttpClient> AdminClientAsync()
    {
        var client = factory.CreateClient();
        var tokens = await AuthTestHelpers.LoginAsync(
            client, AuthTestHelpers.AdminEmail, AuthTestHelpers.AdminPassword);
        return client.WithBearer(tokens.AccessToken);
    }

    private sealed record RetryShape(string OrderNumber, Guid OrderToken, string Status, string? CheckoutUrl);
}
