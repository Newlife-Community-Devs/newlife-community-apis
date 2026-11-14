namespace Newlife.Application.Orders;

public sealed class HubtelOptions
{
    public const string SectionName = "Hubtel";

    public string ApiId { get; set; } = string.Empty;

    public string ApiKey { get; set; } = string.Empty;

    public string MerchantAccountNumber { get; set; } = string.Empty;

    public string CreateCheckoutUrl { get; set; } = "https://payproxyapi.hubtel.com/items/initiate";

    public string StatusBaseUrl { get; set; } = "https://api-txnstatus.hubtel.com";

    public string RefundBaseUrl { get; set; } = "https://refund-api.hubtel.com";

    public bool Sandbox { get; set; }

    public string CallbackBaseUrl { get; set; } = string.Empty;

    public string CallbackToken { get; set; } = string.Empty;

    public bool EnforceCallbackIp { get; set; } = true;
}

public sealed class PaymentsTuningOptions
{
    public const string SectionName = "Payments";

    public int ReconciliationIntervalSeconds { get; set; } = 60;

    public int ReconciliationMinAgeSeconds { get; set; } = 300;

    public int ExpiryIntervalSeconds { get; set; } = 300;
}
