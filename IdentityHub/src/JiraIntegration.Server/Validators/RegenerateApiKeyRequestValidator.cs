using FluentValidation;
using JiraIntegration.Server.Models.ApiKeys;

namespace JiraIntegration.Server.Validators;

public sealed class RegenerateApiKeyRequestValidator : AbstractValidator<RegenerateApiKeyRequest>
{
    public RegenerateApiKeyRequestValidator()
    {
        RuleFor(x => x.ProjectKey).ValidProjectKey();
    }
}
