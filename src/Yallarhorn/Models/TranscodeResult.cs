namespace Yallarhorn.Models;

/// <summary>
/// Represents the result of an FFmpeg transcoding operation.
/// </summary>
public record TranscodeResult
{
    /// <summary>
    /// Gets a value indicating whether the transcoding was successful.
    /// </summary>
    public required bool Success { get; init; }

    /// <summary>
    /// Gets the exit code from the FFmpeg process.
    /// </summary>
    public int ExitCode { get; init; }

    /// <summary>
    /// Gets the duration of the transcoding operation.
    /// </summary>
    public TimeSpan Duration { get; init; }

    /// <summary>
    /// Gets the error output from FFmpeg (stderr).
    /// </summary>
    public string? ErrorOutput { get; init; }

    /// <summary>
    /// Gets the path to the output file.
    /// </summary>
    public string? OutputPath { get; init; }

    /// <summary>
    /// Gets the output file size in bytes, if available.
    /// </summary>
    public long? OutputFileSize { get; init; }
}

/// <summary>
/// Represents media information extracted from a file.
/// </summary>
public record MediaInfo
{
    /// <summary>
    /// Gets the duration of the media file.
    /// </summary>
    public required TimeSpan Duration { get; init; }

    /// <summary>
    /// Gets the video codec name, if video is present.
    /// </summary>
    public string? VideoCodec { get; init; }

    /// <summary>
    /// Gets the audio codec name, if audio is present.
    /// </summary>
    public string? AudioCodec { get; init; }

    /// <summary>
    /// Gets the video width in pixels, if video is present.
    /// </summary>
    public int? Width { get; init; }

    /// <summary>
    /// Gets the video height in pixels, if video is present.
    /// </summary>
    public int? Height { get; init; }

    /// <summary>
    /// Gets the audio sample rate in Hz, if audio is present.
    /// </summary>
    public int? AudioSampleRate { get; init; }

    /// <summary>
    /// Gets the audio channel count.
    /// </summary>
    public int? AudioChannels { get; init; }

    /// <summary>
    /// Gets the video bitrate in bits per second.
    /// </summary>
    public long? VideoBitrate { get; init; }

    /// <summary>
    /// Gets the audio bitrate in bits per second.
    /// </summary>
    public long? AudioBitrate { get; init; }

    /// <summary>
    /// Gets the frames per second.
    /// </summary>
    public double? FrameRate { get; init; }

    /// <summary>
    /// Gets the overall bitrate in bits per second.
    /// </summary>
    public long? OverallBitrate { get; init; }
}

/// <summary>
/// Settings for audio transcoding operations.
/// </summary>
public class AudioTranscodeSettings
{
    /// <summary>
    /// Gets or sets the output format (mp3, m4a, aac, ogg).
    /// </summary>
    public string Format { get; set; } = "mp3";

    /// <summary>
    /// Gets or sets the audio bitrate (e.g., "192k").
    /// </summary>
    public string Bitrate { get; set; } = "192k";

    /// <summary>
    /// Gets or sets the sample rate in Hz.
    /// </summary>
    public int SampleRate { get; set; } = 44100;

    /// <summary>
    /// Gets or sets the number of audio channels.
    /// </summary>
    public int Channels { get; set; } = 2;
}

/// <summary>
/// Settings for video transcoding operations.
/// </summary>
public class VideoTranscodeSettings
{
    /// <summary>
    /// Gets or sets the output format (mp4).
    /// </summary>
    public string Format { get; set; } = "mp4";

    /// <summary>
    /// Gets or sets the video codec (libx264).
    /// </summary>
    public string VideoCodec { get; set; } = "libx264";

    /// <summary>
    /// Gets or sets the encoding preset (ultrafast, fast, medium, slow).
    /// </summary>
    public string Preset { get; set; } = "medium";

    /// <summary>
    /// Gets or sets the CRF quality value (18-51, lower is better).
    /// </summary>
    public int Quality { get; set; } = 23;

    /// <summary>
    /// Gets or sets the audio bitrate.
    /// </summary>
    public string AudioBitrate { get; set; } = "192k";

    /// <summary>
    /// Gets or sets the audio sample rate in Hz.
    /// </summary>
    public int AudioSampleRate { get; set; } = 44100;

    /// <summary>
    /// Gets or sets the number of audio channels.
    /// </summary>
    public int AudioChannels { get; set; } = 2;
}

/// <summary>
/// Represents progress information during transcoding.
/// </summary>
public record TranscodeProgress
{
    /// <summary>
    /// Gets the current frame being processed.
    /// </summary>
    public long? Frame { get; init; }

    /// <summary>
    /// Gets the current time position in the media.
    /// </summary>
    public TimeSpan? Time { get; init; }

    /// <summary>
    /// Gets the current bitrate.
    /// </summary>
    public double? Bitrate { get; init; }

    /// <summary>
    /// Gets the processing speed (e.g., "15.2x").
    /// </summary>
    public double? Speed { get; init; }

    /// <summary>
    /// Gets the progress percentage (0-100).
    /// </summary>
    public double? Progress { get; init; }
}