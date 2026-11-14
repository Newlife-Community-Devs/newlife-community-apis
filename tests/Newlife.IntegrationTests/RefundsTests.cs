using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Newlife.Application.Common.Interfaces;
using Newlife.IntegrationTests.Infrastructure;
using Shouldly;

namespace Newlife.IntegrationTests;

public sealed class RefundsTests(NoRateLimitApiFactory factory) : IClassFixture<NoRateLimitApiFactory>
{
    [Fact]
    public async Task Refund_success_flow_cancels_tickets()
    {
        var admin = await AdminClientAsync();
        var (order, transactionId) = await CreatePaidOrderAsync(admin);

        var refund = await admin.PostAsJsonAsync($"/api/v1/admin/orders/{order.OrderNumber}/refund", new { });
        refund.StatusCode.ShouldBe(HttpStatusCode.OK);

        using (var pending = JsonDocument.Parse(await refund.Content.ReadAsStringAsync()))
        {
            pending.RootElement.GetProperty("status").GetString().ShouldBe("RefundPending");
        }

        factory.Gateway.RefundedTransactions.ShouldContain(transactionId);

        var callback = await admin.PostAsync(
            "/api/v1/payments/hubtel/refund-callback?token=test-callback-token",
            new StringContent(OrderTestHelpers.RefundCallbackBody(transactionId, success: true), null, "application/json"));
        callback.StatusCode.ShouldBe(HttpStatusCode.OK);

        using var document = await WaitForAdminStatusAsync(admin, order.OrderNumber, "Refunded");
        document.RootElement.GetProperty("refundedAt").ValueKind.ShouldBe(JsonValueKind.String);

        foreach (var ticket in document.RootElement.GetProperty("tickets").EnumerateArray())
        {
            ticket.GetProperty("status").GetString().ShouldBe("Cancelled");
        }
    }

    [Fact]
    public async Task Refund_failure_flow_keeps_tickets_valid()
    {
        var admin = await AdminClientAsync();
        var (order, transactionId) = await CreatePaidOrderAsync(admin);

        var refund = await admin.PostAsJsonAsync($"/api/v1/admin/orders/{order.OrderNumber}/refund", new { });
        refund.StatusCode.ShouldBe(HttpStatusCode.OK);

        var callback = await admin.PostAsync(
            "/api/v1/payments/hubtel/refund-callback?token=test-callback-token",
            new StringContent(OrderTestHelpers.RefundCallbackBody(transactionId, success: false), null, "application/json"));
        callback.StatusCode.ShouldBe(HttpStatusCode.OK);

        using var document = await WaitForAdminStatusAsync(admin, order.OrderNumber, "RefundFailed");
        foreach (var ticket in document.RootElement.GetProperty("tickets").EnumerateArray())
        {
            ticket.GetProperty("status").GetString().ShouldBe("Valid");
        }

        var retry = await admin.PostAsJsonAsync($"/api/v1/admin/orders/{order.OrderNumber}/refund", new { });
        retry.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Immediate_hubtel_rejection_returns_409()
    {
        var admin = await AdminClientAsync();
        var (order, _) = await CreatePaidOrderAsync(admin);

        factory.Gateway.RefundFunc = _ => new RefundResult(false, "4509", "Not eligible", null);

        try
        {
            var refund = await admin.PostAsJsonAsync($"/api/v1/admin/orders/{order.OrderNumber}/refund", new { });
            refund.StatusCode.ShouldBe(HttpStatusCode.Conflict);
            (await AuthTestHelpers.ReadProblemCodeAsync(refund)).ShouldBe("refund.rejected");

            using var detail = await GetAdminOrderAsync(admin, order.OrderNumber);
            detail.RootElement.GetProperty("status").GetString().ShouldBe("Paid");
        }
        finally
        {
            factory.Gateway.RefundFunc = transactionId =>
                new RefundResult(true, "0001", "Accepted", $"refund-{transactionId}");
        }
    }

    [Fact]
    public async Task Card_orders_cannot_be_refunded_via_api()
    {
        var admin = await AdminClientAsync();
        var marker = Guid.NewGuid().ToString("N");
        var (eventId, _, ticketTypeId) = await OrderTestHelpers.CreateTicketedEventAsync(admin);

        var order = await OrderTestHelpers.CreateOrderAsync(factory.CreateClient(), eventId, ticketTypeId, 1);
        var clientReference = OrderTestHelpers.ClientReferenceFor(order.OrderNumber);

        factory.Gateway.StatusFunc = reference =>
            reference == clientReference
                ? new TransactionStatusResult($"TX-card-{marker}", TransactionStatus.Paid, 50.00m, "card")
                : new TransactionStatusResult(null, TransactionStatus.Unpaid, -1, null);

        await SendCallbackAsync(clientReference, 50.00m);
        await WaitForAdminStatusAsync(admin, order.OrderNumber, "Paid");

        var refund = await admin.PostAsJsonAsync($"/api/v1/admin/orders/{order.OrderNumber}/refund", new { });

        refund.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await AuthTestHelpers.ReadProblemCodeAsync(refund)).ShouldBe("refund.unsupported_channel");
    }

