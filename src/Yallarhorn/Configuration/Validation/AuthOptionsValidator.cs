using FluentValidation;
using Yallarhorn.Configuration;

namespace Yallarhorn.Configuration.Validation;

/// <summary>
/// Validator for AuthOptions.
/// </summary>
public class AuthOptionsValidator : AbstractValidator<AuthOptions>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AuthOptionsValidator"/> class.
    /// </summary>
    public AuthOptionsValidator()
    {
        RuleFor(x => x.FeedCredentials)
            .SetValidator(new FeedCredentialsValidator()!);

        RuleFor(x => x.AdminAuth)
            .SetValidator(new AdminAuthValidator()!);
    }
}

/// <summary>
/// Validator for FeedCredentials.
/// </summary>
public class FeedCredentialsValidator : AbstractValidator<FeedCredentials>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="FeedCredentialsValidator"/> class.
    /// </summary>
    public FeedCredentialsValidator()
    {
        RuleFor(x => x.Username)
            .NotEmpty()
            .When(x => x.Enabled)
            .WithMessage("Username is required when feed credentials are enabled");

        RuleFor(x => x.Password)
            .NotEmpty()
            .When(x => x.Enabled)
            .WithMessage("Password is required when feed credentials are enabled");

        RuleFor(x => x.Realm)
            .NotEmpty()
            .WithMessage("Realm is required");
    }
}

/// <summary>
/// Validator for AdminAuth.
/// </summary>
public class AdminAuthValidator : AbstractValidator<AdminAuth>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AdminAuthValidator"/> class.
    /// </summary>
    public AdminAuthValidator()
    {
        RuleFor(x => x.Username)
            .NotEmpty()
            .When(x => x.Enabled)
            .WithMessage("Username is required when admin auth is enabled");

        RuleFor(x => x.Password)
            .NotEmpty()
            .When(x => x.Enabled)
            .WithMessage("Password is required when admin auth is enabled");
    }
}