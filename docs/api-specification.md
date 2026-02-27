# Management API Specification

This document defines the REST API for Yallarhorn's management interface. The API provides endpoints for channel management, episode browsing, system status monitoring, and manual refresh triggers.

## Table of Contents

- [Overview](#overview)
- [Authentication](#authentication)
- [Base URL](#base-url)
- [Request/Response Format](#requestresponse-format)
- [Error Handling](#error-handling)
- [Rate Limiting](#rate-limiting)
- [Endpoints](#endpoints)
  - [Channel Management](#channel-management)
  - [Episode Management](#episode-management)
  - [System Status](#system-status)
  - [Manual Triggers](#manual-triggers)
- [Data Types](#data-types)
- [OpenAPI Specification](#openapi-specification)

---

## Overview

The Management API enables programmatic control of Yallarhorn's core functionality:

- **Channel Management**: Create, read, update, and delete monitored YouTube channels
- **Episode Management**: Browse and delete downloaded episodes
- **System Status**: Monitor download queue, storage usage, and application health
- **Manual Triggers**: Force refresh channels or individual downloads

### API Design Principles

1. **RESTful**: Resources are nouns, actions are HTTP verbs
2. **Stateless**: Each request contains all necessary information
3. **Predictable**: Consistent URL patterns and response structures
4. **Versioned**: API version included in URL path (`/api/v1/...`)
5. **Hypermedia**: Responses include related resource links where appropriate

---

## Authentication

All Management API endpoints require **HTTP Basic Authentication**.

### Configuration

Authentication is configured in `yallarhorn.yaml`:

```yaml
auth:
  admin_auth:
    enabled: true
    username: "admin"
    password: "${ADMIN_PASSWORD}"
```

### Request Format

Include credentials in the `Authorization` header:

```
Authorization: Basic <base64(username:password)>
```

Example:
```http
GET /api/v1/channels HTTP/1.1
Host: localhost:8080
Authorization: Basic YWRtaW46c2VjcmV0cGFzc3dvcmQ=
```

### Authentication Errors

| Status Code | Description |
|-------------|-------------|
| `401 Unauthorized` | Missing or invalid credentials |
| `403 Forbidden` | Valid credentials, insufficient permissions |

Example error response:
```json
{
  "error": {
    "code": "UNAUTHORIZED",
    "message": "Authentication required",
    "details": "Include valid credentials in the Authorization header"
  }
}
```

---

## Base URL

```
http://localhost:8080/api/v1
```

The base URL can be customized via the server configuration:

```yaml
server:
  host: "0.0.0.0"
  port: 8080
  base_url: "http://your-domain.com"
```

---

## Request/Response Format

### Content Type

All requests and responses use JSON:

```
Content-Type: application/json
```

### Request Headers

| Header | Required | Description |
|--------|----------|-------------|
| `Authorization` | Yes | HTTP Basic Auth credentials |
| `Content-Type` | Conditional | Required for POST/PUT/PATCH requests |
| `Accept` | No | Defaults to `application/json` |
| `X-Request-ID` | No | Client-provided request ID for tracing |

### Response Headers

| Header | Description |
|--------|-------------|
| `Content-Type` | Always `application/json` |
| `X-Request-ID` | Unique request identifier for debugging |
| `X-RateLimit-Limit` | Maximum requests per window |
| `X-RateLimit-Remaining` | Remaining requests in current window |
| `X-RateLimit-Reset` | Unix timestamp when the window resets |

### Timestamp Format

All timestamps use **ISO 8601** format in UTC:

```
2024-01-15T10:30:00.000Z
```

---

## Error Handling

### Error Response Structure

All error responses follow a consistent structure:

```json
{
  "error": {
    "code": "ERROR_CODE",
    "message": "Human-readable error message",
    "details": "Additional context or guidance",
    "field": "field_name",
    "request_id": "req_abc123xyz"
  }
}
```

### Error Codes

| Code | HTTP Status | Description |
|------|-------------|-------------|
| `UNAUTHORIZED` | 401 | Missing or invalid authentication |
| `FORBIDDEN` | 403 | Insufficient permissions |
| `NOT_FOUND` | 404 | Resource does not exist |
| `METHOD_NOT_ALLOWED` | 405 | HTTP method not supported for this endpoint |
| `CONFLICT` | 409 | Resource conflict (e.g., duplicate) |
| `VALIDATION_ERROR` | 422 | Request validation failed |
| `RATE_LIMITED` | 429 | Too many requests |
| `INTERNAL_ERROR` | 500 | Unexpected server error |
| `SERVICE_UNAVAILABLE` | 503 | Service temporarily unavailable |

### Common Error Examples

#### Not Found
```json
{
  "error": {
    "code": "NOT_FOUND",
    "message": "Channel not found",
    "details": "No channel exists with ID 'ch-abc123'",
    "request_id": "req_xyz789"
  }
}
```

#### Validation Error
```json
{
  "error": {
    "code": "VALIDATION_ERROR",
    "message": "Invalid request body",
    "details": "Field validation failed",
    "field": "url",
    "request_id": "req_def456"
  }
}
```

#### Conflict
```json
{
  "error": {
    "code": "CONFLICT",
    "message": "Channel already exists",
    "details": "A channel with URL 'https://youtube.com/@existing' already exists",
    "request_id": "req_ghi012"
  }
}
```

---

## Rate Limiting

### Limits

| Endpoint Category | Rate Limit | Window |
|-------------------|------------|--------|
| Read operations (GET) | 100 requests | 1 minute |
| Write operations (POST/PUT/DELETE) | 30 requests | 1 minute |
| Trigger operations | 10 requests | 1 minute |

### Rate Limit Headers

```http
X-RateLimit-Limit: 100
X-RateLimit-Remaining: 95
X-RateLimit-Reset: 1705312800
```

### Rate Limit Exceeded

```json
{
  "error": {
    "code": "RATE_LIMITED",
    "message": "Rate limit exceeded",
    "details": "Maximum 100 requests per minute. Retry after 45 seconds.",
    "request_id": "req_jkl345"
  }
}
```

---

## Endpoints

### Channel Management

#### List Channels

List all configured channels with optional filtering and pagination.

```http
GET /api/v1/channels
```

##### Query Parameters

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `page` | integer | 1 | Page number (1-indexed) |
| `limit` | integer | 50 | Items per page (max: 100) |
| `enabled` | boolean | - | Filter by enabled status |
| `feed_type` | string | - | Filter by feed type: `audio`, `video`, `both` |
| `sort` | string | `created_at` | Sort field: `title`, `created_at`, `last_refresh_at` |
| `order` | string | `desc` | Sort order: `asc`, `desc` |

##### Response

```json
{
  "data": [
    {
      "id": "ch-abc123",
      "url": "https://www.youtube.com/@techtalk",
      "title": "Tech Talk Weekly",
      "description": "Technology news and discussion",
      "thumbnail_url": "https://example.com/thumb.jpg",
      "episode_count_config": 50,
      "feed_type": "audio",
      "enabled": true,
      "episode_count": 42,
      "last_refresh_at": "2024-01-15T10:30:00.000Z",
      "created_at": "2024-01-01T00:00:00.000Z",
      "updated_at": "2024-01-15T10:30:00.000Z",
      "_links": {
        "self": "/api/v1/channels/ch-abc123",
        "episodes": "/api/v1/channels/ch-abc123/episodes",
        "refresh": "/api/v1/channels/ch-abc123/refresh"
      }
    }
  ],
  "pagination": {
    "page": 1,
    "limit": 50,
    "total_items": 5,
    "total_pages": 1
  },
  "_links": {
    "self": "/api/v1/channels?page=1&limit=50",
    "next": null,
    "prev": null
  }
}
```

##### Status Codes

| Code | Description |
|------|-------------|
| 200 | Channels retrieved successfully |
| 401 | Authentication required |

---

#### Get Channel

Retrieve a single channel by ID.

```http
GET /api/v1/channels/{id}
```

##### Path Parameters

| Parameter | Type | Description |
|-----------|------|-------------|
| `id` | string | Channel ID |

##### Response

```json
{
  "data": {
    "id": "ch-abc123",
    "url": "https://www.youtube.com/@techtalk",
    "title": "Tech Talk Weekly",
    "description": "Technology news and discussion",
    "thumbnail_url": "https://example.com/thumb.jpg",
    "episode_count_config": 50,
    "feed_type": "audio",
    "enabled": true,
    "episode_count": 42,
    "last_refresh_at": "2024-01-15T10:30:00.000Z",
    "created_at": "2024-01-01T00:00:00.000Z",
    "updated_at": "2024-01-15T10:30:00.000Z",
    "_links": {
      "self": "/api/v1/channels/ch-abc123",
      "episodes": "/api/v1/channels/ch-abc123/episodes",
      "refresh": "/api/v1/channels/ch-abc123/refresh"
    }
  }
}
```

##### Status Codes

| Code | Description |
|------|-------------|
| 200 | Channel retrieved successfully |
| 401 | Authentication required |
| 404 | Channel not found |

---

#### Create Channel

Add a new YouTube channel to monitor.

```http
POST /api/v1/channels
```

##### Request Body

```json
{
  "url": "https://www.youtube.com/@newchannel",
  "title": "Override Title",
  "description": "Custom description override",
  "episode_count_config": 30,
  "feed_type": "audio",
  "enabled": true
}
```

##### Request Fields

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `url` | string | Yes | YouTube channel URL |
| `title` | string | No | Override channel title (fetched from YouTube if not provided) |
| `description` | string | No | Override description |
| `episode_count_config` | integer | No | Number of episodes to keep (default: 50, range: 1-1000) |
| `feed_type` | string | No | Output type: `audio`, `video`, `both` (default: `audio`) |
| `enabled` | boolean | No | Enable monitoring (default: `true`) |

##### Response

```json
{
  "data": {
    "id": "ch-xyz789",
    "url": "https://www.youtube.com/@newchannel",
    "title": "New Channel",
    "description": "Custom description override",
    "thumbnail_url": "https://example.com/new-thumb.jpg",
    "episode_count_config": 30,
    "feed_type": "audio",
    "enabled": true,
    "episode_count": 0,
    "last_refresh_at": null,
    "created_at": "2024-01-15T12:00:00.000Z",
    "updated_at": "2024-01-15T12:00:00.000Z",
    "_links": {
      "self": "/api/v1/channels/ch-xyz789",
      "episodes": "/api/v1/channels/ch-xyz789/episodes",
      "refresh": "/api/v1/channels/ch-xyz789/refresh"
    }
  },
  "message": "Channel created successfully. Initial refresh scheduled."
}
```

##### Status Codes

| Code | Description |
|------|-------------|
| 201 | Channel created successfully |
| 400 | Invalid request body |
| 401 | Authentication required |
| 409 | Channel URL already exists |
| 422 | Validation error |

---

#### Update Channel

Update an existing channel's configuration.

```http
PUT /api/v1/channels/{id}
```

##### Path Parameters

| Parameter | Type | Description |
|-----------|------|-------------|
| `id` | string | Channel ID |

##### Request Body

```json
{
  "title": "Updated Channel Title",
  "episode_count_config": 100,
  "feed_type": "both",
  "enabled": false
}
```

##### Request Fields

All fields are optional. Only provided fields are updated.

| Field | Type | Description |
|-------|------|-------------|
| `title` | string | Update channel title |
| `description` | string | Update description |
| `episode_count_config` | integer | Update episode retention count |
| `feed_type` | string | Update feed output type |
| `enabled` | boolean | Enable/disable monitoring |

##### Response

```json
{
  "data": {
    "id": "ch-abc123",
    "url": "https://www.youtube.com/@techtalk",
    "title": "Updated Channel Title",
    "description": "Technology news and discussion",
    "thumbnail_url": "https://example.com/thumb.jpg",
    "episode_count_config": 100,
    "feed_type": "both",
    "enabled": false,
    "episode_count": 42,
    "last_refresh_at": "2024-01-15T10:30:00.000Z",
    "created_at": "2024-01-01T00:00:00.000Z",
    "updated_at": "2024-01-15T14:00:00.000Z",
    "_links": {
      "self": "/api/v1/channels/ch-abc123",
      "episodes": "/api/v1/channels/ch-abc123/episodes",
      "refresh": "/api/v1/channels/ch-abc123/refresh"
    }
  },
  "message": "Channel updated successfully"
}
```

##### Status Codes

| Code | Description |
|------|-------------|
| 200 | Channel updated successfully |
| 400 | Invalid request body |
| 401 | Authentication required |
| 404 | Channel not found |
| 422 | Validation error |

---

#### Delete Channel

Remove a channel and all associated episodes.

```http
DELETE /api/v1/channels/{id}
```

##### Path Parameters

| Parameter | Type | Description |
|-----------|------|-------------|
| `id` | string | Channel ID |

##### Query Parameters

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `delete_files` | boolean | `true` | Delete downloaded files from disk |

##### Response

```json
{
  "message": "Channel deleted successfully",
  "deleted": {
    "channel_id": "ch-abc123",
    "episodes_deleted": 42,
    "files_deleted": 84,
    "bytes_freed": 5368709120
  }
}
```

##### Status Codes

| Code | Description |
|------|-------------|
| 200 | Channel deleted successfully |
| 401 | Authentication required |
| 404 | Channel not found |

---

### Episode Management

#### List Channel Episodes

List all episodes for a specific channel.

```http
GET /api/v1/channels/{channel_id}/episodes
```

##### Path Parameters

| Parameter | Type | Description |
|-----------|------|-------------|
| `channel_id` | string | Channel ID |

##### Query Parameters

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `page` | integer | 1 | Page number |
| `limit` | integer | 50 | Items per page (max: 100) |
| `status` | string | - | Filter by status: `pending`, `downloading`, `processing`, `completed`, `failed`, `deleted` |
| `sort` | string | `published_at` | Sort field: `title`, `published_at`, `downloaded_at`, `duration_seconds` |
| `order` | string | `desc` | Sort order |

##### Response

```json
{
  "data": [
    {
      "id": "ep-def456",
      "video_id": "yt_video_123",
      "channel_id": "ch-abc123",
      "title": "Episode Title Here",
      "description": "Episode description...",
      "thumbnail_url": "https://example.com/ep-thumb.jpg",
      "duration_seconds": 3600,
      "published_at": "2024-01-10T09:00:00.000Z",
      "downloaded_at": "2024-01-10T10:30:00.000Z",
      "status": "completed",
      "file_size_audio": 52428800,
      "file_size_video": 524288000,
      "_links": {
        "self": "/api/v1/episodes/ep-def456",
        "channel": "/api/v1/channels/ch-abc123"
      }
    }
  ],
  "pagination": {
    "page": 1,
    "limit": 50,
    "total_items": 42,
    "total_pages": 1
  },
  "_links": {
    "self": "/api/v1/channels/ch-abc123/episodes?page=1&limit=50",
    "channel": "/api/v1/channels/ch-abc123"
  }
}
```

##### Status Codes

| Code | Description |
|------|-------------|
| 200 | Episodes retrieved successfully |
| 401 | Authentication required |
| 404 | Channel not found |

---

#### Get Episode

Retrieve details for a single episode.

```http
GET /api/v1/episodes/{id}
```

##### Path Parameters

| Parameter | Type | Description |
|-----------|------|-------------|
| `id` | string | Episode ID |

##### Response

```json
{
  "data": {
    "id": "ep-def456",
    "video_id": "yt_video_123",
    "channel_id": "ch-abc123",
    "title": "Episode Title Here",
    "description": "Detailed episode description...",
    "thumbnail_url": "https://example.com/ep-thumb.jpg",
    "duration_seconds": 3600,
    "published_at": "2024-01-10T09:00:00.000Z",
    "downloaded_at": "2024-01-10T10:30:00.000Z",
    "status": "completed",
    "file_path_audio": "tech-talk/audio/yt_video_123.mp3",
    "file_path_video": "tech-talk/video/yt_video_123.mp4",
    "file_size_audio": 52428800,
    "file_size_video": 524288000,
    "retry_count": 0,
    "error_message": null,
    "created_at": "2024-01-10T09:05:00.000Z",
    "updated_at": "2024-01-10T10:30:00.000Z",
    "_links": {
      "self": "/api/v1/episodes/ep-def456",
      "channel": "/api/v1/channels/ch-abc123",
      "audio_file": "/feeds/tech-talk/audio/yt_video_123.mp3",
      "video_file": "/feeds/tech-talk/video/yt_video_123.mp4"
    }
  }
}
```

##### Status Codes

| Code | Description |
|------|-------------|
| 200 | Episode retrieved successfully |
| 401 | Authentication required |
| 404 | Episode not found |

---

#### Delete Episode

Delete an episode and its associated files.

```http
DELETE /api/v1/episodes/{id}
```

##### Path Parameters

| Parameter | Type | Description |
|-----------|------|-------------|
| `id` | string | Episode ID |

##### Query Parameters

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `delete_files` | boolean | `true` | Delete files from disk |

##### Response

```json
{
  "message": "Episode deleted successfully",
  "deleted": {
    "episode_id": "ep-def456",
    "files_deleted": 2,
    "bytes_freed": 577532928
  }
}
```

##### Status Codes

| Code | Description |
|------|-------------|
| 200 | Episode deleted successfully |
| 401 | Authentication required |
| 404 | Episode not found |
| 409 | Episode is currently being downloaded |

---

### System Status

#### Get System Status

Retrieve overall system health and statistics.

```http
GET /api/v1/status
```

##### Response

```json
{
  "data": {
    "version": "1.0.0",
    "uptime_seconds": 604800,
    "status": "healthy",
    "checks": {
      "database": "healthy",
      "storage": "healthy",
      "downloads": "healthy"
    },
    "statistics": {
      "channels": {
        "total": 5,
        "enabled": 4,
        "disabled": 1
      },
      "episodes": {
        "total": 250,
        "completed": 230,
        "pending": 10,
        "downloading": 2,
        "processing": 3,
        "failed": 5,
        "deleted": 0
      },
      "storage": {
        "total_bytes": 107374182400,
        "used_bytes": 53687091200,
        "free_bytes": 53687091200,
        "usage_percent": 50.0
      },
      "downloads": {
        "total_bytes_downloaded": 1073741824000,
        "total_files_downloaded": 500
      }
    },
    "last_refresh_at": "2024-01-15T10:00:00.000Z",
    "next_refresh_at": "2024-01-15T11:00:00.000Z",
    "config": {
      "poll_interval": 3600,
      "max_concurrent_downloads": 3,
      "download_dir": "./downloads"
    }
  },
  "_links": {
    "self": "/api/v1/status",
    "queue": "/api/v1/queue",
    "health": "/api/v1/health"
  }
}
```

##### Status Values

| Status | Description |
|--------|-------------|
| `healthy` | All systems operational |
| `degraded` | Some non-critical issues |
| `unhealthy` | Critical issues affecting functionality |

##### Status Codes

| Code | Description |
|------|-------------|
| 200 | Status retrieved successfully |
| 401 | Authentication required |
| 503 | Service unhealthy |

---

#### Get Download Queue

Retrieve the current download queue status.

```http
GET /api/v1/queue
```

##### Query Parameters

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `status` | string | - | Filter by queue status: `pending`, `in_progress`, `completed`, `failed`, `cancelled` |
| `page` | integer | 1 | Page number |
| `limit` | integer | 50 | Items per page (max: 100) |

##### Response

```json
{
  "data": {
    "summary": {
      "pending": 10,
      "in_progress": 2,
      "completed": 245,
      "failed": 5,
      "cancelled": 0,
      "total": 262
    },
    "active_downloads": [
      {
        "id": "dq-xyz123",
        "episode_id": "ep-abc456",
        "video_id": "yt_video_cur",
        "status": "in_progress",
        "priority": 5,
        "attempts": 1,
        "progress_percent": 45,
        "download_speed_bps": 5242880,
        "bytes_downloaded": 157286400,
        "bytes_total": 349175808,
        "started_at": "2024-01-15T12:00:00.000Z",
        "estimated_completion": "2024-01-15T12:05:00.000Z"
      }
    ],
    "pending_items": [
      {
        "id": "dq-pending1",
        "episode_id": "ep-pending1",
        "video_id": "yt_video_pending",
        "status": "pending",
        "priority": 5,
        "attempts": 0,
        "created_at": "2024-01-15T11:55:00.000Z"
      }
    ],
    "failed_items": [
      {
        "id": "dq-failed1",
        "episode_id": "ep-failed1",
        "video_id": "yt_video_failed",
        "status": "failed",
        "priority": 5,
        "attempts": 3,
        "max_attempts": 3,
        "last_error": "Network timeout after 30 seconds",
        "next_retry_at": null,
        "created_at": "2024-01-15T10:00:00.000Z",
        "updated_at": "2024-01-15T10:30:00.000Z"
      }
    ]
  },
  "pagination": {
    "page": 1,
    "limit": 50,
    "total_items": 262,
    "total_pages": 6
  },
  "_links": {
    "self": "/api/v1/queue",
    "status": "/api/v1/status"
  }
}
```

##### Status Codes

| Code | Description |
|------|-------------|
| 200 | Queue status retrieved successfully |
| 401 | Authentication required |

---

#### Health Check

Lightweight endpoint for load balancers and monitoring.

```http
GET /api/v1/health
```

##### Response

```json
{
  "status": "healthy",
  "version": "1.0.0",
  "timestamp": "2024-01-15T12:00:00.000Z"
}
```

##### Status Codes

| Code | Description |
|------|-------------|
| 200 | Service is healthy |
| 503 | Service is unhealthy |

---

### Manual Triggers

#### Refresh Channel

Trigger an immediate refresh for a specific channel.

```http
POST /api/v1/channels/{id}/refresh
```

##### Path Parameters

| Parameter | Type | Description |
|-----------|------|-------------|
| `id` | string | Channel ID |

##### Request Body (Optional)

```json
{
  "force": false,
  "download_new": true
}
```

##### Request Fields

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| `force` | boolean | `false` | Refresh even if recently refreshed |
| `download_new` | boolean | `true` | Queue new episodes for download |

##### Response

```json
{
  "message": "Channel refresh initiated",
  "data": {
    "channel_id": "ch-abc123",
    "refresh_id": "refresh-xyz789",
    "status": "in_progress",
    "started_at": "2024-01-15T12:00:00.000Z",
    "episodes_found": 0,
    "episodes_queued": 0
  },
  "_links": {
    "channel": "/api/v1/channels/ch-abc123",
    "status": "/api/v1/status"
  }
}
```

##### Async Response (when refresh completes)

The refresh operation runs asynchronously. Check status via the status endpoint or webhooks.

##### Status Codes

| Code | Description |
|------|-------------|
| 202 | Refresh initiated successfully |
| 401 | Authentication required |
| 404 | Channel not found |
| 409 | Channel is disabled or already refreshing |
| 429 | Rate limited (too many refresh requests) |

---

#### Refresh All Channels

Trigger a refresh for all enabled channels.

```http
POST /api/v1/refresh-all
```

##### Request Body (Optional)

```json
{
  "force": false,
  "download_new": true
}
```

##### Response

```json
{
  "message": "Global refresh initiated",
  "data": {
    "refresh_id": "refresh-global-123",
    "status": "in_progress",
    "started_at": "2024-01-15T12:00:00.000Z",
    "channels_refreshing": 4,
    "channels_skipped": 1,
    "channels_skipped_reason": {
      "ch-disabled": "Channel is disabled"
    }
  },
  "_links": {
    "status": "/api/v1/status",
    "queue": "/api/v1/queue"
  }
}
```

##### Status Codes

| Code | Description |
|------|-------------|
| 202 | Refresh initiated successfully |
| 401 | Authentication required |
| 409 | Global refresh already in progress |
| 429 | Rate limited |

---

#### Retry Failed Download

Retry a failed download.

```http
POST /api/v1/episodes/{id}/retry
```

##### Path Parameters

| Parameter | Type | Description |
|-----------|------|-------------|
| `id` | string | Episode ID |

##### Response

```json
{
  "message": "Download retry queued",
  "data": {
    "episode_id": "ep-failed1",
    "status": "pending",
    "retry_attempt": 1,
    "max_attempts": 3
  }
}
```

##### Status Codes

| Code | Description |
|------|-------------|
| 202 | Retry queued successfully |
| 401 | Authentication required |
| 404 | Episode not found |
| 409 | Episode is not in a retryable state |

---

## Data Types

### Channel

```typescript
interface Channel {
  id: string;                    // UUID v4
  url: string;                   // YouTube channel URL
  title: string;                 // Channel display name
  description: string | null;    // Channel description
  thumbnail_url: string | null;  // Channel thumbnail URL
  episode_count_config: number;  // Rolling window size (1-1000)
  feed_type: 'audio' | 'video' | 'both';
  enabled: boolean;              // Active monitoring
  episode_count: number;         // Current episode count (computed)
  last_refresh_at: string | null;  // ISO 8601 timestamp
  created_at: string;            // ISO 8601 timestamp
  updated_at: string;            // ISO 8601 timestamp
}
```

### Episode

```typescript
interface Episode {
  id: string;                      // UUID v4
  video_id: string;                // YouTube video ID
  channel_id: string;              // Foreign key to channel
  title: string;                   // Episode title
  description: string | null;      // Episode description
  thumbnail_url: string | null;    // Episode thumbnail URL
  duration_seconds: number | null; // Video duration
  published_at: string | null;     // YouTube publish date (ISO 8601)
  downloaded_at: string | null;    // Download completion (ISO 8601)
  file_path_audio: string | null;  // Relative path to audio file
  file_path_video: string | null;  // Relative path to video file
  file_size_audio: number | null;  // Audio file size in bytes
  file_size_video: number | null;  // Video file size in bytes
  status: EpisodeStatus;           // Current status
  retry_count: number;             // Failed download attempts
  error_message: string | null;    // Last error message
  created_at: string;              // ISO 8601 timestamp
  updated_at: string;              // ISO 8601 timestamp
}

type EpisodeStatus = 
  | 'pending'      // Queued for download
  | 'downloading'  // Currently downloading
  | 'processing'   // Transcoding
  | 'completed'    // Ready for consumption
  | 'failed'       // Max retries exceeded
  | 'deleted';     // Removed from disk
```

### DownloadQueueItem

```typescript
interface DownloadQueueItem {
  id: string;                     // UUID v4
  episode_id: string;             // Foreign key to episode
  priority: number;               // 1 (highest) to 10 (lowest)
  status: QueueStatus;            // Queue item status
  attempts: number;               // Current attempt count
  max_attempts: number;           // Max retries (default: 3)
  last_error: string | null;      // Last error message
  next_retry_at: string | null;   // Scheduled retry time (ISO 8601)
  created_at: string;             // ISO 8601 timestamp
  updated_at: string;             // ISO 8601 timestamp
}

type QueueStatus = 
  | 'pending'
  | 'in_progress'
  | 'completed'
  | 'failed'
  | 'cancelled';
```

### Pagination

```typescript
interface PaginationResponse<T> {
  data: T[];
  pagination: {
    page: number;
    limit: number;
    total_items: number;
    total_pages: number;
  };
  _links: {
    self: string;
    next: string | null;
    prev: string | null;
  };
}
```

---

## OpenAPI Specification

The following OpenAPI 3.0 specification can be imported into tools like Swagger UI, Postman, or Insomnia.

```yaml
openapi: 3.0.3
info:
  title: Yallarhorn Management API
  description: REST API for managing Yallarhorn podcast server
  version: 1.0.0
  contact:
    name: Yallarhorn Support
    url: https://github.com/example/yallarhorn

servers:
  - url: http://localhost:8080/api/v1
    description: Local development server

security:
  - BasicAuth: []

components:
  securitySchemes:
    BasicAuth:
      type: http
      scheme: basic
      description: HTTP Basic Authentication

  schemas:
    Channel:
      type: object
      properties:
        id:
          type: string
          format: uuid
          example: "ch-abc123"
        url:
          type: string
          format: uri
          example: "https://www.youtube.com/@techtalk"
        title:
          type: string
          example: "Tech Talk Weekly"
        description:
          type: string
          nullable: true
        thumbnail_url:
          type: string
          format: uri
          nullable: true
        episode_count_config:
          type: integer
          minimum: 1
          maximum: 1000
          default: 50
        feed_type:
          type: string
          enum: [audio, video, both]
          default: audio
        enabled:
          type: boolean
          default: true
        episode_count:
          type: integer
          description: Computed field - current episode count
        last_refresh_at:
          type: string
          format: date-time
          nullable: true
        created_at:
          type: string
          format: date-time
        updated_at:
          type: string
          format: date-time

    Episode:
      type: object
      properties:
        id:
          type: string
          format: uuid
        video_id:
          type: string
        channel_id:
          type: string
          format: uuid
        title:
          type: string
        description:
          type: string
          nullable: true
        thumbnail_url:
          type: string
          format: uri
          nullable: true
        duration_seconds:
          type: integer
          nullable: true
        published_at:
          type: string
          format: date-time
          nullable: true
        downloaded_at:
          type: string
          format: date-time
          nullable: true
        status:
          type: string
          enum: [pending, downloading, processing, completed, failed, deleted]
        retry_count:
          type: integer
        error_message:
          type: string
          nullable: true
        created_at:
          type: string
          format: date-time
        updated_at:
          type: string
          format: date-time

    Error:
      type: object
      properties:
        error:
          type: object
          required: [code, message]
          properties:
            code:
              type: string
              enum:
                - UNAUTHORIZED
                - FORBIDDEN
                - NOT_FOUND
                - METHOD_NOT_ALLOWED
                - CONFLICT
                - VALIDATION_ERROR
                - RATE_LIMITED
                - INTERNAL_ERROR
                - SERVICE_UNAVAILABLE
            message:
              type: string
            details:
              type: string
            field:
              type: string
            request_id:
              type: string

    CreateChannelRequest:
      type: object
      required: [url]
      properties:
        url:
          type: string
          format: uri
        title:
          type: string
        description:
          type: string
        episode_count_config:
          type: integer
          minimum: 1
          maximum: 1000
        feed_type:
          type: string
          enum: [audio, video, both]
        enabled:
          type: boolean

    UpdateChannelRequest:
      type: object
      properties:
        title:
          type: string
        description:
          type: string
        episode_count_config:
          type: integer
          minimum: 1
          maximum: 1000
        feed_type:
          type: string
          enum: [audio, video, both]
        enabled:
          type: boolean

paths:
  /channels:
    get:
      summary: List all channels
      tags: [Channels]
      parameters:
        - name: page
          in: query
          schema:
            type: integer
            default: 1
        - name: limit
          in: query
          schema:
            type: integer
            default: 50
            maximum: 100
        - name: enabled
          in: query
          schema:
            type: boolean
        - name: feed_type
          in: query
          schema:
            type: string
            enum: [audio, video, both]
      responses:
        '200':
          description: List of channels
          content:
            application/json:
              schema:
                type: object
                properties:
                  data:
                    type: array
                    items:
                      $ref: '#/components/schemas/Channel'
                  pagination:
                    type: object
                    properties:
                      page:
                        type: integer
                      limit:
                        type: integer
                      total_items:
                        type: integer
                      total_pages:
                        type: integer
        '401':
          description: Unauthorized

    post:
      summary: Create a new channel
      tags: [Channels]
      requestBody:
        required: true
        content:
          application/json:
            schema:
              $ref: '#/components/schemas/CreateChannelRequest'
      responses:
        '201':
          description: Channel created
          content:
            application/json:
              schema:
                type: object
                properties:
                  data:
                    $ref: '#/components/schemas/Channel'
                  message:
                    type: string
        '400':
          description: Invalid request
        '401':
          description: Unauthorized
        '409':
          description: Channel already exists

  /channels/{id}:
    get:
      summary: Get a channel by ID
      tags: [Channels]
      parameters:
        - name: id
          in: path
          required: true
          schema:
            type: string
      responses:
        '200':
          description: Channel details
          content:
            application/json:
              schema:
                type: object
                properties:
                  data:
                    $ref: '#/components/schemas/Channel'
        '401':
          description: Unauthorized
        '404':
          description: Channel not found

    put:
      summary: Update a channel
      tags: [Channels]
      parameters:
        - name: id
          in: path
          required: true
          schema:
            type: string
      requestBody:
        required: true
        content:
          application/json:
            schema:
              $ref: '#/components/schemas/UpdateChannelRequest'
      responses:
        '200':
          description: Channel updated
        '401':
          description: Unauthorized
        '404':
          description: Channel not found

    delete:
      summary: Delete a channel
      tags: [Channels]
      parameters:
        - name: id
          in: path
          required: true
          schema:
            type: string
        - name: delete_files
          in: query
          schema:
            type: boolean
            default: true
      responses:
        '200':
          description: Channel deleted
        '401':
          description: Unauthorized
        '404':
          description: Channel not found

  /channels/{channel_id}/episodes:
    get:
      summary: List episodes for a channel
      tags: [Episodes]
      parameters:
        - name: channel_id
          in: path
          required: true
          schema:
            type: string
        - name: page
          in: query
          schema:
            type: integer
            default: 1
        - name: limit
          in: query
          schema:
            type: integer
            default: 50
        - name: status
          in: query
          schema:
            type: string
            enum: [pending, downloading, processing, completed, failed, deleted]
      responses:
        '200':
          description: List of episodes
          content:
            application/json:
              schema:
                type: object
                properties:
                  data:
                    type: array
                    items:
                      $ref: '#/components/schemas/Episode'
                  pagination:
                    type: object
        '401':
          description: Unauthorized
        '404':
          description: Channel not found

  /episodes/{id}:
    get:
      summary: Get an episode by ID
      tags: [Episodes]
      parameters:
        - name: id
          in: path
          required: true
          schema:
            type: string
      responses:
        '200':
          description: Episode details
          content:
            application/json:
              schema:
                type: object
                properties:
                  data:
                    $ref: '#/components/schemas/Episode'
        '401':
          description: Unauthorized
        '404':
          description: Episode not found

    delete:
      summary: Delete an episode
      tags: [Episodes]
      parameters:
        - name: id
          in: path
          required: true
          schema:
            type: string
        - name: delete_files
          in: query
          schema:
            type: boolean
            default: true
      responses:
        '200':
          description: Episode deleted
        '401':
          description: Unauthorized
        '404':
          description: Episode not found

  /episodes/{id}/retry:
    post:
      summary: Retry a failed download
      tags: [Episodes]
      parameters:
        - name: id
          in: path
          required: true
          schema:
            type: string
      responses:
        '202':
          description: Retry queued
        '401':
          description: Unauthorized
        '404':
          description: Episode not found
        '409':
          description: Episode not in retryable state

  /status:
    get:
      summary: Get system status
      tags: [System]
      responses:
        '200':
          description: System status
        '401':
          description: Unauthorized

  /queue:
    get:
      summary: Get download queue status
      tags: [System]
      parameters:
        - name: status
          in: query
          schema:
            type: string
            enum: [pending, in_progress, completed, failed, cancelled]
        - name: page
          in: query
          schema:
            type: integer
            default: 1
        - name: limit
          in: query
          schema:
            type: integer
            default: 50
      responses:
        '200':
          description: Queue status
        '401':
          description: Unauthorized

  /health:
    get:
      summary: Health check
      tags: [System]
      security: []
      responses:
        '200':
          description: Healthy
          content:
            application/json:
              schema:
                type: object
                properties:
                  status:
                    type: string
                    enum: [healthy]
                  version:
                    type: string
                  timestamp:
                    type: string
                    format: date-time
        '503':
          description: Unhealthy

  /channels/{id}/refresh:
    post:
      summary: Refresh a channel
      tags: [Triggers]
      parameters:
        - name: id
          in: path
          required: true
          schema:
            type: string
      requestBody:
        content:
          application/json:
            schema:
              type: object
              properties:
                force:
                  type: boolean
                  default: false
                download_new:
                  type: boolean
                  default: true
      responses:
        '202':
          description: Refresh initiated
        '401':
          description: Unauthorized
        '404':
          description: Channel not found
        '429':
          description: Rate limited

  /refresh-all:
    post:
      summary: Refresh all channels
      tags: [Triggers]
      requestBody:
        content:
          application/json:
            schema:
              type: object
              properties:
                force:
                  type: boolean
                  default: false
                download_new:
                  type: boolean
                  default: true
      responses:
        '202':
          description: Global refresh initiated
        '401':
          description: Unauthorized
        '409':
          description: Refresh already in progress
        '429':
          description: Rate limited
```

---

## Appendix: Usage Examples

### Adding a New Channel

```bash
# Add a new channel
curl -X POST http://localhost:8080/api/v1/channels \
  -u admin:password \
  -H "Content-Type: application/json" \
  -d '{
    "url": "https://www.youtube.com/@examplechannel",
    "episode_count_config": 25,
    "feed_type": "audio"
  }'

# Response
# HTTP/1.1 201 Created
# {
#   "data": { ... },
#   "message": "Channel created successfully. Initial refresh scheduled."
# }
```

### Triggering a Manual Refresh

```bash
# Refresh a specific channel
curl -X POST http://localhost:8080/api/v1/channels/ch-abc123/refresh \
  -u admin:password

# Refresh all channels
curl -X POST http://localhost:8080/api/v1/refresh-all \
  -u admin:password
```

### Monitoring Status

```bash
# Get overall status
curl http://localhost:8080/api/v1/status -u admin:password

# Check download queue
curl http://localhost:8080/api/v1/queue -u admin:password

# Filter queue by status
curl "http://localhost:8080/api/v1/queue?status=failed" -u admin:password
```

### Deleting Content

```bash
# Delete an episode
curl -X DELETE http://localhost:8080/api/v1/episodes/ep-def456 \
  -u admin:password

# Delete a channel (and all episodes)
curl -X DELETE http://localhost:8080/api/v1/channels/ch-abc123 \
  -u admin:password
```

---

## See Also

- [Configuration Schema](./configuration.md) - YAML configuration reference
- [Data Model & Schema](./data-model.md) - Database schema documentation