    [Fact]
    public async Task Callback_only_orders_are_not_refundable_until_verified()
    {
        var admin = await AdminClientAsync();
        var (eventId, _, ticketTypeId) = await OrderTestHelpers.CreateTicketedEventAsync(admin);
        var order = await OrderTestHelpers.CreateOrderAsync(factory.CreateClient(), eventId, ticketTypeId, 1);
        var clientReference = OrderTestHelpers.ClientReferenceFor(order.OrderNumber);

        factory.Gateway.StatusUnreachable = true;

        try
        {
            await SendCallbackAsync(clientReference, 50.00m);
            await WaitForAdminStatusAsync(admin, order.OrderNumber, "Paid");

            var refund = await admin.PostAsJsonAsync($"/api/v1/admin/orders/{order.OrderNumber}/refund", new { });

            refund.StatusCode.ShouldBe(HttpStatusCode.Conflict);
            (await AuthTestHelpers.ReadProblemCodeAsync(refund)).ShouldBe("order.not_refundable");

            var reviewed = await admin.PostAsJsonAsync(
                $"/api/v1/admin/orders/{order.OrderNumber}/mark-reviewed", new { });
            reviewed.StatusCode.ShouldBe(HttpStatusCode.OK);

            using var detail = await GetAdminOrderAsync(admin, order.OrderNumber);
            detail.RootElement.GetProperty("requiresReview").GetBoolean().ShouldBeFalse();
        }
        finally
        {
            factory.Gateway.StatusUnreachable = false;
            factory.Gateway.StatusFunc = _ =>
                new TransactionStatusResult(null, TransactionStatus.Unpaid, -1, null);
        }
    }

    [Fact]
    public async Task Admin_list_filters_by_status_event_and_search()
    {
        var admin = await AdminClientAsync();
        var (eventId, _, ticketTypeId) = await OrderTestHelpers.CreateTicketedEventAsync(admin);
        var order = await OrderTestHelpers.CreateOrderAsync(factory.CreateClient(), eventId, ticketTypeId, 1);

        var byNumber = await admin.GetFromJsonAsync<OrderPage>(
            $"/api/v1/admin/orders?search={order.OrderNumber}&status=AwaitingPayment");
        byNumber.ShouldNotBeNull();
        byNumber.Items.ShouldContain(item => item.OrderNumber == order.OrderNumber);

        var byEvent = await admin.GetFromJsonAsync<OrderPage>($"/api/v1/admin/orders?eventId={eventId}");
        byEvent.ShouldNotBeNull();
        byEvent.Items.ShouldContain(item => item.OrderNumber == order.OrderNumber);

        var badStatus = await admin.GetAsync("/api/v1/admin/orders?status=NotAStatus");
        badStatus.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await AuthTestHelpers.ReadProblemCodeAsync(badStatus)).ShouldBe("order.invalid_status");
    }

    private async Task SendCallbackAsync(string clientReference, decimal amount)
    {
        var client = factory.CreateClient();
        var response = await client.PostAsync(
            "/api/v1/payments/hubtel/callback?token=test-callback-token",
            new StringContent(
                OrderTestHelpers.CheckoutCallbackBody(clientReference, amount),
                null,
                "application/json"));
        response.EnsureSuccessStatusCode();
    }

    private async Task<(OrderTestHelpers.CreateOrderShape Order, string TransactionId)> CreatePaidOrderAsync(
        HttpClient admin)
    {
        var (eventId, _, ticketTypeId) = await OrderTestHelpers.CreateTicketedEventAsync(admin);
        var order = await OrderTestHelpers.CreateOrderAsync(factory.CreateClient(), eventId, ticketTypeId, 2);
        var clientReference = OrderTestHelpers.ClientReferenceFor(order.OrderNumber);
        var transactionId = $"TX-{Guid.NewGuid():N}";

        factory.Gateway.StatusFunc = reference =>
            reference == clientReference
                ? new TransactionStatusResult(transactionId, TransactionStatus.Paid, 100.00m, "mobilemoney")
                : new TransactionStatusResult(null, TransactionStatus.Unpaid, -1, null);

        await SendCallbackAsync(clientReference, 100.00m);
        await WaitForAdminStatusAsync(admin, order.OrderNumber, "Paid");

        return (order, transactionId);
    }

    private async Task<JsonDocument> GetAdminOrderAsync(HttpClient admin, string orderNumber)
        => JsonDocument.Parse(await admin.GetStringAsync($"/api/v1/admin/orders/{orderNumber}"));

    private async Task<JsonDocument> WaitForAdminStatusAsync(HttpClient admin, string orderNumber, string status)
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

    private async Task<HttpClient> AdminClientAsync()
    {
        var client = factory.CreateClient();
        var tokens = await AuthTestHelpers.LoginAsync(
            client, AuthTestHelpers.AdminEmail, AuthTestHelpers.AdminPassword);
        return client.WithBearer(tokens.AccessToken);
    }

    private sealed record OrderItemRow(string OrderNumber, string Status);

    private sealed record OrderPage(OrderItemRow[] Items, int Page, int PageSize, int TotalCount);
}
