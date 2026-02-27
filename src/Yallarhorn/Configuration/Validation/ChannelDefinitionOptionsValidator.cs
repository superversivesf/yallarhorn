using System.Text.RegularExpressions;
using FluentValidation;
using Yallarhorn.Configuration;

namespace Yallarhorn.Configuration.Validation;

/// <summary>
/// Validator for ChannelDefinitionOptions.
/// </summary>
public class ChannelDefinitionOptionsValidator : AbstractValidator<ChannelDefinitionOptions>
{
    private static readonly Regex YouTubeChannelUrlPattern = new(
        @"^https://www\.youtube\.com/(@[a-zA-Z0-9_-]+|c/[a-zA-Z0-9_-]+|channel/[a-zA-Z0-9_-]+|user/[a-zA-Z0-9_-]+)$",
        RegexOptions.Compiled);

    private static readonly string[] ValidFeedTypes = ["audio", "video", "both"];

    /// <summary>
    /// Initializes a new instance of the <see cref="ChannelDefinitionOptionsValidator"/> class.
    /// </summary>
    public ChannelDefinitionOptionsValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .WithMessage("Channel name is required");

        RuleFor(x => x.Url)
            .NotEmpty()
            .WithMessage("Channel URL is required")
            .Must(BeAValidYouTubeChannelUrl)
            .WithMessage("URL must be a valid YouTube channel URL (e.g., https://www.youtube.com/@channelname)");

        RuleFor(x => x.EpisodeCount)
            .InclusiveBetween(1, 1000)
            .WithMessage("EpisodeCount must be between 1 and 1000");

        RuleFor(x => x.FeedType)
            .Must(ft => ValidFeedTypes.Contains(ft.ToLowerInvariant()))
            .WithMessage($"FeedType must be one of: {string.Join(", ", ValidFeedTypes)}");

        RuleFor(x => x.CustomSettings)
            .SetValidator(new TranscodeOptionsValidator()!)
            .When(x => x.CustomSettings != null);
    }

    private static bool BeAValidYouTubeChannelUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return false;
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return false;
        }

        if (uri.Scheme != Uri.UriSchemeHttps)
        {
            return false;
        }

        if (uri.Host != "www.youtube.com")
        {
            return false;
        }

        return YouTubeChannelUrlPattern.IsMatch(url);
    }
}