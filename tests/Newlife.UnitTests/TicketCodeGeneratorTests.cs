using System.Text.RegularExpressions;
using Newlife.Application.Orders;
using Shouldly;

namespace Newlife.UnitTests;

public sealed class TicketCodeGeneratorTests
{
    private static readonly Regex Crockford = new("^NL-[0-9A-HJKMNP-TV-Z]{10}$");

    [Fact]
    public void Ticket_codes_use_crockford_alphabet_and_length()
    {
        for (var i = 0; i < 200; i++)
        {
            var code = TicketCodeGenerator.GenerateTicketCode();
            Regex.IsMatch(code, Crockford.ToString()).ShouldBeTrue();
            var suffix = code[3..];
            suffix.ShouldNotContain("I");
            suffix.ShouldNotContain("L");
            suffix.ShouldNotContain("O");
            suffix.ShouldNotContain("U");
        }
    }

    [Fact]
    public void Order_numbers_have_eight_characters()
    {
        var number = TicketCodeGenerator.GenerateOrderNumber();

        number.ShouldMatch(@"^NL-[0-9A-HJKMNP-TV-Z]{8}$");
    }

    [Fact]
    public void Generates_ten_thousand_unique_codes()
    {
        var codes = Enumerable.Range(0, 10_000)
            .Select(_ => TicketCodeGenerator.GenerateTicketCode())
            .ToHashSet();

        codes.Count.ShouldBe(10_000);
    }

    [Theory]
    [InlineData("NL-7K3M9Q2A", 1, "NL7K3M9Q2A01")]
    [InlineData("NL-7K3M9Q2A", 2, "NL7K3M9Q2A02")]
    [InlineData("NL-7K3M9Q2A", 3, "NL7K3M9Q2A03")]
    public void Client_reference_is_unique_per_attempt_within_32_chars(string orderNumber, int attempt, string expected)
    {
        var reference = TicketCodeGenerator.BuildClientReference(orderNumber, attempt);

        reference.ShouldBe(expected);
        reference.Length.ShouldBeLessThanOrEqualTo(32);
        reference.ShouldMatch("^[A-Za-z0-9]+$");
    }
}
