using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace Yallarhorn.Configuration;

/// <summary>
/// Configuration options for transcoding media files.
/// </summary>
public class TranscodeOptions
{
    /// <summary>
    /// The section name in configuration.
    /// </summary>
    public const string SectionName = "TranscodeSettings";

    /// <summary>
    /// Gets or sets the output audio format.
    /// Valid values: mp3, aac, ogg, m4a.
    /// </summary>
    [AllowedValues("mp3", "aac", "ogg", "m4a")]
    public string AudioFormat { get; set; } = "mp3";

    /// <summary>
    /// Gets or sets the audio bitrate for encoding.
    /// Format: number followed by k/K/m/M (e.g., "192k", "128K").
    /// </summary>
    [Required(ErrorMessage = "Audio bitrate is required")]
    [RegularExpression(@"^\d+[kKmM]$", ErrorMessage = "Audio bitrate must be in format like '192k' or '128M'")]
    public string AudioBitrate { get; set; } = "192k";

    /// <summary>
    /// Gets or sets the audio sample rate in Hz.
    /// Typical values: 22050, 44100, 48000.
    /// </summary>
    [Range(8000, 192000, ErrorMessage = "Audio sample rate must be between 8000 and 192000 Hz")]
    public int AudioSampleRate { get; set; } = 44100;

    /// <summary>
    /// Gets or sets the output video container format.
    /// </summary>
    [AllowedValues("mp4", "mkv", "webm")]
    public string VideoFormat { get; set; } = "mp4";

    /// <summary>
    /// Gets or sets the video encoding codec.
    /// </summary>
    [AllowedValues("h264", "h265", "vp9", "av1")]
    public string VideoCodec { get; set; } = "h264";

    /// <summary>
    /// Gets or sets the CRF quality value for video encoding.
    /// Lower values = better quality. Valid range: 18-51. Recommended: 18-28.
    /// </summary>
    [Range(18, 51, ErrorMessage = "Video quality (CRF) must be between 18 and 51")]
    public int VideoQuality { get; set; } = 23;

    /// <summary>
    /// Gets or sets the number of FFmpeg encoding threads.
    /// </summary>
    [Range(1, 64, ErrorMessage = "Threads must be between 1 and 64")]
    public int Threads { get; set; } = 4;

    /// <summary>
    /// Gets or sets a value indicating whether to keep the original file after transcoding.
    /// </summary>
    public bool KeepOriginal { get; set; } = false;

    /// <summary>
    /// Validates the options and returns validation results.
    /// </summary>
    /// <returns>A collection of validation results, empty if valid.</returns>
    public IEnumerable<ValidationResult> Validate()
    {
        var results = new List<ValidationResult>();
        var context = new ValidationContext(this);
        Validator.TryValidateObject(this, context, results, true);
        return results;
    }

    /// <summary>
    /// Validates the options and throws if invalid.
    /// </summary>
    public void ValidateAndThrow()
    {
        var results = Validate().ToList();
        if (results.Count != 0)
        {
            throw new ValidationException(
                $"Transcode options validation failed: {string.Join(", ", results.Select(r => r.ErrorMessage))}");
        }
    }
}