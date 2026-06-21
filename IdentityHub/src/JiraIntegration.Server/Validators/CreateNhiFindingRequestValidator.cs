using System.Text.RegularExpressions;
using FluentValidation;
using JiraIntegration.Server.Models.Nhi;

namespace JiraIntegration.Server.Validators;

public sealed partial class CreateNhiFindingRequestValidator : AbstractValidator<CreateNhiFindingRequest>
{
    public const int MaxTitleLength = 255;
    public const int MaxDescriptionLength = 5000;

    public CreateNhiFindingRequestValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty()
            .MaximumLength(MaxTitleLength)
            .Must(NotContainScriptTags)
            .WithMessage("Title contains disallowed content.");

        RuleFor(x => x.Description)
            .NotEmpty()
            .MaximumLength(MaxDescriptionLength);

        RuleFor(x => x.ProjectKey)
            .NotEmpty()
            .MaximumLength(32)
            .Must(IsAlphanumeric)
            .WithMessage("ProjectKey must be alphanumeric.");
    }

    private static bool NotContainScriptTags(string? value) =>
        string.IsNullOrEmpty(value) || !MaliciousContentRegex().IsMatch(value);

    private static bool IsAlphanumeric(string? value) =>
        !string.IsNullOrEmpty(value) && AlphanumericRegex().IsMatch(value);

    [GeneratedRegex(
        @"<script|javascript:|onerror\s*=|onload\s*=|<iframe|<object|<embed",
        RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex MaliciousContentRegex();

    [GeneratedRegex(@"^[A-Za-z0-9]+$", RegexOptions.Compiled)]
    private static partial Regex AlphanumericRegex();
}
