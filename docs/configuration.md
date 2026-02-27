# Configuration Schema Design

This document defines the YAML configuration file structure for Yallarhorn, a podcast server that downloads YouTube videos and serves them as RSS/Atom feeds.

## Table of Contents

- [Overview](#overview)
- [Configuration File Location](#configuration-file-location)
- [Schema Structure](#schema-structure)
  - [Global Settings](#global-settings)
  - [Authentication](#authentication)
  - [Channels](#channels)
- [Complete Example Configuration](#complete-example-configuration)
- [Validation Rules](#validation-rules)
- [Environment Variable Substitution](#environment-variable-substitution)
- [Migration Guide](#migration-guide)

## Overview

Yallarhorn uses a single YAML configuration file to define:
- **Global settings**: Polling intervals, download concurrency, transcoding options
- **Authentication**: Feed access and admin API credentials
- **Channels**: YouTube channels to monitor with individual settings

## Configuration File Location

By default, Yallarhorn looks for configuration in the following locations (in order):

1. `./yallarhorn.yaml` (current directory)
2. `./yallarhorn.yml` (current directory)
3. `~/.config/yallarhorn/config.yaml` (user config)
4. `/etc/yallarhorn/config.yaml` (system-wide)

You can specify a custom path using the `--config` flag:

```bash
yallarhorn --config /path/to/custom-config.yaml
```

## Schema Structure

### Global Settings

Global settings control the overall behavior of Yallarhorn.

```yaml
# Global Settings
poll_interval: 3600                    # How often to check for new videos (seconds)
max_concurrent_downloads: 3            # Maximum simultaneous downloads
download_dir: "./downloads"            # Base directory for downloaded content
temp_dir: "./temp"                     # Temporary file directory

transcode_settings:
  audio_format: "mp3"                  # Output audio format
  audio_bitrate: "192k"                # Audio bitrate
  audio_sample_rate: 44100             # Sample rate in Hz
  video_format: "mp4"                  # Output video format
  video_codec: "h264"                  # Video codec
  video_quality: 23                    # CRF quality (lower = better)
  threads: 4                           # Number of encoding threads
  keep_original: false                 # Keep original download after transcoding

server:
  host: "0.0.0.0"                      # Server bind address
  port: 8080                           # Server port
  base_url: "http://localhost:8080"    # Public URL for feed generation
  feed_path: "/feeds"                  # URL path prefix for feeds

database:
  path: "./yallarhorn.db"              # SQLite database file path
  pool_size: 5                         # Connection pool size

logging:
  level: "info"                        # Log level: debug, info, warn, error
  file: "./logs/yallarhorn.log"        # Log file path (optional)
  max_size_mb: 100                     # Max log file size before rotation
  max_backups: 3                       # Number of rotated log files to keep
```

#### Global Settings Reference

| Setting | Type | Default | Description |
|---------|------|---------|-------------|
| `poll_interval` | integer | `3600` | Interval between channel refresh cycles (seconds). Min: 300 (5 min) |
| `max_concurrent_downloads` | integer | `3` | Maximum simultaneous downloads. Range: 1-10 |
| `download_dir` | string | `./downloads` | Base directory for downloaded content |
| `temp_dir` | string | `./temp` | Temporary file storage during processing |

#### Transcode Settings Reference

| Setting | Type | Default | Description |
|---------|------|---------|-------------|
| `audio_format` | string | `mp3` | Output audio format: `mp3`, `aac`, `ogg`, `m4a` |
| `audio_bitrate` | string | `192k` | Audio bitrate for encoding |
| `audio_sample_rate` | integer | `44100` | Audio sample rate in Hz |
| `video_format` | string | `mp4` | Output video container |
| `video_codec` | string | `h264` | Video encoding codec |
| `video_quality` | integer | `23` | CRF quality value (18-28, lower = better) |
| `threads` | integer | `4` | Number of FFmpeg encoding threads |
| `keep_original` | boolean | `false` | Keep original files after transcoding |

### Authentication

The authentication section defines credentials for feed access and the management API. Note: yt-dlp extracts metadata directly from YouTube without requiring API keys.

```yaml
# Authentication Settings
auth:
  # Feed access credentials (HTTP Basic Auth for /feed/* endpoints)
  feed_credentials:
    enabled: true
    username: "feed-user"
    password: "${FEED_PASSWORD}"       # Use environment variable for secrets
    realm: "Yallarhorn Feeds"
  
  # HTTP Basic Auth for management API (/api/* endpoints)
  admin_auth:
    enabled: true
    username: "admin"
    password: "${ADMIN_PASSWORD}"
```

#### Feed Credentials Reference

| Setting | Type | Default | Description |
|---------|------|---------|-------------|
| `enabled` | boolean | `false` | Enable HTTP Basic Auth on feed endpoints |
| `username` | string | - | Username for feed access |
| `password` | string | - | Password for feed access |
| `realm` | string | `Yallarhorn Feeds` | HTTP auth realm |

#### Admin Auth Reference

| Setting | Type | Default | Description |
|---------|------|---------|-------------|
| `enabled` | boolean | `false` | Enable HTTP Basic Auth on admin API endpoints |
| `username` | string | - | Username for admin access |
| `password` | string | - | Password for admin access |

### Channels

The channels section defines which YouTube channels to monitor.

```yaml
# Channel Configuration
channels:
  # Minimal configuration
  - name: "Tech Channel"
    url: "https://www.youtube.com/@techchannel"
  
  # Full configuration
  - name: "Educational Content"
    url: "https://www.youtube.com/@educhannel"
    episode_count: 25                  # Override global episode limit
    enabled: true                      # Enable/disable this channel
    feed_type: "audio"                 # Output format: audio, video, both
    
    # Custom transcoding settings (optional)
    custom_settings:
      audio_bitrate: "128k"            # Lower quality for talk content
      audio_format: "m4a"
    
    # Custom metadata
    description: "Educational podcast content"
    tags: ["education", "learning"]
    
  # Video-only channel
  - name: "Video Podcast"
    url: "https://www.youtube.com/@videopodcast"
    enabled: true
    feed_type: "video"
    episode_count: 10
  
  # Disabled channel (won't be refreshed)
  - name: "Archived Channel"
    url: "https://www.youtube.com/@archived"
    enabled: false
    episode_count: 100
```

#### Channel Settings Reference

| Setting | Type | Required | Default | Description |
|---------|------|----------|---------|-------------|
| `name` | string | yes | - | Display name for the channel |
| `url` | string | yes | - | YouTube channel URL |
| `episode_count` | integer | no | `50` | Number of episodes to keep (rolling window) |
| `enabled` | boolean | no | `true` | Whether to actively monitor this channel |
| `feed_type` | string | no | `audio` | Feed output type: `audio`, `video`, `both` |
| `custom_settings` | object | no | - | Override transcode settings for this channel |
| `description` | string | no | - | Custom channel description |
| `tags` | array | no | - | Tags for categorization |

#### Supported Channel URL Formats

Yallarhorn accepts the following YouTube URL formats:

| Format | Example |
|--------|---------|
| Handle URL | `https://www.youtube.com/@channelname` |
| Custom URL | `https://www.youtube.com/c/channelname` |
| Channel ID | `https://www.youtube.com/channel/UC...` |
| User URL | `https://www.youtube.com/user/username` |

## Complete Example Configuration

```yaml
# Yallarhorn Configuration
# Full example with all available options

# Global Settings
poll_interval: 3600
max_concurrent_downloads: 3
download_dir: "./downloads"
temp_dir: "./temp"

# Transcoding Configuration
transcode_settings:
  audio_format: "mp3"
  audio_bitrate: "192k"
  audio_sample_rate: 44100
  video_format: "mp4"
  video_codec: "h264"
  video_quality: 23
  threads: 4
  keep_original: false

# Server Configuration
server:
  host: "0.0.0.0"
  port: 8080
  base_url: "http://localhost:8080"
  feed_path: "/feeds"

# Database Configuration
database:
  path: "./yallarhorn.db"
  pool_size: 5

# Logging Configuration
logging:
  level: "info"
  file: "./logs/yallarhorn.log"
  max_size_mb: 100
  max_backups: 3

# Authentication
auth:
  feed_credentials:
    enabled: true
    username: "feed-user"
    password: "${FEED_PASSWORD}"
  
  admin_auth:
    enabled: true
    username: "admin"
    password: "${ADMIN_PASSWORD}"

# Channels
channels:
  # Tech podcast - audio only
  - name: "Tech Talk Weekly"
    url: "https://www.youtube.com/@techtalkweekly"
    episode_count: 30
    enabled: true
    feed_type: "audio"
    custom_settings:
      audio_bitrate: "160k"
    tags: ["technology", "podcast"]
  
  # Educational content - audio with lower quality
  - name: "Learn With Me"
    url: "https://www.youtube.com/@learnwithme"
    episode_count: 50
    enabled: true
    feed_type: "audio"
    custom_settings:
      audio_bitrate: "96k"
      audio_format: "m4a"
    tags: ["education"]
  
  # Video podcast - both formats
  - name: "Video Show"
    url: "https://www.youtube.com/@videoshow"
    episode_count: 20
    enabled: true
    feed_type: "both"
    tags: ["video", "entertainment"]
  
  # Inactive channel
  - name: "Old Podcast Archive"
    url: "https://www.youtube.com/channel/UCabcdefghijklmnopqrstuvwxyz"
    enabled: false
    episode_count: 100
    tags: ["archive", "inactive"]
```

## Validation Rules

### Schema Validation

Yallarhorn validates the configuration file on startup. Invalid configurations will prevent the server from starting.

#### Required Fields

- At least one channel must be defined
- Each channel must have `name` and `url`
- If `auth.feed_credentials.enabled` is true, `username` and `password` must be set
- If `auth.admin_auth.enabled` is true, `username` and `password` must be set

#### Type Validation

| Field | Valid Type | Validation |
|-------|------------|------------|
| `poll_interval` | integer | Minimum: 300 seconds |
| `max_concurrent_downloads` | integer | Range: 1-10 |
| `episode_count` | integer | Range: 1-1000 |
| `enabled` | boolean | true/false |
| `feed_type` | string enum | Must be: `audio`, `video`, or `both` |
| `audio_format` | string enum | Must be: `mp3`, `aac`, `ogg`, or `m4a` |
| `audio_bitrate` | string pattern | Must match: `\d+[kKmM]` |
| `audio_sample_rate` | integer | Typical values: 22050, 44100, 48000 |
| `video_quality` | integer | Range: 0-51 (18-28 recommended) |
| `threads` | integer | Range: 1-CPU core count |

#### URL Validation

Channel URLs must:
- Use HTTPS protocol
- Be a valid YouTube channel URL format
- Not be a playlist or video URL

```yaml
# Valid URLs
url: "https://www.youtube.com/@techchannel"
url: "https://www.youtube.com/c/channelname"
url: "https://www.youtube.com/channel/UC1234567890abcdef"
url: "https://www.youtube.com/user/username"

# Invalid URLs (will fail validation)
url: "https://www.youtube.com/watch?v=abc123"        # Video URL
url: "https://www.youtube.com/playlist?list=xyz"     # Playlist URL
url: "http://www.youtube.com/@channel"               # Must use HTTPS
```

### Validation Error Messages

When validation fails, Yallarhorn outputs detailed error messages:

```
Configuration validation failed:
  - channels[0].url: Invalid YouTube channel URL format
  - channels[2].episode_count: Value must be between 1 and 1000, got 0
  - transcode_settings.audio_bitrate: Invalid bitrate format, expected format like "192k"
  - auth.feed_credentials.password: Password is required when feed_credentials is enabled
```

### Configuration Linting

Validate your configuration without starting the server:

```bash
yallarhorn config validate
```

Check for deprecated or unused settings:

```bash
yallarhorn config lint
```

## Environment Variable Substitution

Yallarhorn supports environment variable substitution for sensitive values. Use `${VARIABLE_NAME}` syntax:

```yaml
auth:
  feed_credentials:
    password: "${FEED_PASSWORD}"
  admin_auth:
    password: "${ADMIN_PASSWORD}"
```

### Environment Variable Features

- **Default values**: `${VAR:-default}` - Use default if VAR is unset
- **Required variables**: `${VAR:?error message}` - Fail with error if VAR is unset
- **Nested substitution**: Variables in paths are resolved

```yaml
database:
  path: "${DATA_DIR:-./data}/yallarhorn.db"
  
auth:
  feed_credentials:
    password: "${FEED_PASSWORD:?FEED_PASSWORD environment variable is required}"
```

### Environment File Support

Load environment variables from `.env` file:

```bash
# .env
FEED_PASSWORD=secure-feed-password
ADMIN_PASSWORD=secure-admin-password
DATA_DIR=/var/lib/yallarhorn
```

Load with:

```bash
yallarhorn --env-file .env
```

Or automatically loads from `.env` in the current directory.

## Migration Guide

### Upgrading Configuration

When upgrading Yallarhorn, check for:

1. **Deprecated fields**: Will show warning but still work
2. **Removed fields**: Will cause validation error
3. **New required fields**: Will cause validation error if missing

### Version Compatibility

Configuration files support a version field for future compatibility:

```yaml
version: "1.0"  # Configuration schema version
```

### Example Migration

If `audio_quality` was renamed to `audio_bitrate`:

```yaml
# Old configuration (version 0.9)
transcode_settings:
  audio_quality: "high"

# New configuration (version 1.0)
transcode_settings:
  audio_bitrate: "192k"
```

Yallarhorn will automatically migrate deprecated fields where possible and warn about manual migrations needed.

---

## See Also

- [Data Model & Schema Design](./data-model.md) - Database schema documentation
- [API Reference](./api-reference.md) - REST API documentation
- [Deployment Guide](./deployment.md) - Production deployment instructions