using FluentValidation;
using Newlife.Application.Users.DTOs;

namespace Newlife.Application.Users.Validators;

public sealed class CreateUserRequestValidator : AbstractValidator<CreateUserRequest>
{
    public CreateUserRequestValidator()
    {
        RuleFor(request => request.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("Email must be a valid address.")
            .MaximumLength(320);

        RuleFor(request => request.FullName)
            .NotEmpty().WithMessage("Full name is required.")
            .MaximumLength(120);

        RuleFor(request => request.Password)
            .NotEmpty().WithMessage("Password is required.")
            .MinimumLength(10).WithMessage("Password must be at least 10 characters.")
            .MaximumLength(200);
    }
}

public sealed class ResetPasswordRequestValidator : AbstractValidator<ResetPasswordRequest>
{
    public ResetPasswordRequestValidator()
    {
        RuleFor(request => request.NewPassword)
            .NotEmpty().WithMessage("New password is required.")
            .MinimumLength(10).WithMessage("Password must be at least 10 characters.")
            .MaximumLength(200);
    }
}
