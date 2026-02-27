# Download & Transcoding Pipeline

This document describes the download and transcoding workflow for Yallarhorn, a podcast server that downloads YouTube videos and serves them as RSS/Atom feeds.

## Table of Contents

- [Overview](#overview)
- [yt-dlp Integration](#yt-dlp-integration)
- [Download Workflow](#download-workflow)
- [Concurrent Downloads](#concurrent-downloads)
- [FFmpeg Transcoding](#ffmpeg-transcoding)
- [Error Handling](#error-handling)
- [File Management](#file-management)
- [Pipeline Architecture](#pipeline-architecture)

---

## Overview

Yallarhorn's download pipeline transforms YouTube videos into podcast-ready audio and video files through a multi-stage process:

```
┌──────────────────┐     ┌──────────────────┐     ┌──────────────────┐
│  Channel Refresh │────>│  Episode Discovery   │────>│  Queue Episodes  │
└──────────────────┘     └──────────────────┘     └──────────────────┘
                                                           │
                                                           ▼
┌──────────────────┐     ┌──────────────────┐     ┌──────────────────┐
│  Mark Completed  │<────│  Transcode Files │<────│  Download Video  │
└──────────────────┘     └──────────────────┘     └──────────────────┘
```

### Key Components

| Component | Tool | Purpose |
|-----------|------|---------|
| Metadata Extraction | yt-dlp | Fetch video/channel info without API keys |
| Video Download | yt-dlp | Download video/audio from YouTube |
| Audio Transcoding | FFmpeg | Convert to MP3/M4A (H.264/AAC) |
| Video Transcoding | FFmpeg | Convert to MP4 (H.264/AAC) |
| Queue Management | SQLite | Track download state and retries |

### Configuration Reference

```yaml
# From configuration.md
poll_interval: 3600                    # Channel refresh interval (seconds)
max_concurrent_downloads: 3            # Max simultaneous downloads

transcode_settings:
  audio_format: "mp3"                  # Output audio format
  audio_bitrate: "192k"                # Audio bitrate
  audio_sample_rate: 44100             # Sample rate in Hz
  video_format: "mp4"                  # Output video container
  video_codec: "h264"                  # Video codec (CPU-only)
  video_quality: 23                    # CRF quality (lower = better)
  threads: 4                           # Encoding threads
  keep_original: false                 # Keep original after transcoding
```

---

## yt-dlp Integration

yt-dlp is the primary tool for downloading YouTube content and extracting metadata. It requires no YouTube API keys.

### Installation & Verification

```bash
# Install yt-dlp (recommended: via pip or brew)
pip install yt-dlp
# or
brew install yt-dlp

# Verify installation
yt-dlp --version

# Install FFmpeg (required for transcoding)
brew install ffmpeg  # macOS
apt install ffmpeg   # Ubuntu/Debian
```

### Command-Line Arguments

#### Download Commands

```bash
# Basic video download
yt-dlp "https://www.youtube.com/watch?v=VIDEO_ID"

# Download best quality video + audio
yt-dlp -f "bestvideo+bestaudio/best" "https://youtube.com/watch?v=VIDEO_ID"

# Download audio only (best quality)
yt-dlp -f "bestaudio/best" -x "https://youtube.com/watch?v=VIDEO_ID"

# Download with custom output template
yt-dlp -o "%(channel)s/%(id)s.%(ext)s" "VIDEO_URL"

# Download with format selection
yt-dlp -f "bestvideo[ext=mp4]+bestaudio[ext=m4a]/best[ext=mp4]/best" "VIDEO_URL"
```

#### yt-dlp Options Reference

| Option | Description | Example |
|--------|-------------|---------|
| `-f, --format` | Video format selection | `-f "bestaudio/best"` |
| `-o, --output` | Output filename template | `-o "%(id)s.%(ext)s"` |
| `--write-info-json` | Write metadata to JSON | `--write-info-json` |
| `--print-json` | Print video info as JSON | `--print-json` |
| `--no-download` | Skip actual download | `--no-download` |
| `--flat-playlist` | List playlist without downloading | `--flat-playlist` |
| `--playlist-end` | Number of videos to download | `--playlist-end 50` |
| `--dateafter` | Download videos after date | `--dateafter 20240101` |
| `--ignore-errors` | Continue on download errors | `--ignore-errors` |
| `--no-progress` | Suppress progress output | `--no-progress` |
| `--quiet` | Quiet mode | `-q` |
| `-x, --extract-audio` | Extract audio only | `-x --audio-format mp3` |
| `--audio-format` | Audio format conversion | `--audio-format mp3` |
| `--audio-quality` | Audio quality | `--audio-quality 192K` |

### JSON Output

#### Fetch Video Metadata (Lightweight)

```bash
# Get video info as JSON without downloading
yt-dlp --print-json --no-download "https://youtube.com/watch?v=VIDEO_ID"
```

#### JSON Response Structure

```json
{
  "id": "dQw4w9WgXcQ",
  "title": "Rick Astley - Never Gonna Give You Up",
  "description": "The official video...",
  "duration": 212,
  "view_count": 1400000000,
  "like_count": 15000000,
  "channel": "Rick Astley",
  "channel_id": "UCuAXFkgsw1L7xaCfnd5JJOw",
  "channel_url": "https://www.youtube.com/channel/UCuAXFkgsw1L7xaCfnd5JJOw",
  "uploader": "Rick Astley",
  "uploader_id": "RickAstleyVEVO",
  "upload_date": "20091025",
  "timestamp": 1256476800,
  "thumbnail": "https://i.ytimg.com/vi/dQw4w9WgXcQ/maxresdefault.jpg",
  "thumbnails": [
    {"url": "https://i.ytimg.com/vi/dQw4w9WgXcQ/hqdefault.jpg", "id": "0"}
  ],
  "categories": ["Music"],
  "tags": ["pop", "80s", "rickroll"],
  "width": 1920,
  "height": 1080,
  "resolution": "1920x1080",
  "fps": 30,
  "vcodec": "avc1",
  "acodec": "mp4a",
  "ext": "mp4",
  "filesize": 50000000,
  "formats": [...]
}
```

#### Channel Episode Discovery

```bash
# List all videos in a channel (flat, no download)
yt-dlp --flat-playlist --print "%(id)s %(title)s" "https://youtube.com/@channel"

# Get detailed info for channel videos
yt-dlp --flat-playlist --print-json "https://youtube.com/@channel"
```

#### Flat Playlist JSON

```json
{
  "id": "dQw4w9WgXcQ",
  "title": "Video Title",
  "type": "url",
  "url": "https://www.youtube.com/watch?v=dQw4w9WgXcQ",
  "ie_key": "Youtube",
  "_type": "url"
}
```

### Metadata Extraction

#### Extract Channel Metadata

```bash
# Get channel info without API
yt-dlp --print-json --no-download "https://youtube.com/@channel" 2>/dev/null | jq '{
  id: .channel_id,
  title: .channel,
  description: .description,
  thumbnail: .channel_url
}'
```

#### Extract Episode Metadata

```csharp
public class YtDlpMetadata
{
    public string id { get; set; }                    // Video ID
    public string title { get; set; }                 // Video title
    public string description { get; set; }           // Video description
    public int duration { get; set; }                 // Duration in seconds
    public string thumbnail { get; set; }             // Thumbnail URL
    public string channel { get; set; }               // Channel name
    public string channel_id { get; set; }            // Channel ID
    public long? timestamp { get; set; }              // Upload timestamp (Unix)
    public string upload_date { get; set; }           // Upload date (YYYYMMDD)
    public string channel_url { get; set; }           // Channel URL
    public List<string> tags { get; set; }            // Video tags
    public List<string> categories { get; set; }      // Video categories
    public string _type { get; set; }                 // Entry type
    public string ie_key { get; set; }                // Extractor key
}
```

### Output Templates

Use output templates for consistent file naming:

```bash
# Channel-based directory structure
yt-dlp -o "%(channel_id)s/%(id)s.%(ext)s" "VIDEO_URL"
# Output: UCuAXFkgsw1L7xaCfnd5JJOw/dQw4w9WgXcQ.mp4

# Custom naming with sanitization
yt-dlp -o "%(channel)s/%(upload_date>%Y-%m-%d)s - %(title)s.%(ext)s" "VIDEO_URL"
# Output: Rick Astley/2009-10-25 - Never Gonna Give You Up.mp4

# With playlist index
yt-dlp -o "%(playlist_index)03d - %(title)s.%(ext)s" "PLAYLIST_URL"
# Output: 001 - Video Title.mp4
```

### Recommended Download Arguments

```bash
# Optimized download command for podcast workflow
yt-dlp \
  --format "bestvideo[ext=mp4]+bestaudio[ext=m4a]/best[ext=mp4]/best" \
  --merge-output-format mp4 \
  --write-info-json \
  --no-playlist \
  --no-overwrites \
  --continue \
  --ignore-errors \
  --no-progress \
  --output "%(channel_id)s/%(id)s.%(ext)s" \
  "VIDEO_URL"
```

---

## Download Workflow

### State Machine

Each episode transitions through a defined state machine:

```
┌─────────┐    Refresh     ┌─────────┐   Download    ┌─────────────┐
│ pending │───────────────>│ queued  │──────────────>│ downloading │
└─────────┘                └─────────┘               └─────────────┘
                                │                          │
                                │ Cancel                   │ Success
                                ▼                          ▼
                           ┌─────────┐               ┌────────────┐
                           │cancelled│               │ processing │
                           └─────────┘               └────────────┘
                                                          │
                                                          │ Transcode
                                                          ▼
                         ┌──────────┐    Error      ┌───────────┐
                         │  failed  │<──────────────│ transcoding│
                         └──────────┘               └───────────┘
                              │                          │
                              │ Retry                    │ Success
                              ▼                          ▼
                         ┌──────────┐              ┌───────────┐
                         │  retry   │              │ completed │
                         └──────────┘              └───────────┘
                                                        │
                                                        │ Cleanup
                                                        ▼
                                                   ┌──────────┐
                                                   │ deleted  │
                                                   └──────────┘
```

### Episode Status Values

| Status | Description | Next States |
|--------|-------------|-------------|
| `pending` | Queued for download, not started | `downloading`, `cancelled` |
| `downloading` | Currently downloading | `processing`, `failed` |
| `processing` | Download complete, transcoding | `completed`, `failed` |
| `completed` | Ready for feeds | `deleted` |
| `failed` | Max retries exceeded | `pending` (manual retry) |
| `deleted` | Files removed from disk | - |

### Refresh Trigger

The download workflow is initiated by the refresh scheduler:

```csharp
public class RefreshScheduler : IHostedService
{
    private readonly ILogger<RefreshScheduler> _logger;
    private readonly IConfiguration _config;
    private readonly ChannelService _channelService;
    private Timer _timer;
    
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var pollInterval = _config.GetValue<int>("poll_interval", 3600);
        _logger.LogInformation("Starting refresh scheduler with {Interval}s interval", pollInterval);
        
        // Initial refresh on startup
        _ = RefreshAllChannelsAsync();
        
        // Schedule periodic refresh
        _timer = new Timer(
            async _ => await RefreshAllChannelsAsync(),
            null,
            TimeSpan.FromSeconds(pollInterval),
            TimeSpan.FromSeconds(pollInterval)
        );
    }
    
    private async Task RefreshAllChannelsAsync()
    {
        var channels = await _channelService.GetEnabledChannelsAsync();
        
        foreach (var channel in channels)
        {
            try
            {
                await RefreshChannelAsync(channel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to refresh channel {ChannelId}", channel.id);
            }
        }
    }
}
```

### Episode Discovery

When refreshing a channel, yt-dlp fetches the list of available videos:

```csharp
public class ChannelRefresher
{
    private readonly YtDlpClient _ytDlp;
    private readonly EpisodeRepository _episodeRepo;
    private readonly DownloadQueueService _queueService;
    
    public async Task<RefreshResult> RefreshChannelAsync(Channel channel)
    {
        var result = new RefreshResult { ChannelId = channel.id };
        
        // Step 1: Fetch video list from YouTube
        var videoList = await _ytDlp.GetChannelVideosAsync(channel.url);
        result.VideosFound = videoList.Count;
        
        // Step 2: Filter by rolling window
        var videosToProcess = videoList
            .OrderByDescending(v => v.Timestamp)
            .Take(channel.episode_count_config)
            .ToList();
        
        // Step 3: Check for new episodes (dedupe by video_id)
        foreach (var video in videosToProcess)
        {
            var existing = await _episodeRepo.GetByVideoIdAsync(video.id);
            
            if (existing == null)
            {
                // New episode discovered
                var episode = await CreateEpisodeAsync(channel, video);
                await _queueService.EnqueueAsync(episode, priority: 5);
                result.EpisodesQueued++;
            }
        }
        
        // Step 4: Update channel last_refresh_at
        await _channelRepo.UpdateLastRefreshAsync(channel.id);
        result.RefreshedAt = DateTime.UtcNow;
        
        return result;
    }
    
    private async Task<Episode> CreateEpisodeAsync(Channel channel, YtDlpVideoInfo video)
    {
        var episode = new Episode
        {
            id = Guid.NewGuid().ToString("N"),
            video_id = video.id,
            channel_id = channel.id,
            title = video.title,
            description = video.description,
            thumbnail_url = video.thumbnail,
            duration_seconds = video.duration,
            published_at = video.Timestamp.HasValue 
                ? DateTimeOffset.FromUnixTimeSeconds(video.Timestamp.Value).DateTime
                : null,
            status = "pending",
            created_at = DateTime.UtcNow,
            updated_at = DateTime.UtcNow
        };
        
        await _episodeRepo.CreateAsync(episode);
        return episode;
    }
}
```

### Deduplication by video_id

The `episodes.video_id` field has a unique constraint, ensuring no duplicate downloads:

```sql
-- Check for existing episode before creating
SELECT id, status, channel_id 
FROM episodes 
WHERE video_id = ?;

-- Unique constraint prevents duplicates
CREATE UNIQUE INDEX idx_episodes_video_id ON episodes(video_id);
```

```csharp
public async Task<Episode> GetOrCreateEpisodeAsync(Channel channel, string videoId)
{
    // Check for existing episode (dedupe)
    var existing = await _episodeRepo.GetByVideoIdAsync(videoId);
    if (existing != null)
    {
        _logger.LogDebug("Episode {VideoId} already exists with status {Status}", 
            videoId, existing.status);
        return existing;
    }
    
    // Create new episode
    return await CreateEpisodeAsync(channel, videoId);
}
```

---

## Concurrent Downloads

### Semaphore-Based Concurrency

Control concurrent downloads using a semaphore:

```csharp
public class DownloadCoordinator
{
    private readonly SemaphoreSlim _downloadSemaphore;
    private readonly ILogger<DownloadCoordinator> _logger;
    private readonly IConfiguration _config;
    
    public DownloadCoordinator(IConfiguration config, ILogger<DownloadCoordinator> logger)
    {
        _config = config;
        _logger = logger;
        
        var maxConcurrent = config.GetValue<int>("max_concurrent_downloads", 3);
        _downloadSemaphore = new SemaphoreSlim(maxConcurrent, maxConcurrent);
        
        _logger.LogInformation("Download coordinator initialized with {MaxConcurrent} concurrent slots", 
            maxConcurrent);
    }
    
    public async Task<DownloadResult> DownloadAsync(Episode episode, CancellationToken ct = default)
    {
        _logger.LogInformation("Waiting for download slot for episode {EpisodeId}", episode.id);
        
        await _downloadSemaphore.WaitAsync(ct);
        try
        {
            _logger.LogInformation("Starting download for episode {EpisodeId}", episode.id);
            return await _downloader.DownloadVideoAsync(episode, ct);
        }
        finally
        {
            _downloadSemaphore.Release();
            _logger.LogInformation("Released download slot for episode {EpisodeId}", episode.id);
        }
    }
    
    public int AvailableSlots => _downloadSemaphore.CurrentCount;
}
```

### Queue Processing Worker

```csharp
public class DownloadWorker : BackgroundService
{
    private readonly DownloadCoordinator _coordinator;
    private readonly DownloadQueueService _queueService;
    private readonly EpisodeRepository _episodeRepo;
    private readonly TimeSpan _pollInterval = TimeSpan.FromSeconds(5);
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessNextItemAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing download queue");
            }
            
            await Task.Delay(_pollInterval, stoppingToken);
        }
    }
    
    private async Task ProcessNextItemAsync(CancellationToken ct)
    {
        // Get next pending item from queue
        var queueItem = await _queueService.GetNextPendingAsync();
        
        if (queueItem == null)
        {
            _logger.LogDebug("No pending downloads in queue");
            return;
        }
        
        // Mark as in-progress
        await _queueService.MarkInProgressAsync(queueItem.id);
        var episode = await _episodeRepo.GetByIdAsync(queueItem.episode_id);
        
        try
        {
            // Download with concurrency control
            var result = await _coordinator.DownloadAsync(episode, ct);
            
            if (result.Success)
            {
                // Transcode
                await TranscodeAsync(episode, result.TempFilePath);
                await _queueService.MarkCompletedAsync(queueItem.id);
            }
            else
            {
                await _queueService.MarkFailedAsync(queueItem.id, result.ErrorMessage);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Download cancelled for episode {EpisodeId}", episode.id);
            await _queueService.MarkCancelledAsync(queueItem.id);
        }
    }
}
```

### Configurable Concurrency

The `max_concurrent_downloads` config controls how many downloads run in parallel:

```yaml
# Conservative (low bandwidth/processing)
max_concurrent_downloads: 1

# Balanced (recommended)
max_concurrent_downloads: 3

# Aggressive (high bandwidth/CPU)
max_concurrent_downloads: 5
```

```csharp
public static int ValidateConcurrencySetting(int configValue)
{
    if (configValue < 1) return 1;
    if (configValue > 10) return 10;
    return configValue;
}
```

---

## FFmpeg Transcoding

### Transcode Pipeline

After download, videos are transcoded to podcast-ready formats:

```
Downloaded Video (MKV/WebM/etc.)
         │
         ├──────────────────────────────┐
         │                              │
         ▼                              ▼
   Transcode Audio                 Transcode Video
   (H.264/AAC → MP3)              (H.264/AAC → MP4)
         │                              │
         ▼                              ▼
    audio/video_id.mp3            video/video_id.mp4
```

### FFmpeg Commands

#### Audio Transcoding (MP3)

```bash
# Extract audio, convert to MP3 with specified bitrate
ffmpeg -i input.mp4 \
  -vn \
  -acodec libmp3lame \
  -b:a 192k \
  -ar 44100 \
  -ac 2 \
  -y \
  output.mp3
```

#### Audio Transcoding (M4A/AAC)

```bash
# Extract audio, convert to AAC/M4A
ffmpeg -i input.mp4 \
  -vn \
  -acodec aac \
  -b:a 192k \
  -ar 44100 \
  -ac 2 \
  -y \
  output.m4a
```

#### Video Transcoding (MP4 H.264)

```bash
# Transcode video to H.264/AAC in MP4 container
ffmpeg -i input.mkv \
  -c:v libx264 \
  -preset medium \
  -crf 23 \
  -c:a aac \
  -b:a 192k \
  -ar 44100 \
  -ac 2 \
  -movflags +faststart \
  -y \
  output.mp4
```

### Transcode Settings from Config

```csharp
public class TranscodeSettings
{
    public string audio_format { get; set; } = "mp3";
    public string audio_bitrate { get; set; } = "192k";
    public int audio_sample_rate { get; set; } = 44100;
    public string video_format { get; set; } = "mp4";
    public string video_codec { get; set; } = "h264";
    public int video_quality { get; set; } = 23;
    public int threads { get; set; } = 4;
    public bool keep_original { get; set; } = false;
}

public class Transcoder
{
    private readonly TranscodeSettings _settings;
    
    public async Task<TranscodeResult> TranscodeAudioAsync(string inputPath, string outputPath)
    {
        var args = new List<string>
        {
            "-i", inputPath,
            "-vn",  // No video
            "-acodec", _settings.audio_format == "mp3" ? "libmp3lame" : "aac",
            "-b:a", _settings.audio_bitrate,
            "-ar", _settings.audio_sample_rate.ToString(),
            "-ac", "2",  // Stereo
            "-y",  // Overwrite output
            outputPath
        };
        
        return await RunFfmpegAsync(args);
    }
    
    public async Task<TranscodeResult> TranscodeVideoAsync(string inputPath, string outputPath)
    {
        var args = new List<string>
        {
            "-i", inputPath,
            "-c:v", "libx264",
            "-preset", "medium",
            "-crf", _settings.video_quality.ToString(),
            "-c:a", "aac",
            "-b:a", _settings.audio_bitrate,
            "-ar", _settings.audio_sample_rate.ToString(),
            "-ac", "2",
            "-movflags", "+faststart",  // Enable streaming
            "-threads", _settings.threads.ToString(),
            "-y",
            outputPath
        };
        
        return await RunFfmpegAsync(args);
    }
    
    private async Task<TranscodeResult> RunFfmpegAsync(List<string> args)
    {
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "ffmpeg",
                Arguments = string.Join(" ", args),
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            }
        };
        
        var timer = Stopwatch.StartNew();
        process.Start();
        await process.WaitForExitAsync();
        timer.Stop();
        
        return new TranscodeResult
        {
            Success = process.ExitCode == 0,
            ExitCode = process.ExitCode,
            Duration = timer.Elapsed,
            ErrorOutput = await process.StandardError.ReadToEndAsync()
        };
    }
}
```

### CPU-Only H.264/AAC

By default, use software encoding (CPU-only):

| Codec | Library | Notes |
|-------|---------|-------|
| H.264 | `libx264` | Software encoder, widely compatible |
| AAC | `aac` or `libfdk_aac` | Built-in AAC encoder |

```bash
# CPU-only encoding (default)
ffmpeg -i input.mp4 -c:v libx264 -preset medium -crf 23 -c:a aac output.mp4

# Faster encoding (lower quality)
ffmpeg -i input.mp4 -c:v libx264 -preset fast -crf 26 -c:a aac output.mp4

# Higher quality (slower)
ffmpeg -i input.mp4 -c:v libx264 -preset slow -crf 20 -c:a aac output.mp4
```

### Quality Settings

CRF (Constant Rate Factor) controls quality:

| CRF Value | Quality | Use Case |
|-----------|---------|----------|
| 18-22 | High | Archival, high quality |
| 23-28 | Medium | Podcast (recommended) |
| 28-32 | Low | Bandwidth-constrained |

---

## Error Handling

### Retry with Exponential Backoff

Failed downloads are retried with increasing delays:

```csharp
public class RetryStrategy
{
    private static readonly TimeSpan[] BackoffDelays = new[]
    {
        TimeSpan.Zero,           // Attempt 1: Immediate
        TimeSpan.FromMinutes(5),  // Attempt 2: Wait 5 minutes
        TimeSpan.FromMinutes(30), // Attempt 3: Wait 30 minutes
        TimeSpan.FromHours(2),    // Attempt 4: Wait 2 hours
        TimeSpan.FromHours(8)     // Attempt 5: Wait 8 hours
    };
    
    public DateTime? CalculateNextRetry(int attempts)
    {
        if (attempts >= BackoffDelays.Length)
            return null;  // Max retries exceeded
        
        return DateTime.UtcNow + BackoffDelays[attempts];
    }
}
```

### Queue Status Management

```sql
-- Mark download as failed with retry
UPDATE download_queue 
SET status = 'pending',
    attempts = attempts + 1,
    last_error = ?,
    next_retry_at = ?,
    updated_at = datetime('now')
WHERE id = ?;

-- Mark download as permanently failed
UPDATE download_queue 
SET status = 'failed',
    last_error = ?,
    updated_at = datetime('now')
WHERE id = ?;
```

### Error Tracking

```csharp
public class DownloadErrorHandler
{
    public async Task HandleErrorAsync(string queueId, Exception error)
    {
        var queueItem = await _queueRepo.GetByIdAsync(queueId);
        
        var errorMessage = error switch
        {
            YtDlpNotFoundException e => "Video not found or removed",
            YtDlpPrivateVideoException e => "Video is private or age-restricted",
            YtDlpNetworkException e => "Network error, retrying",
            TranscodeException e => $"Transcoding failed: {e.Message}",
            _ => $"Unknown error: {error.Message}"
        };
        
        queueItem.attempts++;
        queueItem.last_error = errorMessage;
        
        if (queueItem.attempts >= queueItem.max_attempts)
        {
            // Max retries exceeded
            queueItem.status = "failed";
            _logger.LogError("Download permanently failed for queue {QueueId}: {Error}", 
                queueId, errorMessage);
            
            // Update episode status
            await _episodeRepo.UpdateStatusAsync(queueItem.episode_id, "failed", errorMessage);
        }
        else
        {
            // Schedule retry
            queueItem.status = "pending";
            queueItem.next_retry_at = _retryStrategy.CalculateNextRetry(queueItem.attempts);
            
            _logger.LogWarning("Download failed for queue {QueueId}, schedule retry #{Attempt}: {Error}", 
                queueId, queueItem.attempts, errorMessage);
        }
        
        await _queueRepo.UpdateAsync(queueItem);
    }
}
```

### Failed Episode Tracking

Failed episodes are tracked in the database and can be retried:

```sql
-- Get all failed downloads
SELECT dq.id, dq.episode_id, dq.attempts, dq.max_attempts, dq.last_error,
       e.video_id, e.title, c.title as channel_title
FROM download_queue dq
JOIN episodes e ON dq.episode_id = e.id
JOIN channels c ON e.channel_id = c.id
WHERE dq.status = 'failed'
ORDER BY dq.updated_at DESC;

-- Manually retry a failed download
UPDATE download_queue 
SET status = 'pending',
    attempts = 0,
    next_retry_at = NULL,
    updated_at = datetime('now')
WHERE episode_id = ?;

UPDATE episodes 
SET status = 'pending',
    error_message = NULL,
    updated_at = datetime('now')
WHERE id = ?;
```

---

## File Management

### Directory Structure

```
downloads/
├── {channel-id-1}/
│   ├── audio/
│   │   ├── video-id-1.mp3
│   │   ├── video-id-2.mp3
│   │   └── ...
│   ├── video/
│   │   ├── video-id-1.mp4
│   │   ├── video-id-2.mp4
│   │   └── ...
│   └── thumbnails/
│       ├── video-id-1.jpg
│       └── video-id-2.jpg
├── {channel-id-2}/
│   └── ...
└── temp/
    └── downloads/
        └── video-id-partial.mp4
```

### File Naming Convention

```csharp
public class FilePathGenerator
{
    private readonly string _downloadDir;
    
    public string GenerateAudioPath(string channelId, string videoId, string format)
    {
        return Path.Combine(
            _downloadDir,
            channelId,
            "audio",
            $"{videoId}.{format}"
        );
    }
    
    public string GenerateVideoPath(string channelId, string videoId)
    {
        return Path.Combine(
            _downloadDir,
            channelId,
            "video",
            $"{videoId}.mp4"
        );
    }
    
    public string GenerateThumbnailPath(string channelId, string videoId)
    {
        return Path.Combine(
            _downloadDir,
            channelId,
            "thumbnails",
            $"{videoId}.jpg"
        );
    }
}
```

### Storage Paths in Database

```sql
-- Episodes store relative paths
file_path_audio: "channel-abc123/audio/video-id-123.mp3"
file_path_video: "channel-abc123/video/video-id-123.mp4"

-- File sizes stored for RSS enclosures
file_size_audio: 52428800
file_size_video: 524288000
```

### Cleanup Process

Remove episodes outside the rolling window:

```csharp
public class EpisodeCleanupService
{
    public async Task CleanupOldEpisodesAsync(Channel channel)
    {
        // Get episodes to delete (outside rolling window)
        var episodesToDelete = await _episodeRepo.GetEpisodesOutsideWindowAsync(
            channel.id, 
            channel.episode_count_config
        );
        
        foreach (var episode in episodesToDelete)
        {
            try
            {
                // Delete files from disk
                await DeleteFilesAsync(episode);
                
                // Mark as deleted in database
                await _episodeRepo.UpdateStatusAsync(episode.id, "deleted");
                
                _logger.LogInformation("Deleted old episode {EpisodeId} for channel {ChannelId}", 
                    episode.id, channel.id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete episode {EpisodeId}", episode.id);
            }
        }
    }
    
    private async Task DeleteFilesAsync(Episode episode)
    {
        if (episode.file_path_audio != null)
        {
            var fullPath = Path.Combine(_downloadDir, episode.file_path_audio);
            if (File.Exists(fullPath))
                File.Delete(fullPath);
        }
        
        if (episode.file_path_video != null)
        {
            var fullPath = Path.Combine(_downloadDir, episode.file_path_video);
            if (File.Exists(fullPath))
                File.Delete(fullPath);
        }
    }
}
```

### Temporary File Management

```csharp
public class TempFileManager
{
    private readonly string _tempDir;
    private readonly TimeSpan _maxAge = TimeSpan.FromHours(24);
    
    public async Task<string> CreateTempDownloadPathAsync(string videoId)
    {
        var tempPath = Path.Combine(_tempDir, "downloads", $"{videoId}.partial.mp4");
        Directory.CreateDirectory(Path.GetDirectoryName(tempPath));
        return tempPath;
    }
    
    public async Task CleanupStaleTempFilesAsync()
    {
        var cutoff = DateTime.UtcNow - _maxAge;
        
        foreach (var file in Directory.EnumerateFiles(_tempDir, "*.partial.*", SearchOption.AllDirectories))
        {
            var info = new FileInfo(file);
            if (info.CreationTimeUtc < cutoff)
            {
                File.Delete(file);
                _logger.LogInformation("Cleaned up stale temp file: {File}", file);
            }
        }
    }
}
```

---

## Pipeline Architecture

### Background Services

The pipeline runs as a set of background services:

```csharp
// Program.cs or Startup.cs
builder.Services.AddHostedService<RefreshScheduler>();
builder.Services.AddHostedService<DownloadWorker>();
builder.Services.AddHostedService<TranscodeWorker>();
builder.Services.AddHostedService<CleanupWorker>();
```

### Worker Architecture

```
┌──────────────────────────────────────────────────────────────┐
│                     Background Services                       │
├──────────────────┬──────────────────┬───────────────────────┤
│ RefreshScheduler │  DownloadWorker  │   TranscodeWorker     │
│                  │                  │                       │
│   Periodic:      │   Continuous:    │   Continuous:         │
│   - Check for    │   - Pull from    │   - Watch for         │
│     new episodes │     queue        │     downloaded files  │
│   - Update       │   - Download     │   - Transcode to      │
│     last_refresh │     with yt-dlp  │     target formats    │
│                  │   - Handle       │   - Update            │
│   Interval:      │     retries      │     file_size         │
│   poll_interval  │   - Semaphore    │   - Clean up          │
│                  │     limiting     │     originals         │
└──────────────────┴──────────────────┴───────────────────────┘
         │                   │                    │
         ▼                   ▼                    ▼
┌──────────────────────────────────────────────────────────────┐
│                       SQLite Database                         │
│  channels │ episodes │ download_queue │ schema_version        │
└──────────────────────────────────────────────────────────────┘
```

### Complete Pipeline Flow

```csharp
public class DownloadPipeline
{
    public async Task ExecuteAsync(Episode episode, CancellationToken ct)
    {
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            // Step 1: Download video
            await UpdateStatusAsync(episode, "downloading");
            var downloadResult = await _downloader.DownloadAsync(episode, ct);
            
            if (!downloadResult.Success)
            {
                await HandleErrorAsync(episode, downloadResult.Error);
                return;
            }
            
            // Step 2: Transcode based on channel settings
            await UpdateStatusAsync(episode, "processing");
            var channel = await _channelRepo.GetByIdAsync(episode.channel_id);
            
            if (channel.feed_type is "audio" or "both")
            {
                var audioPath = GenerateAudioPath(episode);
                await _transcoder.TranscodeAudioAsync(
                    downloadResult.TempPath, 
                    audioPath,
                    channel.custom_settings
                );
                await UpdateFileSizeAsync(episode, "audio", audioPath);
            }
            
            if (channel.feed_type is "video" or "both")
            {
                var videoPath = GenerateVideoPath(episode);
                await _transcoder.TranscodeVideoAsync(
                    downloadResult.TempPath, 
                    videoPath,
                    channel.custom_settings
                );
                await UpdateFileSizeAsync(episode, "video", videoPath);
            }
            
            // Step 3: Cleanup
            if (!_settings.keep_original)
            {
                File.Delete(downloadResult.TempPath);
            }
            
            // Step 4: Mark complete
            await UpdateStatusAsync(episode, "completed");
            await UpdateDownloadedAtAsync(episode);
            
            _logger.LogInformation(
                "Pipeline completed for episode {EpisodeId} in {Duration}s",
                episode.id, stopwatch.Elapsed.TotalSeconds
            );
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Pipeline cancelled for episode {EpisodeId}", episode.id);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Pipeline failed for episode {EpisodeId}", episode.id);
            await HandleErrorAsync(episode, ex.Message);
        }
    }
}
```

### Monitoring & Observability

```csharp
public class PipelineMetrics
{
    private readonly IMetricsCollector _metrics;
    
    public void RecordDownloadStart(string episodeId, string channelId)
    {
        _metrics.Increment("downloads.started");
        _metrics.Gauge("downloads.active", _coordinator.ActiveDownloads);
    }
    
    public void RecordDownloadComplete(string episodeId, TimeSpan duration, long bytes)
    {
        _metrics.Increment("downloads.completed");
        _metrics.Histogram("downloads.duration_seconds", duration.TotalSeconds);
        _metrics.Histogram("downloads.bytes", bytes);
        _metrics.Gauge("downloads.active", _coordinator.ActiveDownloads);
    }
    
    public void RecordDownloadFailed(string episodeId, string error)
    {
        _metrics.Increment("downloads.failed");
        _metrics.Increment($"downloads.errors.{SanitizeLabel(error)}");
    }
    
    public void RecordTranscodeComplete(string episodeId, string format, TimeSpan duration)
    {
        _metrics.Increment("transcodes.completed");
        _metrics.Histogram($"transcodes.{format}.duration_seconds", duration.TotalSeconds);
    }
}
```

---

## See Also

- [Data Model & Schema Design](./data-model.md) - Database schema documentation
- [Configuration Schema](./configuration.md) - YAML configuration reference
- [API Specification](./api-specification.md) - REST API documentation
- [Feed Generation](./feed-generation.md) - RSS/Atom feed generation

## References

- [yt-dlp Documentation](https://github.com/yt-dlp/yt-dlp)
- [yt-dlp JSON Output Format](https://github.com/yt-dlp/yt-dlp#json-object)
- [FFmpeg Documentation](https://ffmpeg.org/documentation.html)
- [FFmpeg H.264 Encoding Guide](https://trac.ffmpeg.org/wiki/Encode/H.264)
- [FFmpeg AAC Encoding Guide](https://trac.ffmpeg.org/wiki/Encode/AAC)