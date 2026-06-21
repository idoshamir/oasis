using System.Text.RegularExpressions;
using FluentValidation;
using JiraIntegration.Server.Models.Tickets;

namespace JiraIntegration.Server.Validators;

public sealed partial class CreateUiTicketRequestValidator : AbstractValidator<CreateUiTicketRequest>
{
    public const int MaxTitleLength = 255;
    public const int MaxDescriptionLength = 5000;

    public CreateUiTicketRequestValidator()
    {
        RuleFor(x => x.ProjectKey)
            .NotEmpty()
            .MaximumLength(32);

        RuleFor(x => x.Title)
            .NotEmpty()
            .MaximumLength(MaxTitleLength)
            .Must(NotContainScriptTags)
            .WithMessage("Title contains disallowed content.");

        RuleFor(x => x.Description)
            .MaximumLength(MaxDescriptionLength);
    }

    private static bool NotContainScriptTags(string? value) =>
        string.IsNullOrEmpty(value) || !MaliciousContentRegex().IsMatch(value);

    [GeneratedRegex(
        @"<script|javascript:|onerror\s*=|onload\s*=|<iframe|<object|<embed",
        RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex MaliciousContentRegex();
}
