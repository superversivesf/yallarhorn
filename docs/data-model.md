# Data Model & Schema Design

This document describes the SQLite database schema for Yallarhorn, a podcast server that downloads YouTube videos and serves them as RSS/Atom feeds.

## Overview

Yallarhorn uses SQLite as its datastore for simplicity, portability, and zero-configuration deployment. The database tracks:
- **Channels**: YouTube channels being monitored
- **Episodes**: Downloaded videos with metadata
- **DownloadQueue**: Pending and failed download jobs

## Entity Relationship Diagram

```
┌─────────────────────┐       ┌─────────────────────────┐
│      Channels       │       │        Episodes         │
├─────────────────────┤       ├─────────────────────────┤
│ id (PK)             │───┐   │ id (PK)                 │
│ url                 │   │   │ video_id (UK)           │
│ title               │   │   │ channel_id (FK)         │───┐
│ description         │   │   │ title                   │   │
│ thumbnail_url       │   │   │ description             │   │
│ episode_count_config│   │   │ thumbnail_url           │   │
│ feed_type           │   │   │ duration_seconds        │   │
│ enabled             │   │   │ published_at            │   │
│ last_refresh_at     │   │   │ downloaded_at           │   │
│ created_at          │   │   │ file_path_audio         │   │
│ updated_at          │   │   │ file_path_video         │   │
└─────────────────────┘   │   │ status                  │   │
                          │   │ retry_count             │   │
                          │   │ error_message           │   │
                          │   │ created_at              │   │
                          │   │ updated_at              │   │
                          │   └─────────────────────────┘   │
                          │                                   │
                          │   ┌─────────────────────────┐    │
                          │   │     DownloadQueue       │    │
                          │   ├─────────────────────────┤    │
                          │   │ id (PK)                 │    │
                          │   │ episode_id (FK)         │────┘
                          │   │ priority                │
                          │   │ status                  │
                          │   │ attempts                │
                          │   │ max_attempts            │
                          │   │ last_error              │
                          │   │ next_retry_at           │
                          │   │ created_at              │
                          │   │ updated_at              │
                          │   └─────────────────────────┘
                          │
                          └──────────────────────────────────┘
                                    (1:N relationship)
```

## Table Definitions

### Channels Table

Stores metadata about YouTube channels being monitored for new content.

```sql
CREATE TABLE channels (
    id                    TEXT PRIMARY KEY,
    url                   TEXT NOT NULL UNIQUE,
    title                 TEXT NOT NULL,
    description           TEXT,
    thumbnail_url         TEXT,
    episode_count_config  INTEGER NOT NULL DEFAULT 50,
    feed_type             TEXT NOT NULL DEFAULT 'audio' CHECK(feed_type IN ('audio', 'video', 'both')),
    enabled               INTEGER NOT NULL DEFAULT 1,
    last_refresh_at       TEXT,  -- ISO 8601 timestamp
    created_at            TEXT NOT NULL,  -- ISO 8601 timestamp
    updated_at            TEXT NOT NULL   -- ISO 8601 timestamp
);

-- Index for querying active channels during refresh cycles
CREATE INDEX idx_channels_enabled ON channels(enabled) WHERE enabled = 1;

-- Index for finding channels by last refresh time (for scheduling)
CREATE INDEX idx_channels_last_refresh ON channels(last_refresh_at);
```

#### Field Descriptions

| Field | Type | Description |
|-------|------|-------------|
| `id` | TEXT | Unique identifier (UUID v4 or slug-based) |
| `url` | TEXT | YouTube channel URL (must be unique) |
| `title` | TEXT | Channel display name |
| `description` | TEXT | Channel description (nullable) |
| `thumbnail_url` | TEXT | Channel thumbnail image URL (nullable) |
| `episode_count_config` | INTEGER | Number of episodes to keep in the rolling window (default: 50) |
| `feed_type` | TEXT | Feed output type: 'audio', 'video', or 'both' |
| `enabled` | INTEGER | Whether channel is active (1 = enabled, 0 = disabled) |
| `last_refresh_at` | TEXT | Timestamp of last successful channel refresh |
| `created_at` | TEXT | Record creation timestamp |
| `updated_at` | TEXT | Record last modification timestamp |

#### Constraints

