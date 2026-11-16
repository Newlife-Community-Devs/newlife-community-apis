using System.Net;
using System.Net.Http.Json;
using Newlife.IntegrationTests.Infrastructure;
using Shouldly;

namespace Newlife.IntegrationTests;

public sealed class OrdersLifecycleTests(NoRateLimitApiFactory factory) : IClassFixture<NoRateLimitApiFactory>
{
    [Fact]
    public async Task Paid_order_creation_returns_checkout_and_stops_overselling()
    {
        var admin = await AdminClientAsync();
        var (eventId, _, ticketTypeId) = await OrderTestHelpers.CreateTicketedEventAsync(
            admin, quantity: 5, maxPerOrder: 5);
        var client = factory.CreateClient();

        var before = factory.Gateway.CreatedRequests.Count;
        var order = await OrderTestHelpers.CreateOrderAsync(client, eventId, ticketTypeId, 2);

        order.Status.ShouldBe("AwaitingPayment");
        order.TotalAmount.ShouldBe(100.00m);
        order.Currency.ShouldBe("GHS");
        order.CheckoutUrl.ShouldNotBeNull();
        order.Items.ShouldHaveSingleItem().Quantity.ShouldBe(2);

        var suffix = order.OrderNumber.Replace("NL-", string.Empty);
        var request = factory.Gateway.CreatedRequests
            .Single(item => item.ClientReference.StartsWith("NL" + suffix, StringComparison.Ordinal));
        request.ClientReference.ShouldMatch("^[A-Z0-9]+$");
        request.ClientReference.Length.ShouldBeLessThanOrEqualTo(32);
        request.Amount.ShouldBe(100.00m);
        request.Description.ShouldBe($"Tickets {order.OrderNumber}");

        await OrderTestHelpers.CreateOrderAsync(client, eventId, ticketTypeId, 2);

        var oversell = await client.PostAsJsonAsync("/api/v1/orders", new
        {
            eventId,
            buyerFullName = "Kwame Boateng",
            buyerEmail = "kwame@example.com",
            buyerPhone = "233244000000",
            items = new[] { new { ticketTypeId, quantity = 2 } }
        });

        oversell.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await AuthTestHelpers.ReadProblemCodeAsync(oversell)).ShouldBe("order.capacity_exceeded");
    }

    [Fact]
    public async Task Create_validates_buyer_phone_quantity_and_distinct_types()
    {
        var admin = await AdminClientAsync();
        var (eventId, _, ticketTypeId) = await OrderTestHelpers.CreateTicketedEventAsync(admin);
        var client = factory.CreateClient();

        var badPhone = await client.PostAsJsonAsync("/api/v1/orders", new
        {
            eventId,
            buyerFullName = "Ama Mensah",
            buyerEmail = "ama@example.com",
            buyerPhone = "12345",
            items = new[] { new { ticketTypeId, quantity = 1 } }
        });
        badPhone.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await AuthTestHelpers.ReadProblemCodeAsync(badPhone)).ShouldBe("validation.failed");

        var overMax = await client.PostAsJsonAsync("/api/v1/orders", new
        {
            eventId,
            buyerFullName = "Ama Mensah",
            buyerEmail = "ama@example.com",
            buyerPhone = "233244000000",
            items = new[] { new { ticketTypeId, quantity = 11 } }
        });
        overMax.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await AuthTestHelpers.ReadProblemCodeAsync(overMax)).ShouldBe("order.quantity_exceeded");

        var duplicateTypes = await client.PostAsJsonAsync("/api/v1/orders", new
        {
            eventId,
            buyerFullName = "Ama Mensah",
            buyerEmail = "ama@example.com",
            buyerPhone = "233244000000",
            items = new[]
            {
                new { ticketTypeId, quantity = 1 },
                new { ticketTypeId, quantity = 1 }
            }
        });
        duplicateTypes.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Free_order_issues_tickets_immediately_without_checkout()
    {
        var admin = await AdminClientAsync();
        var (eventId, _, ticketTypeId) = await OrderTestHelpers.CreateTicketedEventAsync(
            admin, price: 0.00m, quantity: 50, maxPerOrder: 50);

        var before = factory.Gateway.CreatedRequests.Count;
        var order = await OrderTestHelpers.CreateOrderAsync(factory.CreateClient(), eventId, ticketTypeId, 2);

        order.Status.ShouldBe("Paid");
        order.CheckoutUrl.ShouldBeNull();
        order.Tickets.ShouldNotBeNull();
        order.Tickets.Length.ShouldBe(2);
        order.Tickets.All(ticket => ticket.Status == "Valid").ShouldBeTrue();
        factory.Gateway.CreatedRequests.Count.ShouldBe(before);

        var read = await factory.CreateClient()
            .GetFromJsonAsync<ReadOrderShape>($"/api/v1/orders/{order.OrderNumber}?token={order.OrderToken}");
        read.ShouldNotBeNull();
        read.PaymentMethod.ShouldBe("free");
        read.PaidAt.ShouldNotBeNull();
    }

    [Fact]
    public async Task Order_reads_require_the_correct_token()
    {
        var admin = await AdminClientAsync();
        var (eventId, _, ticketTypeId) = await OrderTestHelpers.CreateTicketedEventAsync(admin);
        var order = await OrderTestHelpers.CreateOrderAsync(factory.CreateClient(), eventId, ticketTypeId, 2);

        var read = await factory.CreateClient()
            .GetFromJsonAsync<ReadOrderShape>($"/api/v1/orders/{order.OrderNumber}?token={order.OrderToken}");
        read.ShouldNotBeNull();
        read.Status.ShouldBe("AwaitingPayment");
        read.CheckoutUrl.ShouldNotBeNull();

        var wrongToken = await factory.CreateClient()
            .GetAsync($"/api/v1/orders/{order.OrderNumber}?token={Guid.NewGuid()}");
        wrongToken.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await AuthTestHelpers.ReadProblemCodeAsync(wrongToken)).ShouldBe("order.not_found");

        var noToken = await factory.CreateClient().GetAsync($"/api/v1/orders/{order.OrderNumber}");
        noToken.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Qr_png_endpoint_validates_token()
    {
        var admin = await AdminClientAsync();
        var (eventId, _, ticketTypeId) = await OrderTestHelpers.CreateTicketedEventAsync(
            admin, price: 0.00m, quantity: 10, maxPerOrder: 10);

        var order = await OrderTestHelpers.CreateOrderAsync(factory.CreateClient(), eventId, ticketTypeId, 1);
        var code = order.Tickets!.ShouldHaveSingleItem().Code;

        var png = await factory.CreateClient().GetAsync($"/api/v1/tickets/{code}/qr.png?token={order.OrderToken}");
        png.StatusCode.ShouldBe(HttpStatusCode.OK);
        png.Content.Headers.ContentType!.MediaType.ShouldBe("image/png");
        var bytes = await png.Content.ReadAsByteArrayAsync();
        bytes.Take(8).ToArray().ShouldBe([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);

        var badToken = await factory.CreateClient().GetAsync($"/api/v1/tickets/{code}/qr.png?token={Guid.NewGuid()}");
        badToken.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        var badCode = await factory.CreateClient()
            .GetAsync($"/api/v1/tickets/NL-A000000000/qr.png?token={order.OrderToken}");
        badCode.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Gateway_outage_returns_502_with_retry_handle_and_limit_of_three()
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

            using var body = System.Text.Json.JsonDocument.Parse(await failed.Content.ReadAsStringAsync());
            var orderNumber = body.RootElement.GetProperty("orderNumber").GetString()!;
            var orderToken = body.RootElement.GetProperty("orderToken").GetString()!;

            for (var attempt = 2; attempt <= 3; attempt++)
            {
                var retry = await client.PostAsync(
                    $"/api/v1/orders/{orderNumber}/retry-payment?token={orderToken}", null);

                (await AuthTestHelpers.ReadProblemCodeAsync(retry)).ShouldBe("payment.provider_unavailable");
            }

            (await AuthTestHelpers.ReadProblemCodeAsync(
                await client.PostAsync($"/api/v1/orders/{orderNumber}/retry-payment?token={orderToken}", null)))
                .ShouldBe("order.retry_limit_reached");
        }
        finally
        {
            factory.Gateway.FailCreate = false;
        }

        var recovery = await client.PostAsJsonAsync("/api/v1/orders", new
        {
            eventId,
            buyerFullName = "Ama Mensah",
            buyerEmail = "ama@example.com",
            buyerPhone = "233244000000",
            items = new[] { new { ticketTypeId, quantity = 1 } }
        });
        recovery.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Concurrent_creates_never_exceed_capacity()
    {
        var admin = await AdminClientAsync();
        var (eventId, slug, ticketTypeId) = await OrderTestHelpers.CreateTicketedEventAsync(
            admin, quantity: 100, maxPerOrder: 5);

        var before = factory.Gateway.CreatedRequests.Count;

        var tasks = Enumerable.Range(0, 40)
            .Select(_ => Task.Run(async () =>
            {
                var requestClient = factory.CreateClient();
                return await requestClient.PostAsJsonAsync("/api/v1/orders", new
                {
                    eventId,
                    buyerFullName = "Concurrent Buyer",
                    buyerEmail = "buyer@example.com",
                    buyerPhone = "233244000000",
                    items = new[] { new { ticketTypeId, quantity = 5 } }
                });
            }))
            .ToArray();

        var responses = await Task.WhenAll(tasks);

        var successes = responses.Count(response => response.StatusCode == HttpStatusCode.Created);
        var conflicts = responses.Count(response => response.StatusCode == HttpStatusCode.Conflict);

        (successes + conflicts).ShouldBe(40);
        successes.ShouldBeLessThanOrEqualTo(20);
        successes.ShouldBe(20);

        var adminOrders = await admin.GetFromJsonAsync<AdminOrderPage>($"/api/v1/admin/orders?eventId={eventId}&pageSize=100");
        adminOrders.ShouldNotBeNull();

        adminOrders.Items
            .Count(item => item.Status is "PendingPayment" or "AwaitingPayment" or "Paid")
            .ShouldBe(successes);

        var checkoutCalls = factory.Gateway.CreatedRequests.Count - before;
        checkoutCalls.ShouldBe(successes);

        var types = await factory.CreateClient()
            .GetFromJsonAsync<RemainingRow[]>($"/api/v1/events/{slug}/ticket-types");
        types.ShouldNotBeNull();
        types.ShouldHaveSingleItem().Remaining.ShouldBe(0);
    }

    private async Task<HttpClient> AdminClientAsync()
    {
        var client = factory.CreateClient();
        var tokens = await AuthTestHelpers.LoginAsync(
            client, AuthTestHelpers.AdminEmail, AuthTestHelpers.AdminPassword);
        return client.WithBearer(tokens.AccessToken);
    }

    private sealed record ReadOrderShape(
        string OrderNumber,
        string Status,
        decimal TotalAmount,
        string Currency,
        string? CheckoutUrl,
        DateTime? PaidAt,
        DateTime? RefundedAt,
        string? PaymentMethod);

    private sealed record RemainingRow(int Remaining);

    private sealed record AdminOrderRow(string OrderNumber, string Status);

    private sealed record AdminOrderPage(AdminOrderRow[] Items, int Page, int PageSize, int TotalCount);
}
