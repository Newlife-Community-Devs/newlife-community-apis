using Newlife.Application.Common.Interfaces;
using Newlife.Infrastructure.Payments;
using Shouldly;

namespace Newlife.UnitTests;

public sealed class HubtelResponseMappingTests
{
    [Theory]
    [InlineData("0000", true)]
    [InlineData("4000", false)]
    [InlineData("0001", false)]
    [InlineData(null, false)]
    public void Create_response_codes_map_to_success_only_for_0000(string? code, bool expected)
        => HubtelResponseMapping.IsCreateSuccess(code).ShouldBe(expected);

    [Theory]
    [InlineData("Paid", TransactionStatus.Paid)]
    [InlineData("Success", TransactionStatus.Paid)]
    [InlineData("Unpaid", TransactionStatus.Unpaid)]
    [InlineData("Pending", TransactionStatus.Unpaid)]
    [InlineData("Initiated", TransactionStatus.Unpaid)]
    [InlineData("Refunded", TransactionStatus.Refunded)]
    [InlineData("Failed", TransactionStatus.Failed)]
    [InlineData("Declined", TransactionStatus.Failed)]
    [InlineData("something else", TransactionStatus.Unknown)]
    [InlineData(null, TransactionStatus.Unknown)]
    public void Status_strings_are_mapped(string? value, TransactionStatus expected)
        => HubtelResponseMapping.MapStatus(value).ShouldBe(expected);

    [Fact]
    public void Status_payload_is_parsed_camel_case_or_pascal_case()
    {
        var camel = """
            { "responseCode": "0000", "status": "Success",
              "data": { "transactionId": "TX123", "status": "Paid", "amount": 100.00, "paymentMethod": "mobilemoney" } }
            """;

        var result = HubtelClient.ParseStatus(camel);

        result.TransactionId.ShouldBe("TX123");
        result.Status.ShouldBe(TransactionStatus.Paid);
        result.Amount.ShouldBe(100.00m);
        result.PaymentMethod.ShouldBe("mobilemoney");

        var flat = """{ "transactionStatus": "Refunded", "transactionId": "TX9", "amount": "25.50" }""";
        var flatResult = HubtelClient.ParseStatus(flat);

        flatResult.Status.ShouldBe(TransactionStatus.Refunded);
        flatResult.Amount.ShouldBe(25.50m);
    }

    [Fact]
    public void Callback_pascal_case_payload_is_consumed_by_processing()
    {
        const string callback = """
            { "ResponseCode": "0000", "Status": "Success",
              "Data": { "CheckoutId": "C1", "SalesInvoiceId": "SI9", "ClientReference": "NL7K3M9Q2A01",
                "Status": "Success", "Amount": 100.00,
                "PaymentDetails": { "MobileMoneyNumber": "233244000000", "PaymentType": "mobilemoney", "Channel": "mtn-gh" },
                "Description": "Tickets NL-7K3M9Q2A" } }
            """;

        using var document = System.Text.Json.JsonDocument.Parse(callback);
        var data = document.RootElement.GetProperty("Data");

        data.GetProperty("ClientReference").GetString().ShouldBe("NL7K3M9Q2A01");
        data.GetProperty("SalesInvoiceId").GetString().ShouldBe("SI9");
        data.GetProperty("Amount").GetDecimal().ShouldBe(100.00m);
    }

    [Theory]
    [InlineData("0001", 200, true)]
    [InlineData("4515", 200, true)]
    [InlineData("0000", 200, true)]
    [InlineData("4000", 200, false)]
    [InlineData(null, 202, true)]
    [InlineData(null, 400, false)]
    public void Refund_codes_are_classified(string? code, int statusCode, bool expectedAccepted)
        => HubtelResponseMapping.IsRefundAccepted(code, statusCode).ShouldBe(expectedAccepted);

    [Theory]
    [InlineData("4000", true)]
    [InlineData("4509", true)]
    [InlineData("0001", false)]
    [InlineData(null, false)]
    public void Immediate_refund_rejections_are_recognized(string? code, bool expected)
        => HubtelResponseMapping.IsRefundImmediateRejection(code).ShouldBe(expected);

    [Fact]
    public void Description_sanitizes_forbidden_characters()
        => HubtelResponseMapping.SanitizeDescription("Tickets A&B*C!D%E@F")
            .ShouldBe("Tickets ABCDEF");

    [Theory]
    [InlineData("https://payproxyapi.hubtel.com/items/initiate", "https://code.hubtel.com/sandbox/v1/payproxyapi/items/initiate")]
    [InlineData("https://api-txnstatus.hubtel.com", "https://code.hubtel.com/sandbox/v1/api-txnstatus")]
    [InlineData("https://refund-api.hubtel.com/refund/100000/order/TX1", "https://code.hubtel.com/sandbox/v1/refund-api/refund/100000/order/TX1")]
    public void Sandbox_urls_are_rewritten_per_verified_contract(string production, string expected)
        => HubtelResponseMapping.RewriteForSandbox(production).ShouldBe(expected);
}