- `url` must be unique across all channels
- `feed_type` must be one of: 'audio', 'video', 'both'
- `episode_count_config` should be a positive integer (recommend 10-200)

---

### Episodes Table

Stores metadata about each downloaded video/episode.

```sql
CREATE TABLE episodes (
    id                TEXT PRIMARY KEY,
    video_id          TEXT NOT NULL UNIQUE,
    channel_id        TEXT NOT NULL,
    title             TEXT NOT NULL,
    description       TEXT,
    thumbnail_url     TEXT,
    duration_seconds  INTEGER,
    published_at      TEXT,  -- ISO 8601 timestamp (YouTube publish date)
    downloaded_at     TEXT,  -- ISO 8601 timestamp (local download completion)
    file_path_audio   TEXT,  -- Relative path to audio file (e.g., "channel-slug/audio/video-id.mp3")
    file_path_video   TEXT,  -- Relative path to video file (e.g., "channel-slug/video/video-id.mp4")
    file_size_audio   INTEGER,  -- Size in bytes
    file_size_video   INTEGER,  -- Size in bytes
    status            TEXT NOT NULL DEFAULT 'pending' CHECK(status IN ('pending', 'downloading', 'processing', 'completed', 'failed', 'deleted')),
    retry_count       INTEGER NOT NULL DEFAULT 0,
    error_message     TEXT,
    created_at        TEXT NOT NULL,
    updated_at        TEXT NOT NULL,
    
    FOREIGN KEY (channel_id) REFERENCES channels(id) ON DELETE CASCADE
);

-- Index for finding episodes by channel (most common query pattern)
CREATE INDEX idx_episodes_channel_id ON episodes(channel_id);

-- Index for finding episodes by status (queue processing)
CREATE INDEX idx_episodes_status ON episodes(status);

-- Index for chronological ordering within a channel
CREATE INDEX idx_episodes_channel_published ON episodes(channel_id, published_at DESC);

-- Index for finding episodes needing download
CREATE INDEX idx_episodes_pending ON episodes(status) WHERE status IN ('pending', 'failed');

-- Index for video_id lookups (deduplication check)
CREATE UNIQUE INDEX idx_episodes_video_id ON episodes(video_id);

-- Index for finding downloaded episodes (for feed generation)
CREATE INDEX idx_episodes_downloaded ON episodes(downloaded_at) WHERE downloaded_at IS NOT NULL;
```

#### Field Descriptions

| Field | Type | Description |
|-------|------|-------------|
| `id` | TEXT | Unique identifier (UUID v4) |
| `video_id` | TEXT | YouTube video ID (unique, used for deduplication) |
| `channel_id` | TEXT | Foreign key to channels table |
| `title` | TEXT | Episode title |
| `description` | TEXT | Episode description (nullable) |
| `thumbnail_url` | TEXT | Episode thumbnail URL (nullable) |
| `duration_seconds` | INTEGER | Video duration in seconds |
| `published_at` | TEXT | Original YouTube publish timestamp |
| `downloaded_at` | TEXT | When the download completed (nullable until complete) |
| `file_path_audio` | TEXT | Relative path to transcoded audio file |
| `file_path_video` | TEXT | Relative path to transcoded video file |
| `file_size_audio` | INTEGER | Audio file size in bytes (for RSS enclosure) |
| `file_size_video` | INTEGER | Video file size in bytes (for RSS enclosure) |
| `status` | TEXT | Current download/processing status |
| `retry_count` | INTEGER | Number of failed download attempts |
| `error_message` | TEXT | Last error message if status is 'failed' |
| `created_at` | TEXT | Record creation timestamp |
| `updated_at` | TEXT | Record last modification timestamp |

#### Status Values

| Status | Description |
|--------|-------------|
| `pending` | Episode queued for download, not yet started |
| `downloading` | Currently being downloaded by yt-dlp |
| `processing` | Download complete, being transcoded by ffmpeg |
| `completed` | Fully processed and available in feeds |
| `failed` | Download failed after max retries |
| `deleted` | Episode removed from disk (outside rolling window) |

---

### DownloadQueue Table

Manages the download queue with priority and retry logic.

