using FluentValidation;
using Yallarhorn.Configuration;

namespace Yallarhorn.Configuration.Validation;

/// <summary>
/// Validator for YallarhornOptions.
/// </summary>
public class YallarhornOptionsValidator : AbstractValidator<YallarhornOptions>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="YallarhornOptionsValidator"/> class.
    /// </summary>
    public YallarhornOptionsValidator()
    {
        RuleFor(x => x.Version)
            .NotEmpty()
            .WithMessage("Version is required");

        RuleFor(x => x.PollInterval)
            .GreaterThanOrEqualTo(300)
            .WithMessage("PollInterval must be at least 300 seconds (5 minutes)");

        RuleFor(x => x.MaxConcurrentDownloads)
            .InclusiveBetween(1, 10)
            .WithMessage("MaxConcurrentDownloads must be between 1 and 10");

        RuleFor(x => x.DownloadDir)
            .NotEmpty()
            .WithMessage("DownloadDir is required");

        RuleFor(x => x.TempDir)
            .NotEmpty()
            .WithMessage("TempDir is required");

        RuleFor(x => x.Channels)
            .NotEmpty()
            .WithMessage("At least one channel must be defined")
            .ForEach(channel => channel.SetValidator(new ChannelDefinitionOptionsValidator()));

        RuleFor(x => x.Server)
            .SetValidator(new ServerOptionsValidator());

        RuleFor(x => x.TranscodeSettings)
            .SetValidator(new TranscodeOptionsValidator());

        RuleFor(x => x.Database)
            .SetValidator(new DatabaseOptionsValidator());

        RuleFor(x => x.Auth)
            .SetValidator(new AuthOptionsValidator());
    }

    /// <summary>
    /// Validates options and throws if invalid.
    /// </summary>
    /// <param name="options">The options to validate.</param>
    /// <exception cref="ValidationException">Thrown when validation fails.</exception>
    public void ValidateAndThrow(YallarhornOptions options)
    {
        var result = Validate(options);
        if (!result.IsValid)
        {
            throw new ValidationException(result.Errors);
        }
    }
}