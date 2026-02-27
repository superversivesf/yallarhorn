using FluentValidation;
using Yallarhorn.Configuration;

namespace Yallarhorn.Configuration.Validation;

/// <summary>
/// Validator for ServerOptions.
/// </summary>
public class ServerOptionsValidator : AbstractValidator<ServerOptions>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ServerOptionsValidator"/> class.
    /// </summary>
    public ServerOptionsValidator()
    {
        RuleFor(x => x.Host)
            .NotEmpty()
            .WithMessage("Host is required");

        RuleFor(x => x.Port)
            .InclusiveBetween(1, 65535)
            .WithMessage("Port must be between 1 and 65535");

        RuleFor(x => x.BaseUrl)
            .NotEmpty()
            .Must(BeAValidUrl)
            .WithMessage("BaseUrl must be a valid URL");

        RuleFor(x => x.FeedPath)
            .NotEmpty()
            .Must(path => path.StartsWith('/'))
            .WithMessage("FeedPath must start with '/'");

        RuleFor(x => x.MaxConcurrentConnections)
            .GreaterThanOrEqualTo(1)
            .WithMessage("MaxConcurrentConnections must be at least 1");

        RuleFor(x => x.RequestTimeoutSeconds)
            .GreaterThanOrEqualTo(1)
            .WithMessage("RequestTimeoutSeconds must be at least 1");
    }

    private static bool BeAValidUrl(string url)
    {
        return Uri.TryCreate(url, UriKind.Absolute, out var result) 
               && (result.Scheme == Uri.UriSchemeHttp || result.Scheme == Uri.UriSchemeHttps);
    }
}