```sql
CREATE TABLE download_queue (
    id            TEXT PRIMARY KEY,
    episode_id    TEXT NOT NULL UNIQUE,
    priority      INTEGER NOT NULL DEFAULT 5,  -- 1 (highest) to 10 (lowest)
    status        TEXT NOT NULL DEFAULT 'pending' CHECK(status IN ('pending', 'in_progress', 'completed', 'failed', 'cancelled')),
    attempts      INTEGER NOT NULL DEFAULT 0,
    max_attempts  INTEGER NOT NULL DEFAULT 3,
    last_error    TEXT,
    next_retry_at TEXT,  -- ISO 8601 timestamp for retry scheduling
    created_at    TEXT NOT NULL,
    updated_at    TEXT NOT NULL,
    
    FOREIGN KEY (episode_id) REFERENCES episodes(id) ON DELETE CASCADE
);

-- Index for processing queue in priority order
CREATE INDEX idx_queue_pending ON download_queue(status, priority, created_at) 
    WHERE status = 'pending';

-- Index for finding stuck in-progress downloads
CREATE INDEX idx_queue_in_progress ON download_queue(status, updated_at) 
    WHERE status = 'in_progress';

-- Index for retry scheduling
CREATE INDEX idx_queue_retry ON download_queue(next_retry_at) 
    WHERE status = 'pending' AND next_retry_at IS NOT NULL;
```

#### Field Descriptions

| Field | Type | Description |
|-------|------|-------------|
| `id` | TEXT | Unique identifier (UUID v4) |
| `episode_id` | TEXT | Foreign key to episodes table (unique, one-to-one) |
| `priority` | INTEGER | Download priority (1-10, lower = higher priority) |
| `status` | TEXT | Current queue item status |
| `attempts` | INTEGER | Number of download attempts made |
| `max_attempts` | INTEGER | Maximum retries before marking as failed |
| `last_error` | TEXT | Last error message from failed attempt |
| `next_retry_at` | TEXT | Scheduled time for next retry (exponential backoff) |
| `created_at` | TEXT | Record creation timestamp |
| `updated_at` | TEXT | Record last modification timestamp |

#### Priority Values

| Priority | Description |
|----------|-------------|
| 1-2 | High priority (new episodes from active channels) |
| 3-5 | Normal priority (standard queue) |
| 6-8 | Low priority (backfill, older episodes) |
| 9-10 | Background/bulk priority |

#### Retry Strategy

Exponential backoff for failed downloads:
- Attempt 1: Immediate
- Attempt 2: Wait 5 minutes
- Attempt 3: Wait 30 minutes
- After max attempts: Mark episode as 'failed'

---

## Complete Schema SQL

