using Newlife.Application.Common.Interfaces;

namespace Newlife.Infrastructure.Payments;

public static class HubtelResponseMapping
{
    public static bool IsCreateSuccess(string? responseCode) => responseCode == "0000";

    public static TransactionStatus MapStatus(string? value)
        => value?.Trim().ToLowerInvariant() switch
        {
            "paid" or "success" or "complete" or "completed" => TransactionStatus.Paid,
            "refunded" or "refund" => TransactionStatus.Refunded,
            "failed" or "declined" or "cancelled" or "canceled" or "error" => TransactionStatus.Failed,
            "unpaid" or "pending" or "initiated" or "processing" or "created" => TransactionStatus.Unpaid,
            _ => TransactionStatus.Unknown
        };

    public static bool IsRefundAccepted(string? responseCode, int statusCode)
        => statusCode == 202 || responseCode is "0000" or "0001" or "4515";

    public static bool IsRefundImmediateRejection(string? responseCode)
        => responseCode is "4000" or "4509";

    public static string SanitizeDescription(string description)
        => string.Concat(description.Where(character => !"&*!%@".Contains(character)));

    public static string RewriteForSandbox(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var parsed))
        {
            return url;
        }

        var head = parsed.Host.Split('.')[0];
        var path = parsed.AbsolutePath == "/" ? string.Empty : parsed.AbsolutePath;
        return $"https://code.hubtel.com/sandbox/v1/{head}{path}{parsed.Query}";
    }
}
