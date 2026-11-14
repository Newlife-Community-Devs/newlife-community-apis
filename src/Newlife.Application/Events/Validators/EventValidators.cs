using FluentValidation;
using Newlife.Application.Events.DTOs;

namespace Newlife.Application.Events.Validators;

public sealed class EventUpsertRequestValidator : AbstractValidator<EventUpsertRequest>
{
    public EventUpsertRequestValidator()
    {
        RuleFor(request => request.Title).NotEmpty().MaximumLength(200);
        RuleFor(request => request.Slug).MaximumLength(200);
        RuleFor(request => request.Description).MaximumLength(20000);
        RuleFor(request => request.Venue).MaximumLength(300);

        RuleFor(request => request.EndAt)
            .Must((req, endAt) => endAt == null || endAt >= req.StartAt)
            .WithMessage("endAt must be greater than or equal to startAt.");
    }
}

public sealed class TicketTypeUpsertRequestValidator : AbstractValidator<TicketTypeUpsertRequest>
{
    public TicketTypeUpsertRequestValidator()
    {
        RuleFor(request => request.Name).NotEmpty().MaximumLength(120);
        RuleFor(request => request.Description).MaximumLength(500);

        RuleFor(request => request.Price)
            .GreaterThanOrEqualTo(0);

        RuleFor(request => request.Quantity).InclusiveBetween(1, 10000);
        RuleFor(request => request.MaxPerOrder).InclusiveBetween(1, 50);

        RuleFor(request => request.MaxPerOrder)
            .LessThanOrEqualTo(request => request.Quantity)
            .WithMessage("maxPerOrder must be less than or equal to quantity.")
            .When(request => request.Quantity < 50);

        RuleFor(request => request.SalesEndAt)
            .Must((request, salesEndAt) => salesEndAt == null
                                          || request.SalesStartAt == null
                                          || salesEndAt > request.SalesStartAt)
            .WithMessage("salesEndAt must be after salesStartAt.");
    }
}