```sql
-- Enable foreign key support (must be set per-connection)
PRAGMA foreign_keys = ON;

-- Enable Write-Ahead Logging for better concurrency
PRAGMA journal_mode = WAL;

-- Set busy timeout for concurrent access
PRAGMA busy_timeout = 5000;

-- ============================================
-- Channels Table
-- ============================================
CREATE TABLE IF NOT EXISTS channels (
    id                    TEXT PRIMARY KEY,
    url                   TEXT NOT NULL UNIQUE,
    title                 TEXT NOT NULL,
    description           TEXT,
    thumbnail_url         TEXT,
    episode_count_config  INTEGER NOT NULL DEFAULT 50,
    feed_type             TEXT NOT NULL DEFAULT 'audio' CHECK(feed_type IN ('audio', 'video', 'both')),
    enabled               INTEGER NOT NULL DEFAULT 1,
    last_refresh_at       TEXT,
    created_at            TEXT NOT NULL,
    updated_at            TEXT NOT NULL
);

CREATE INDEX IF NOT EXISTS idx_channels_enabled ON channels(enabled) WHERE enabled = 1;
CREATE INDEX IF NOT EXISTS idx_channels_last_refresh ON channels(last_refresh_at);

-- ============================================
-- Episodes Table
-- ============================================
CREATE TABLE IF NOT EXISTS episodes (
    id                TEXT PRIMARY KEY,
    video_id          TEXT NOT NULL UNIQUE,
    channel_id        TEXT NOT NULL,
    title             TEXT NOT NULL,
    description       TEXT,
    thumbnail_url     TEXT,
    duration_seconds  INTEGER,
    published_at      TEXT,
    downloaded_at     TEXT,
    file_path_audio   TEXT,
    file_path_video   TEXT,
    file_size_audio   INTEGER,
    file_size_video   INTEGER,
    status            TEXT NOT NULL DEFAULT 'pending' CHECK(status IN ('pending', 'downloading', 'processing', 'completed', 'failed', 'deleted')),
    retry_count       INTEGER NOT NULL DEFAULT 0,
    error_message     TEXT,
    created_at        TEXT NOT NULL,
    updated_at        TEXT NOT NULL,
    
    FOREIGN KEY (channel_id) REFERENCES channels(id) ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS idx_episodes_channel_id ON episodes(channel_id);
CREATE INDEX IF NOT EXISTS idx_episodes_status ON episodes(status);
CREATE INDEX IF NOT EXISTS idx_episodes_channel_published ON episodes(channel_id, published_at DESC);
CREATE INDEX IF NOT EXISTS idx_episodes_pending ON episodes(status) WHERE status IN ('pending', 'failed');
CREATE UNIQUE INDEX IF NOT EXISTS idx_episodes_video_id ON episodes(video_id);
CREATE INDEX IF NOT EXISTS idx_episodes_downloaded ON episodes(downloaded_at) WHERE downloaded_at IS NOT NULL;

-- ============================================
-- DownloadQueue Table
-- ============================================
CREATE TABLE IF NOT EXISTS download_queue (
    id            TEXT PRIMARY KEY,
    episode_id    TEXT NOT NULL UNIQUE,
    priority      INTEGER NOT NULL DEFAULT 5,
    status        TEXT NOT NULL DEFAULT 'pending' CHECK(status IN ('pending', 'in_progress', 'completed', 'failed', 'cancelled')),
    attempts      INTEGER NOT NULL DEFAULT 0,
    max_attempts  INTEGER NOT NULL DEFAULT 3,
    last_error    TEXT,
    next_retry_at TEXT,
    created_at    TEXT NOT NULL,
    updated_at    TEXT NOT NULL,
    
    FOREIGN KEY (episode_id) REFERENCES episodes(id) ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS idx_queue_pending ON download_queue(status, priority, created_at) 
    WHERE status = 'pending';
CREATE INDEX IF NOT EXISTS idx_queue_in_progress ON download_queue(status, updated_at) 
    WHERE status = 'in_progress';
CREATE INDEX IF NOT EXISTS idx_queue_retry ON download_queue(next_retry_at) 
    WHERE status = 'pending' AND next_retry_at IS NOT NULL;

-- ============================================
-- Schema Version Tracking
-- ============================================
CREATE TABLE IF NOT EXISTS schema_version (
    version     INTEGER PRIMARY KEY,
    applied_at  TEXT NOT NULL,
    description TEXT
);

INSERT INTO schema_version (version, applied_at, description) 
VALUES (1, datetime('now'), 'Initial schema with channels, episodes, download_queue');
```

---

## Data Access Patterns

### Common Queries

#### 1. Get All Active Channels for Refresh

```sql
SELECT id, url, title, episode_count_config, feed_type, last_refresh_at
FROM channels
WHERE enabled = 1
ORDER BY last_refresh_at ASC NULLS FIRST;
```

#### 2. Get Episodes for a Channel's Feed (Rolling Window)

```sql
SELECT e.id, e.video_id, e.title, e.description, e.thumbnail_url,
       e.duration_seconds, e.published_at, e.file_path_audio, 
       e.file_path_video, e.file_size_audio, e.file_size_video
FROM episodes e
WHERE e.channel_id = ? 
  AND e.status = 'completed'
  AND e.downloaded_at IS NOT NULL
ORDER BY e.published_at DESC
LIMIT ?;  -- episode_count_config from channel
```

#### 3. Get Next Download Job from Queue

```sql
SELECT dq.id, dq.episode_id, dq.priority, dq.attempts,
       e.video_id, e.channel_id, c.url as channel_url
FROM download_queue dq
JOIN episodes e ON dq.episode_id = e.id
JOIN channels c ON e.channel_id = c.id
WHERE dq.status = 'pending'
  AND (dq.next_retry_at IS NULL OR dq.next_retry_at <= datetime('now'))
ORDER BY dq.priority ASC, dq.created_at ASC
LIMIT 1;
```

