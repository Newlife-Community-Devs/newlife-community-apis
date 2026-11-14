using FluentValidation;
using Newlife.Application.Announcements.DTOs;

namespace Newlife.Application.Announcements.Validators;

public sealed class AnnouncementUpsertRequestValidator : AbstractValidator<AnnouncementUpsertRequest>
{
    public AnnouncementUpsertRequestValidator()
    {
        RuleFor(request => request.Title).NotEmpty().MaximumLength(200);

        RuleFor(request => request.Body).NotEmpty().MaximumLength(20000);

        RuleFor(request => request)
            .Must(request => request.PublishAt == null
                             || request.ExpiresAt == null
                             || request.ExpiresAt > request.PublishAt)
            .WithMessage("expiresAt must be after publishAt.")
            .OverridePropertyName(nameof(AnnouncementUpsertRequest.ExpiresAt));
    }
}
