using FluentValidation;
using Yallarhorn.Configuration;

namespace Yallarhorn.Configuration.Validation;

/// <summary>
/// Validator for TranscodeOptions.
/// </summary>
public class TranscodeOptionsValidator : AbstractValidator<TranscodeOptions>
{
    private static readonly string[] ValidAudioFormats = ["mp3", "aac", "ogg", "m4a"];
    private static readonly string[] ValidVideoFormats = ["mp4", "mkv", "webm"];
    private static readonly string[] ValidVideoCodecs = ["h264", "h265", "vp9", "av1"];

    /// <summary>
    /// Initializes a new instance of the <see cref="TranscodeOptionsValidator"/> class.
    /// </summary>
    public TranscodeOptionsValidator()
    {
        RuleFor(x => x.AudioFormat)
            .Must(f => ValidAudioFormats.Contains(f.ToLowerInvariant()))
            .WithMessage($"AudioFormat must be one of: {string.Join(", ", ValidAudioFormats)}");

        RuleFor(x => x.AudioBitrate)
            .NotEmpty()
            .Matches(@"^\d+[kKmM]$")
            .WithMessage("AudioBitrate must be in format like '192k' or '128M'");

        RuleFor(x => x.AudioSampleRate)
            .InclusiveBetween(8000, 192000)
            .WithMessage("AudioSampleRate must be between 8000 and 192000 Hz");

        RuleFor(x => x.VideoFormat)
            .Must(f => ValidVideoFormats.Contains(f.ToLowerInvariant()))
            .WithMessage($"VideoFormat must be one of: {string.Join(", ", ValidVideoFormats)}");

        RuleFor(x => x.VideoCodec)
            .Must(c => ValidVideoCodecs.Contains(c.ToLowerInvariant()))
            .WithMessage($"VideoCodec must be one of: {string.Join(", ", ValidVideoCodecs)}");

        RuleFor(x => x.VideoQuality)
            .InclusiveBetween(18, 51)
            .WithMessage("VideoQuality (CRF) must be between 18 and 51");

        RuleFor(x => x.Threads)
            .InclusiveBetween(1, 64)
            .WithMessage("Threads must be between 1 and 64");
    }
}