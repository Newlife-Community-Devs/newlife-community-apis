using FluentValidation;
using Newlife.Application.Media.DTOs;

namespace Newlife.Application.Media.Validators;

public sealed class CreateUploadRequestValidator : AbstractValidator<CreateUploadRequest>
{
    public CreateUploadRequestValidator()
    {
        RuleFor(request => request.FileName).NotEmpty().MaximumLength(255);

        RuleFor(request => request.ContentType)
            .NotEmpty()
            .Must(contentType => contentType is "image/jpeg" or "image/png" or "image/webp")
            .WithMessage("Only image/jpeg, image/png and image/webp uploads are allowed.");
    }
}

public sealed class RegisterMediaRequestValidator : AbstractValidator<RegisterMediaRequest>
{
    public RegisterMediaRequestValidator()
    {
        RuleFor(request => request.Key).NotEmpty().MaximumLength(300);

        RuleFor(request => request.FileName).NotEmpty().MaximumLength(255);

        RuleFor(request => request.ContentType)
            .NotEmpty()
            .Must(contentType => contentType is "image/jpeg" or "image/png" or "image/webp")
            .WithMessage("Only image/jpeg, image/png and image/webp uploads are allowed.");

        RuleFor(request => request.SizeBytes)
            .GreaterThan(0)
            .When(request => request.SizeBytes.HasValue);

        RuleFor(request => request.Title).MaximumLength(200);
        RuleFor(request => request.AltText).MaximumLength(300);
    }
}
