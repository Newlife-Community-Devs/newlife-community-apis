using FluentValidation;
using Newlife.Application.Orders.DTOs;

namespace Newlife.Application.Orders.Validators;

public sealed class CreateOrderRequestValidator : AbstractValidator<CreateOrderRequest>
{
    public CreateOrderRequestValidator()
    {
        RuleFor(request => request.EventId).NotEmpty();

        RuleFor(request => request.BuyerFullName).NotEmpty().MaximumLength(200);

        RuleFor(request => request.BuyerEmail)
            .NotEmpty()
            .EmailAddress()
            .MaximumLength(320);

        RuleFor(request => request.BuyerPhone)
            .Matches("^233[0-9]{9}$")
            .WithMessage("buyerPhone must be a Ghanaian mobile number in international format without +.");

        RuleFor(request => request.Items)
            .NotEmpty()
            .WithMessage("At least one ticket type is required.")
            .Must(items => items.Count <= 5)
            .WithMessage("A maximum of 5 distinct ticket types per order is allowed.");

        RuleForEach(request => request.Items).ChildRules(item =>
        {
            item.RuleFor(selection => selection.Quantity).GreaterThanOrEqualTo(1);
        });

        RuleFor(request => request.Items)
            .Must(items => items
                .GroupBy(selection => selection.TicketTypeId)
                .All(group => group.Count() == 1))
            .WithMessage("Ticket types must be distinct within an order.");
    }
}

public sealed class CheckInRequestValidator : AbstractValidator<CheckInRequest>
{
    public CheckInRequestValidator()
    {
        RuleFor(request => request.Code).NotEmpty().MaximumLength(20);
    }
}
