using FluentValidation;
using JiraIntegration.Server.Models.Auth;

namespace JiraIntegration.Server.Validators;

public sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public const int MaxUsernameLength = 128;
    public const int MaxPasswordLength = 128;

    public LoginRequestValidator()
    {
        RuleFor(x => x.Username)
            .NotEmpty()
            .MaximumLength(MaxUsernameLength);

        RuleFor(x => x.Password)
            .NotEmpty()
            .MaximumLength(MaxPasswordLength);
    }
}
