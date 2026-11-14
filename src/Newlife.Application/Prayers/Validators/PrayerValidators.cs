using FluentValidation;
using Newlife.Application.Prayers.DTOs;

namespace Newlife.Application.Prayers.Validators;

public sealed class SubmitPrayerRequestValidator : AbstractValidator<SubmitPrayerRequest>
{
    public SubmitPrayerRequestValidator()
    {
        RuleFor(request => request.Content)
            .NotEmpty()
            .Length(5, 4000);

        RuleFor(request => request.Title)
            .MaximumLength(200);

        RuleFor(request => request.SubmitterName)
            .NotEmpty()
            .MaximumLength(120)
            .When(request => !request.IsAnonymous);

        RuleFor(request => request.SubmitterName)
            .MaximumLength(120);

        RuleFor(request => request.PhoneNumber)
            .Matches("^233[0-9]{9}$")
            .When(request => !string.IsNullOrWhiteSpace(request.PhoneNumber));
    }
}

public sealed class PrayerWallQueryValidator : AbstractValidator<PrayerWallQuery>
{
    public PrayerWallQueryValidator()
    {
        RuleFor(query => query.Sort)
            .Must(sort => sort is "createdAt" or "answeredAt")
            .WithMessage("sort must be one of: createdAt, answeredAt.");

        RuleFor(query => query.Order)
            .Must(order => order is "asc" or "desc")
            .WithMessage("order must be one of: asc, desc.");
    }
}

public sealed class PrayerAdminQueryValidator : AbstractValidator<PrayerAdminQuery>
{
    public PrayerAdminQueryValidator()
    {
        RuleFor(query => query.Status)
            .Must(status => status is "all" or "open" or "answered" or "flagged")
            .WithMessage("status must be one of: all, open, answered, flagged.");
    }
}

public sealed class AnswerPrayerRequestValidator : AbstractValidator<AnswerPrayerRequest>
{
    public AnswerPrayerRequestValidator()
    {
        RuleFor(request => request.Note).MaximumLength(1000);
    }
}

public sealed class FlagPrayerRequestValidator : AbstractValidator<FlagPrayerRequest>
{
    public FlagPrayerRequestValidator()
    {
        RuleFor(request => request.Reason).MaximumLength(500);
    }
}