#### 4. Check for Duplicate Video

```sql
SELECT id, status, channel_id
FROM episodes
WHERE video_id = ?;
```

#### 5. Get Episodes to Delete (Outside Rolling Window)

```sql
SELECT e.id, e.file_path_audio, e.file_path_video
FROM episodes e
JOIN channels c ON e.channel_id = c.id
WHERE e.channel_id = ?
  AND e.status = 'completed'
  AND e.downloaded_at IS NOT NULL
ORDER BY e.published_at DESC
OFFSET ?;  -- episode_count_config
```

#### 6. Get Download Statistics

```sql
SELECT 
    COUNT(CASE WHEN status = 'completed' THEN 1 END) as completed,
    COUNT(CASE WHEN status = 'pending' THEN 1 END) as pending,
    COUNT(CASE WHEN status = 'downloading' THEN 1 END) as downloading,
    COUNT(CASE WHEN status = 'processing' THEN 1 END) as processing,
    COUNT(CASE WHEN status = 'failed' THEN 1 END) as failed
FROM episodes;
```

#### 7. Get Queue Status

```sql
SELECT 
    status,
    COUNT(*) as count,
    MIN(created_at) as oldest,
    MAX(created_at) as newest
FROM download_queue
GROUP BY status;
```

---

## Entity Relationships

### Channels → Episodes (1:N)

- A channel can have many episodes
- Each episode belongs to exactly one channel
- **Cascade Delete**: When a channel is deleted, all associated episodes are deleted

### Episodes → DownloadQueue (1:1)

- Each episode has at most one download queue entry
- Queue entry tracks download progress and retries
- **Cascade Delete**: When an episode is deleted, its queue entry is deleted

### Referential Integrity

```sql
-- Example: Cannot insert episode with non-existent channel_id
INSERT INTO episodes (id, video_id, channel_id, title, created_at, updated_at)
VALUES ('ep-001', 'abc123', 'non-existent-channel', 'Title', datetime('now'), datetime('now'));
-- Result: FOREIGN KEY constraint failed

-- Example: Cannot delete channel with episodes (without CASCADE)
-- Solution: Use ON DELETE CASCADE or manually delete episodes first
```

---

## Migration Strategy

### Version-Based Migrations

Each schema change creates a new migration file:

```
/migrations/
├── 001_initial_schema.sql
├── 002_add_episode_published_at_index.sql
├── 003_add_feed_subtype_column.sql
└── ...
```

### Migration Application

```sql
-- In application startup
BEGIN TRANSACTION;

-- Check current version
SELECT version FROM schema_version ORDER BY version DESC LIMIT 1;

-- Apply each pending migration in order
-- Example migration file:
INSERT INTO schema_version (version, applied_at, description)
VALUES (2, datetime('now'), 'Add episode published_at index');

CREATE INDEX idx_episodes_published ON episodes(published_at DESC);

COMMIT;
```

---

## Best Practices

### Timestamp Handling

- Use ISO 8601 format: `YYYY-MM-DDTHH:MM:SS.sssZ`
- Use SQLite's `datetime('now')` for current time
- Store in UTC for consistency

```sql
-- Insert with proper timestamps
INSERT INTO channels (id, url, title, created_at, updated_at)
VALUES ('ch-001', 'https://youtube.com/@channel', 'My Channel', 
        datetime('now'), datetime('now'));
```

### Soft Deletes

For auditability, consider soft deletes instead of hard deletes:

```sql
-- Add to channels table
ALTER TABLE channels ADD COLUMN deleted_at TEXT;

-- Soft delete
UPDATE channels SET deleted_at = datetime('now'), enabled = 0 WHERE id = ?;

-- Query excludes soft-deleted
SELECT * FROM channels WHERE deleted_at IS NULL;
```

### Connection Settings

Always set these pragmas on each connection:

```csharp
// In C# / EF Core
protected override void OnConfiguring(DbContextOptionsBuilder options)
{
    options.UseSqlite(connectionString, o => 
    {
        // Handled automatically by EF Core, but for raw connections:
    });
}

// For raw ADO.NET connections
using var connection = new SqliteConnection(connectionString);
await connection.OpenAsync();
using var cmd = connection.CreateCommand();
cmd.CommandText = "PRAGMA journal_mode=WAL; PRAGMA foreign_keys=ON; PRAGMA busy_timeout=5000;";
await cmd.ExecuteNonQueryAsync();
```

