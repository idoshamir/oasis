using FluentValidation;
using JiraIntegration.Server.Models.ApiKeys;

namespace JiraIntegration.Server.Validators;

public sealed class GenerateApiKeyRequestValidator : AbstractValidator<GenerateApiKeyRequest>
{
    public GenerateApiKeyRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(256);

        RuleFor(x => x.ProjectKey)
            .NotEmpty()
            .MaximumLength(32);
    }
}
