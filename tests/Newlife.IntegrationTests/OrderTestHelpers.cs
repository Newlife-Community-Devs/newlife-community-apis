using System.Net.Http.Json;
using Shouldly;
using Newlife.Application.Orders;
using Newlife.IntegrationTests.Infrastructure;

namespace Newlife.IntegrationTests;

internal static class OrderTestHelpers
{
    public static string ClientReferenceFor(string orderNumber, int attempt = 1)
        => TicketCodeGenerator.BuildClientReference(orderNumber, attempt);

    public static async Task<(Guid Id, string Slug, Guid TicketTypeId)> CreateTicketedEventAsync(
        HttpClient adminClient,
        decimal price = 50.00m,
        int quantity = 100,
        int maxPerOrder = 10,
        string? ticketName = null)
    {
        var marker = Guid.NewGuid().ToString("N");
        var startAt = DateTime.UtcNow.AddDays(30);

        var created = await adminClient.PostAsJsonAsync("/api/v1/admin/events", new
        {
            title = $"Order event {marker}",
            slug = $"order-event-{marker}",
            startAt,
            endAt = startAt.AddDays(1),
            isPublished = true,
            ticketingEnabled = true
        });
        created.EnsureSuccessStatusCode();
        var evt = await created.Content.ReadFromJsonAsync<EventShape>();
        evt.ShouldNotBeNull();

        var type = await adminClient.PostAsJsonAsync($"/api/v1/admin/events/{evt.Id}/ticket-types", new
        {
            name = ticketName ?? "Regular",
            description = (string?)null,
            price,
            quantity,
            maxPerOrder,
            salesStartAt = (DateTime?)null,
            salesEndAt = (DateTime?)null,
            isActive = true
        });
        type.EnsureSuccessStatusCode();
        var typeBody = await type.Content.ReadFromJsonAsync<TicketTypeShape>();
        typeBody.ShouldNotBeNull();

        return (evt.Id, evt.Slug, typeBody.Id);
    }

    public static async Task<CreateOrderShape> CreateOrderAsync(
        HttpClient client,
        Guid eventId,
        Guid ticketTypeId,
        int quantity = 2,
        string? buyerPhone = null)
    {
        var response = await client.PostAsJsonAsync("/api/v1/orders", new
        {
            eventId,
            buyerFullName = "Ama Mensah",
            buyerEmail = "ama@example.com",
            buyerPhone = buyerPhone ?? "233244000000",
            items = new[] { new { ticketTypeId, quantity } }
        });

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<CreateOrderShape>();
        body.ShouldNotBeNull();
        return body;
    }

    public static string CheckoutCallbackBody(string clientReference, decimal amount)
        => $$"""
            { "ResponseCode": "0000", "Status": "Success",
              "Data": { "CheckoutId": "checkout-1", "SalesInvoiceId": "invoice-1",
                "ClientReference": "{{clientReference}}", "Status": "Success", "Amount": {{amount}},
                "CustomerPhoneNumber": "233244000000",
                "PaymentDetails": { "MobileMoneyNumber": "233244000000", "PaymentType": "mobilemoney", "Channel": "mtn-gh" },
                "Description": "Tickets" } }
            """;

    public static string RefundCallbackBody(string transactionId, bool success)
        => $$"""
            { "ResponseCode": "{{(success ? "0000" : "4509")}}", "Status": "{{(success ? "Success" : "Failed")}}",
              "Data": { "TransactionId": "{{transactionId}}", "Status": "{{(success ? "Success" : "Failed")}}" } }
            """;

    public static async Task WaitUntilAsync(Func<Task<bool>> condition, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;

        while (DateTime.UtcNow < deadline)
        {
            if (await condition())
            {
                return;
            }

            await Task.Delay(200);
        }

        throw new TimeoutException("Condition was not met within the timeout.");
    }

    internal record EventShape(Guid Id, string Slug);

    internal record TicketTypeShape(Guid Id);

    internal record CreateOrderShape(
        string OrderNumber,
        Guid OrderToken,
        string Status,
        decimal TotalAmount,
        string Currency,
        string? CheckoutUrl,
        OrderItemShape[] Items,
        TicketShape[]? Tickets);

    internal record OrderItemShape(Guid TicketTypeId, string Name, int Quantity, decimal UnitPrice);

    internal record TicketShape(string Code, string TicketTypeName, string? HolderName, string Status, DateTime? CheckedInAt);
}
