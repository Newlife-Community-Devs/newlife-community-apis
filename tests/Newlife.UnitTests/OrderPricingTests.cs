using Newlife.Application.Common.Interfaces;
using Newlife.Application.Orders;
using Shouldly;

namespace Newlife.UnitTests;

public sealed class OrderPricingTests
{
    [Fact]
    public void Total_is_computed_from_unit_price_and_quantity()
    {
        var lines = new List<(decimal UnitPrice, int Quantity)>
        {
            (50.00m, 2),
            (100.00m, 1)
        };

        OrderPricing.ComputeTotal(lines).ShouldBe(200.00m);
    }

    [Fact]
    public void Free_orders_total_zero()
    {
        var lines = new List<(decimal UnitPrice, int Quantity)>
        {
            (0.00m, 4)
        };

        OrderPricing.IsFree(OrderPricing.ComputeTotal(lines)).ShouldBeTrue();
    }

    [Fact]
    public void Paid_order_total_never_zero_when_any_price_positive()
    {
        var lines = new List<(decimal UnitPrice, int Quantity)>
        {
            (0.00m, 4),
            (0.01m, 1)
        };

        OrderPricing.IsFree(OrderPricing.ComputeTotal(lines)).ShouldBeFalse();
    }

    [Fact]
    public void Client_reference_is_alphanumeric()
    {
        var reference = TicketCodeGenerator.BuildClientReference(TicketCodeGenerator.GenerateOrderNumber(), 1);

        reference.ShouldMatch("^[A-Za-z0-9]+$");
        reference.Length.ShouldBeLessThanOrEqualTo(32);
    }
}
