namespace Yallarhorn.Data.Enums;

/// <summary>
/// Represents the type of feed output for a channel.
/// </summary>
public enum FeedType
{
    /// <summary>
    /// Audio-only feed (transcoded to MP3/M4A).
    /// </summary>
    Audio = 0,

    /// <summary>
    /// Video-only feed (transcoded to MP4).
    /// </summary>
    Video = 1,

    /// <summary>
    /// Both audio and video feeds.
    /// </summary>
    Both = 2
}