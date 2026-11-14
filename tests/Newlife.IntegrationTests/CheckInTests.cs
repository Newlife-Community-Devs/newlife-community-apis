using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Newlife.Application.Common.Interfaces;
using Newlife.IntegrationTests.Infrastructure;
using Shouldly;

namespace Newlife.IntegrationTests;

public sealed class CheckInTests(NoRateLimitApiFactory factory) : IClassFixture<NoRateLimitApiFactory>
{
    [Fact]
    public async Task Check_in_is_idempotent_and_reports_original_time()
    {
        var admin = await AdminClientAsync();
        var (eventId, _, ticketTypeId) = await OrderTestHelpers.CreateTicketedEventAsync(
            admin, price: 0.00m, quantity: 10, maxPerOrder: 10);

        var order = await OrderTestHelpers.CreateOrderAsync(factory.CreateClient(), eventId, ticketTypeId, 1);
        var code = order.Tickets!.ShouldHaveSingleItem().Code;

        var first = await admin.PostAsJsonAsync("/api/v1/admin/check-in", new { code });
        first.StatusCode.ShouldBe(HttpStatusCode.OK);
        var firstBody = await first.Content.ReadFromJsonAsync<CheckInShape>();
        firstBody.ShouldNotBeNull();
        firstBody.Result.ShouldBe("CheckedIn");
        firstBody.Ticket.ShouldNotBeNull();
        firstBody.Ticket.Code.ShouldBe(code);
        firstBody.CheckedInAt.ShouldNotBeNull();

        var second = await admin.PostAsJsonAsync("/api/v1/admin/check-in", new { code });
        var secondBody = await second.Content.ReadFromJsonAsync<CheckInShape>();
        secondBody.ShouldNotBeNull();
        secondBody.Result.ShouldBe("AlreadyCheckedIn");
        secondBody.CheckedInAt.ShouldBe(firstBody.CheckedInAt);

        var lowerCase = await admin.PostAsJsonAsync("/api/v1/admin/check-in", new { code = code.ToLowerInvariant() });
        var lowerBody = await lowerCase.Content.ReadFromJsonAsync<CheckInShape>();
        lowerBody.ShouldNotBeNull();
        lowerBody.Result.ShouldBe("AlreadyCheckedIn");

        var unknown = await admin.PostAsJsonAsync("/api/v1/admin/check-in", new { code = "NL-A000000B00" });
        unknown.StatusCode.ShouldBe(HttpStatusCode.OK);
        var unknownBody = await unknown.Content.ReadFromJsonAsync<CheckInShape>();
        unknownBody.ShouldNotBeNull();
        unknownBody.Result.ShouldBe("Invalid");

        var stats = await admin.GetFromJsonAsync<StatsShape>($"/api/v1/admin/check-in/stats?eventId={eventId}");
        stats.ShouldNotBeNull();
        stats.TotalTickets.ShouldBe(1);
        stats.CheckedIn.ShouldBe(1);
        stats.Remaining.ShouldBe(0);
    }

    [Fact]
    public async Task Cancelled_tickets_report_cancelled_result()
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

        var callback = await factory.CreateClient().PostAsync(
            "/api/v1/payments/hubtel/callback?token=test-callback-token",
            new StringContent(
                OrderTestHelpers.CheckoutCallbackBody(clientReference, 50.00m),
                null,
                "application/json"));
        callback.EnsureSuccessStatusCode();

        await OrderTestHelpers.WaitUntilAsync(async () =>
        {
            using var detail = JsonDocument.Parse(await admin.GetStringAsync($"/api/v1/admin/orders/{order.OrderNumber}"));
            return detail.RootElement.GetProperty("status").GetString() == "Paid";
        }, TimeSpan.FromSeconds(20));

        string code;
        using (var paid = JsonDocument.Parse(await admin.GetStringAsync($"/api/v1/admin/orders/{order.OrderNumber}")))
        {
            code = paid.RootElement.GetProperty("tickets")[0].GetProperty("code").GetString()!;
        }

        var refund = await admin.PostAsJsonAsync($"/api/v1/admin/orders/{order.OrderNumber}/refund", new { });
        refund.EnsureSuccessStatusCode();

        var refundCallback = await admin.PostAsync(
            "/api/v1/payments/hubtel/refund-callback?token=test-callback-token",
            new StringContent(
                OrderTestHelpers.RefundCallbackBody(transactionId, success: true),
                null,
                "application/json"));
        refundCallback.EnsureSuccessStatusCode();

        await OrderTestHelpers.WaitUntilAsync(async () =>
        {
            using var detail = JsonDocument.Parse(await admin.GetStringAsync($"/api/v1/admin/orders/{order.OrderNumber}"));
            return detail.RootElement.GetProperty("status").GetString() == "Refunded";
        }, TimeSpan.FromSeconds(20));

        var scan = await admin.PostAsJsonAsync("/api/v1/admin/check-in", new { code });
        scan.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await scan.Content.ReadFromJsonAsync<CheckInShape>();
        body.ShouldNotBeNull();
        body.Result.ShouldBe("Cancelled");

        var stats = await admin.GetFromJsonAsync<StatsShape>($"/api/v1/admin/check-in/stats?eventId={eventId}");
        stats.ShouldNotBeNull();
        stats.TotalTickets.ShouldBe(0);
    }

    [Fact]
    public async Task EventManager_can_check_in_and_anonymous_cannot()
    {
        var admin = await AdminClientAsync();
        var email = $"scanner-{Guid.NewGuid():N}@newlife.test";

        var create = await admin.PostAsJsonAsync("/api/v1/admin/users", new
        {
            email,
            fullName = "Gate Scanner",
            password = "ScannerPass123",
            roles = new[] { "EventManager" }
        });
        create.EnsureSuccessStatusCode();

        var scannerClient = factory.CreateClient();
        var tokens = await AuthTestHelpers.LoginAsync(scannerClient, email, "ScannerPass123");
        scannerClient.WithBearer(tokens.AccessToken);

        var scan = await scannerClient.PostAsJsonAsync("/api/v1/admin/check-in", new { code = "NL-A000000B00" });
        scan.StatusCode.ShouldBe(HttpStatusCode.OK);

        var stats = await scannerClient.GetAsync("/api/v1/admin/check-in/stats");
        stats.StatusCode.ShouldBe(HttpStatusCode.OK);

        var anonymous = await factory.CreateClient().PostAsJsonAsync("/api/v1/admin/check-in", new { code = "x" });
        anonymous.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    private async Task<HttpClient> AdminClientAsync()
    {
        var client = factory.CreateClient();
        var tokens = await AuthTestHelpers.LoginAsync(
            client, AuthTestHelpers.AdminEmail, AuthTestHelpers.AdminPassword);
        return client.WithBearer(tokens.AccessToken);
    }

    private sealed record CheckInShape(
        string Result,
        TicketRow? Ticket,
        DateTime? CheckedInAt);

    private sealed record TicketRow(string Code, string? HolderName, string TicketTypeName, string Status);

    private sealed record StatsShape(int TotalTickets, int CheckedIn, int Remaining);
}
