using System.Text.RegularExpressions;
using FluentValidation;

namespace JiraIntegration.Server.Validators;

public static partial class SharedValidationRules
{
    public const int MaxProjectKeyLength = 32;

    public static IRuleBuilderOptions<T, string> ValidProjectKey<T>(this IRuleBuilder<T, string> ruleBuilder) =>
        ruleBuilder
            .NotEmpty()
            .MaximumLength(MaxProjectKeyLength)
            .Must(IsAlphanumericProjectKey)
            .WithMessage("ProjectKey must be alphanumeric.");

    public static IRuleBuilderOptions<T, string> OptionalValidProjectKey<T>(this IRuleBuilder<T, string> ruleBuilder) =>
        ruleBuilder
            .MaximumLength(MaxProjectKeyLength)
            .Must(value => string.IsNullOrWhiteSpace(value) || IsAlphanumericProjectKey(value))
            .WithMessage("ProjectKey must be alphanumeric.");

    private static bool IsAlphanumericProjectKey(string? value) =>
        !string.IsNullOrEmpty(value) && AlphanumericProjectKeyRegex().IsMatch(value);

    [GeneratedRegex(@"^[A-Za-z0-9]+$", RegexOptions.Compiled)]
    private static partial Regex AlphanumericProjectKeyRegex();
}
