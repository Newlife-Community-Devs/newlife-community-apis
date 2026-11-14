using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using Newlife.Application.Common.Interfaces;
using Newlife.Application.Orders;

namespace Newlife.Infrastructure.Payments;

public sealed class HubtelClient(HttpClient httpClient, IOptions<HubtelOptions> options) : IPaymentGateway
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly HubtelOptions _options = options.Value;

    public async Task<CheckoutResult> CreateCheckoutAsync(
        CheckoutRequest request,
        CancellationToken cancellationToken)
    {
        var url = _options.Sandbox
            ? HubtelResponseMapping.RewriteForSandbox(_options.CreateCheckoutUrl)
            : _options.CreateCheckoutUrl;

        var body = new
        {
            totalAmount = Math.Round(request.Amount, 2, MidpointRounding.AwayFromZero),
            description = HubtelResponseMapping.SanitizeDescription(request.Description),
            callbackUrl = request.CallbackUrl,
            returnUrl = request.ReturnUrl,
            cancellationUrl = request.CancellationUrl,
            merchantAccountNumber = _options.MerchantAccountNumber,
            clientReference = request.ClientReference,
            payeeName = request.PayeeName,
            payeeMobileNumber = request.PayeeMobileNumber,
            payeeEmail = request.PayeeEmail
        };

        using var message = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(JsonSerializer.Serialize(body, JsonOptions), Encoding.UTF8, "application/json")
        };

        ApplyAuth(message);

        using var response = await httpClient.SendAsync(message, cancellationToken);
        var payload = await response.Content.ReadAsStringAsync(cancellationToken);

        using var document = JsonDocument.Parse(payload);
        var root = document.RootElement;

        var responseCode = TryGetString(root, "responseCode") ?? TryGetString(root, "ResponseCode");

        if (!HubtelResponseMapping.IsCreateSuccess(responseCode))
        {
            var status = TryGetString(root, "status") ?? TryGetString(root, "message") ?? "unknown";
            throw new HttpRequestException($"Hubtel checkout creation failed with code {responseCode}: {status}");
        }

        var data = root.TryGetProperty("data", out var dataElement) ? dataElement : default;

        return new CheckoutResult(
            TryGetString(data, "checkoutId") ?? string.Empty,
            TryGetString(data, "checkoutUrl") ?? string.Empty,
            TryGetString(data, "clientReference") ?? request.ClientReference);
    }

    public async Task<TransactionStatusResult> GetTransactionStatusAsync(
        string clientReference,
        CancellationToken cancellationToken)
    {
        var baseHost = _options.Sandbox
            ? HubtelResponseMapping.RewriteForSandbox(_options.StatusBaseUrl)
            : _options.StatusBaseUrl;

        var url = $"{baseHost.TrimEnd('/')}/transactions/{Uri.EscapeDataString(_options.MerchantAccountNumber)}" +
                  $"/status?clientReference={Uri.EscapeDataString(clientReference)}";

        using var message = new HttpRequestMessage(HttpMethod.Get, url);
        ApplyAuth(message);

        using var response = await httpClient.SendAsync(message, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"Hubtel status check returned HTTP {(int)response.StatusCode}.");
        }

        var payload = await response.Content.ReadAsStringAsync(cancellationToken);

        return ParseStatus(payload);
    }

    public async Task<RefundResult> RefundAsync(
        string transactionId,
        string callbackUrl,
        CancellationToken cancellationToken)
    {
        var baseHost = _options.Sandbox
            ? HubtelResponseMapping.RewriteForSandbox(_options.RefundBaseUrl)
            : _options.RefundBaseUrl;

        var url = $"{baseHost.TrimEnd('/')}/refund/{Uri.EscapeDataString(_options.MerchantAccountNumber)}" +
                  $"/order/{Uri.EscapeDataString(transactionId)}";

        using var message = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(new { callbackUrl }, JsonOptions),
                Encoding.UTF8,
                "application/json")
        };

        ApplyAuth(message);

        using var response = await httpClient.SendAsync(message, cancellationToken);
        var payload = await response.Content.ReadAsStringAsync(cancellationToken);

        string? responseCode = null;
        string? resultMessage = null;
        string? refundId = null;

        try
        {
            using var document = JsonDocument.Parse(payload);
            var root = document.RootElement;
            responseCode = TryGetString(root, "responseCode") ?? TryGetString(root, "ResponseCode") ??
                           TryGetString(root, "code");
            resultMessage = TryGetString(root, "message") ?? TryGetString(root, "status");
            refundId = root.TryGetProperty("data", out var data)
                ? TryGetString(data, "id") ?? TryGetString(data, "Id")
                : null;
        }
        catch (JsonException)
        {
        }

        var accepted = HubtelResponseMapping.IsRefundAccepted(responseCode, (int)response.StatusCode);

        return new RefundResult(
            accepted,
            responseCode ?? ((int)response.StatusCode).ToString(),
            resultMessage ?? (accepted ? "Refund request accepted." : "Refund rejected."),
            refundId);
    }

    public static TransactionStatusResult ParseStatus(string payload)
    {
        using var document = JsonDocument.Parse(payload);
        var root = document.RootElement;

        var data = root.TryGetProperty("data", out var dataElement)
            ? dataElement
            : root.TryGetProperty("Data", out var dataPascal)
                ? dataPascal
                : root;

        var statusText = TryGetString(data, "status")
                         ?? TryGetString(data, "transactionStatus")
                         ?? TryGetString(root, "status");

        var transactionId = TryGetString(data, "transactionId")
                            ?? TryGetString(data, "TransactionId")
                            ?? TryGetString(data, "id");

        decimal amount = -1;
        if (TryGetDecimal(data, "amount", out var parsedAmount))
        {
            amount = parsedAmount;
        }
        else if (TryGetDecimal(root, "amount", out var rootAmount))
        {
            amount = rootAmount;
        }

        var paymentMethod = TryGetString(data, "paymentMethod") ?? TryGetString(data, "channel");

        return new TransactionStatusResult(transactionId, HubtelResponseMapping.MapStatus(statusText), amount, paymentMethod);
    }

    private void ApplyAuth(HttpRequestMessage message)
    {
        var credentials = Convert.ToBase64String(
            Encoding.UTF8.GetBytes($"{_options.ApiId}:{_options.ApiKey}"));
        message.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);
    }

    private static string? TryGetString(JsonElement element, string name)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    private static bool TryGetDecimal(JsonElement element, string name, out decimal value)
    {
        value = 0;

        if (element.ValueKind != JsonValueKind.Object ||
            !element.TryGetProperty(name, out var property))
        {
            return false;
        }

        if (property.ValueKind == JsonValueKind.Number)
        {
            value = property.GetDecimal();
            return true;
        }

        return property.ValueKind == JsonValueKind.String && decimal.TryParse(property.GetString(), out value);
    }
}
