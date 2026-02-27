using FluentValidation;
using Yallarhorn.Configuration;

namespace Yallarhorn.Configuration.Validation;

/// <summary>
/// Validator for DatabaseOptions.
/// </summary>
public class DatabaseOptionsValidator : AbstractValidator<DatabaseOptions>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DatabaseOptionsValidator"/> class.
    /// </summary>
    public DatabaseOptionsValidator()
    {
        RuleFor(x => x.Path)
            .NotEmpty()
            .WithMessage("Database path is required");

        RuleFor(x => x.PoolSize)
            .InclusiveBetween(1, 100)
            .WithMessage("PoolSize must be between 1 and 100");
    }
}