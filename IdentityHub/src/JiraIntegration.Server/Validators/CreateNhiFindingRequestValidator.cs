using System.Text.RegularExpressions;
using FluentValidation;
using JiraIntegration.Server.Interfaces;
using JiraIntegration.Server.Models.Nhi;

namespace JiraIntegration.Server.Validators;

public sealed partial class CreateNhiFindingRequestValidator : AbstractValidator<CreateNhiFindingRequest>
{
    public const int MaxTitleLength = 255;
    public const int MaxDescriptionLength = 5000;

    public CreateNhiFindingRequestValidator(ICurrentUserAccessor currentUserAccessor)
    {
        RuleFor(x => x.Title)
            .NotEmpty()
            .MaximumLength(MaxTitleLength)
            .Must(NotContainScriptTags)
            .WithMessage("Title contains disallowed content.");

        RuleFor(x => x.Description)
            .NotEmpty()
            .MaximumLength(MaxDescriptionLength);

        When(_ => string.IsNullOrWhiteSpace(currentUserAccessor.GetScopedProjectKey()), () =>
        {
            RuleFor(x => x.ProjectKey).ValidProjectKey();
        });

        When(_ => !string.IsNullOrWhiteSpace(currentUserAccessor.GetScopedProjectKey()), () =>
        {
            RuleFor(x => x.ProjectKey).OptionalValidProjectKey();
        });
    }

    private static bool NotContainScriptTags(string? value) =>
        string.IsNullOrEmpty(value) || !MaliciousContentRegex().IsMatch(value);

    [GeneratedRegex(
        @"<script|javascript:|onerror\s*=|onload\s*=|<iframe|<object|<embed",
        RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex MaliciousContentRegex();
}