---

## Performance Considerations

### Indexed Queries

All common query patterns have corresponding indexes:
- Episode lookup by channel (feed generation)
- Episode lookup by status (queue processing)
- Video deduplication check
- Channel refresh scheduling

### Partial Indexes

Used to reduce index size for filtered queries:

```sql
-- Only index pending/failed episodes (not completed ones)
CREATE INDEX idx_episodes_pending ON episodes(status) WHERE status IN ('pending', 'failed');
```

### WAL Mode

Write-Ahead Logging enables:
- Concurrent reads during writes
- Better performance for read-heavy workloads
- No database locks during reads

### Connection Pooling

For ASP.NET Core, connection pooling is automatic with `Microsoft.Data.Sqlite`.

---

## Testing Considerations

### Test Data Seed

```sql
-- Insert test channel
INSERT INTO channels (id, url, title, episode_count_config, feed_type, created_at, updated_at)
VALUES ('test-channel-1', 'https://youtube.com/@test', 'Test Channel', 10, 'both', 
        datetime('now'), datetime('now'));

-- Insert test episodes
INSERT INTO episodes (id, video_id, channel_id, title, status, created_at, updated_at)
VALUES 
    ('ep-1', 'vid001', 'test-channel-1', 'Episode 1', 'completed', datetime('now'), datetime('now')),
    ('ep-2', 'vid002', 'test-channel-1', 'Episode 2', 'pending', datetime('now'), datetime('now'));

-- Insert test queue item
INSERT INTO download_queue (id, episode_id, priority, status, created_at, updated_at)
VALUES ('dq-1', 'ep-2', 5, 'pending', datetime('now'), datetime('now'));
```

### Constraints Testing

```sql
-- Test unique constraint
INSERT INTO episodes (id, video_id, channel_id, title, created_at, updated_at)
VALUES ('ep-3', 'vid001', 'test-channel-1', 'Duplicate', datetime('now'), datetime('now'));
-- Expected: UNIQUE constraint failed: episodes.video_id

-- Test foreign key constraint
INSERT INTO episodes (id, video_id, channel_id, title, created_at, updated_at)
VALUES ('ep-3', 'vid003', 'non-existent', 'Orphan', datetime('now'), datetime('now'));
-- Expected: FOREIGN KEY constraint failed

-- Test check constraint
INSERT INTO episodes (id, video_id, channel_id, title, status, created_at, updated_at)
VALUES ('ep-3', 'vid003', 'test-channel-1', 'Invalid Status', 'invalid_status', datetime('now'), datetime('now'));
-- Expected: CHECK constraint failed
```

---

## Future Considerations

### Potential Schema Extensions

1. **Playlists Support**: Add a `playlists` table for YouTube playlist sources
2. **Tags/Categories**: Many-to-many relationship for episode categorization
3. **Watch History**: Track playback progress per episode
4. **Schedule Configuration**: Per-channel refresh intervals
5. **Storage Metrics**: Track disk usage per channel

### Schema Evolution Patterns

```sql
-- Example: Add playlist support
CREATE TABLE playlists (
    id          TEXT PRIMARY KEY,
    channel_id  TEXT,
    url         TEXT NOT NULL UNIQUE,
    title       TEXT NOT NULL,
    created_at  TEXT NOT NULL,
    updated_at  TEXT NOT NULL,
    FOREIGN KEY (channel_id) REFERENCES channels(id) ON DELETE SET NULL
);

-- Modify episodes to support playlist source
ALTER TABLE episodes ADD COLUMN playlist_id TEXT REFERENCES playlists(id);
```

---

## References

- [SQLite Documentation](https://www.sqlite.org/docs.html)
- [SQLite Foreign Key Support](https://www.sqlite.org/foreignkeys.html)
- [SQLite WAL Mode](https://www.sqlite.org/wal.html)
- [SQLite Query Planning](https://www.sqlite.org/queryplanner.html)
- [Microsoft.Data.Sqlite Documentation](https://docs.microsoft.com/en-us/dotnet/standard/data/sqlite/)