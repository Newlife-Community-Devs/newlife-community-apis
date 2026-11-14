using System.Security.Cryptography;

namespace Newlife.Application.Orders;

public static class OrderPricing
{
    public static decimal ComputeTotal(IReadOnlyList<(decimal UnitPrice, int Quantity)> lines)
        => lines.Sum(line => line.UnitPrice * line.Quantity);

    public static bool IsFree(decimal total) => total == 0m;
}

public static class TicketCodeGenerator
{
    private const string CrockfordAlphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";

    public static string GenerateOrderNumber() => $"NL-{Generate(8)}";

    public static string GenerateTicketCode() => $"NL-{Generate(10)}";

    public static string BuildClientReference(string orderNumber, int attemptNumber)
    {
        var suffix = orderNumber.StartsWith("NL-", StringComparison.Ordinal)
            ? orderNumber[3..]
            : orderNumber.Replace("-", string.Empty);
        return $"NL{suffix}{attemptNumber:00}";
    }

    private static string Generate(int length)
    {
        var bytes = RandomNumberGenerator.GetBytes(length);
        var chars = new char[length];

        for (var i = 0; i < length; i++)
        {
            chars[i] = CrockfordAlphabet[bytes[i] % CrockfordAlphabet.Length];
        }

        return new string(chars);
    }
}
