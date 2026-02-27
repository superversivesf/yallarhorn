# Implementation Task Breakdown

This document provides a detailed implementation task breakdown for Yallarhorn, a podcast server that downloads YouTube videos and serves them as RSS/Atom feeds.

## Table of Contents

- [Overview](#overview)
- [Phase Summary](#phase-summary)
- [Phase 1: Project Setup & Core Infrastructure](#phase-1-project-setup--core-infrastructure)
- [Phase 2: Data Layer & Configuration](#phase-2-data-layer--configuration)
- [Phase 3: Download/Transcoding Pipeline](#phase-3-downloadtranscoding-pipeline)
- [Phase 4: Feed Generation](#phase-4-feed-generation)
- [Phase 5: API Implementation](#phase-5-api-implementation)
- [Phase 6: Authentication](#phase-6-authentication)
- [Phase 7: Docker & Deployment](#phase-7-docker--deployment)
- [Phase 8: Testing & Documentation](#phase-8-testing--documentation)
- [Critical Path](#critical-path)
- [Parallel Work Opportunities](#parallel-work-opportunities)
- [Testing Strategy](#testing-strategy)
- [Risk Mitigation](#risk-mitigation)

---

## Overview

### Project Summary

Yallarhorn is a self-hosted podcast server that:
- Monitors YouTube channels for new content
- Downloads and transcodes videos to audio/video formats
- Generates RSS/Atom feeds for podcast clients
- Provides a management API for channel and episode control

### Technology Stack

| Component | Technology |
|-----------|------------|
| Runtime | .NET 10 / ASP.NET Core |
| Database | SQLite with EF Core |
| Download Tool | yt-dlp |
| Transcoding | FFmpeg |
| Containerization | Docker multi-stage build |

### Estimated Total Effort

| Phase | Task Count | Est. Hours |
|-------|------------|------------|
| Phase 1: Setup & Infrastructure | 8 | 16 |
| Phase 2: Data & Config | 9 | 24 |
| Phase 3: Download Pipeline | 11 | 40 |
| Phase 4: Feed Generation | 7 | 20 |
| Phase 5: API Implementation | 12 | 32 |
| Phase 6: Authentication | 6 | 16 |
| Phase 7: Docker & Deployment | 9 | 20 |
| Phase 8: Testing & Documentation | 8 | 24 |
| **Total** | **70** | **192** |

---

## Phase Summary

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                              IMPLEMENTATION PHASES                           │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                              │
│  Phase 1: Setup & Infrastructure                                            │
│  ├── Solution structure                                                     │
│  ├── Project initialization                                                 │
│  └── Core abstractions                                                      │
│                                                                              │
│  Phase 2: Data Layer & Configuration                                        │
│  ├── Database schema & migrations                                           │
│  ├── Entity models & repositories                                           │
│  └── Configuration loading & validation                                     │
│                                                                              │
│  Phase 3: Download/Transcoding Pipeline ───────────────────── CRITICAL PATH │
│  ├── yt-dlp integration                                                     │
│  ├── FFmpeg transcoding                                                     │
│  ├── Download queue management                                              │
│  └── Background workers                                                     │
│                                                                              │
│  Phase 4: Feed Generation                                                   │
│  ├── RSS 2.0 feed builder                                                   │
│  ├── Atom feed builder                                                      │
│  └── iTunes podcast extensions                                              │
│                                                                              │
│  Phase 5: API Implementation                                                │
│  ├── Channel CRUD endpoints                                                 │
│  ├── Episode management                                                     │
│  ├── System status endpoints                                                │
│  └── Manual trigger endpoints                                               │
│                                                                              │
│  Phase 6: Authentication                                                    │
│  ├── HTTP Basic Auth middleware                                             │
│  ├── Dual credential system                                                 │
│  └── Authorization policies                                                 │
│                                                                              │
│  Phase 7: Docker & Deployment                                               │
│  ├── Multi-stage Dockerfile                                                 │
│  ├── Docker Compose configuration                                           │
│  └── Production deployment guides                                           │
│                                                                              │
│  Phase 8: Testing & Documentation                                           │
│  ├── Unit tests                                                             │
│  ├── Integration tests                                                      │
│  └── User documentation                                                     │
│                                                                              │
└─────────────────────────────────────────────────────────────────────────────┘
```

---

## Phase 1: Project Setup & Core Infrastructure

### 1.1 Solution Structure

**Task ID:** P1-001
**Title:** Create .NET Solution Structure
**Estimated Effort:** 2 hours
**Dependencies:** None
**Related Docs:** architecture.md

**Description:**
Initialize the .NET solution with proper project structure following clean architecture principles.

**Acceptance Criteria:**
- [ ] Solution file created (`Yallarhorn.sln`)
- [ ] Source directory structure established
- [ ] Test project created with xUnit
- [ ] Directory.Build.props configured for common settings
- [ ] .editorconfig and .gitignore files added

**Implementation Notes:**
```
/src
  /Yallarhorn
    /Controllers
    /Services
    /Models
    /Data
    /Background
/tests
  /Yallarhorn.Tests
    /Unit
    /Integration
```

---

### 1.2 Main Project Initialization

**Task ID:** P1-002
**Title:** Initialize ASP.NET Core Web Project
**Estimated Effort:** 2 hours
**Dependencies:** P1-001
**Related Docs:** architecture.md, configuration.md

**Description:**
Create the main ASP.NET Core web application project with minimal API structure.

**Acceptance Criteria:**
- [ ] ASP.NET Core 10 project created
- [ ] Program.cs with minimal API structure
- [ ] appsettings.json with default configuration
- [ ] Development and Production configuration files
- [ ] Project compiles without errors

---

### 1.3 Common Utilities

**Task ID:** P1-003
**Title:** Create Common Utility Library
**Estimated Effort:** 3 hours
**Dependencies:** P1-001
**Related Docs:** configuration.md

**Description:**
Implement common utility classes and extension methods used across the application.

**Acceptance Criteria:**
- [ ] Slug generation utility for channel/episode naming
- [ ] Duration formatting utility (seconds to HH:MM:SS)
- [ ] File size formatting utility
- [ ] Environment variable expansion helper
- [ ] Unit tests for all utilities

---

### 1.4 Logging Infrastructure

**Task ID:** P1-004
**Title:** Configure Serilog Logging
**Estimated Effort:** 2 hours
**Dependencies:** P1-002
**Related Docs:** configuration.md

**Description:**
Set up Serilog with file logging, console output, and configurable log levels.

**Acceptance Criteria:**
- [ ] Serilog package installed
- [ ] File sink configured with rotation
- [ ] Console sink for development
- [ ] Log level configurable from appsettings
- [ ] Structured logging with request IDs

---

### 1.5 Dependency Injection Setup

**Task ID:** P1-005
**Title:** Configure Service Registration Pattern
**Estimated Effort:** 2 hours
**Dependencies:** P1-002
**Related Docs:** architecture.md

**Description:**
Establish consistent pattern for service registration and dependency injection.

**Acceptance Criteria:**
- [ ] IServiceCollection extension methods for each module
- [ ] Service lifetime properly scoped (Singleton, Scoped, Transient)
- [ ] Options pattern for configuration injection
- [ ] Health check services registered

---

### 1.6 Error Handling Middleware

**Task ID:** P1-006
**Title:** Implement Global Error Handling
**Estimated Effort:** 3 hours
**Dependencies:** P1-002, P1-004
**Related Docs:** api-specification.md

**Description:**
Create global exception handling middleware that returns consistent error responses.

**Acceptance Criteria:**
- [ ] Exception handling middleware implemented
- [ ] Consistent JSON error response format
- [ ] Proper HTTP status codes for different exception types
- [ ] Request ID included in error responses
- [ ] Stack traces hidden in production mode

---

### 1.7 CLI Argument Parsing

**Task ID:** P1-007
**Title:** Implement Command-Line Interface
**Estimated Effort:** 3 hours
**Dependencies:** P1-002
**Related Docs:** configuration.md

**Description:**
Add CLI support for configuration validation, password hashing, and daemon mode.

**Acceptance Criteria:**
- [ ] `--config` flag for custom configuration path
- [ ] `--env-file` flag for environment file loading
- [ ] `config validate` command
- [ ] `config lint` command
- [ ] `auth hash-password` command
- [ ] Help text for all commands

---

### 1.8 Project Configuration

**Task ID:** P1-008
**Title:** Configure Project Build Settings
**Estimated Effort:** 1 hour
**Dependencies:** P1-001
**Related Docs:** docker-deployment.md

**Description:**
Configure project build settings for development, testing, and production.

**Acceptance Criteria:**
- [ ] Debug and Release configurations
- [ ] Nullable reference types enabled
- [ ] Treat warnings as errors in Release
- [ ] XML documentation generation
- [ ] Source link configured for debugging

---

## Phase 2: Data Layer & Configuration

### 2.1 Entity Models

**Task ID:** P2-001
**Title:** Create Entity Models
**Estimated Effort:** 3 hours
**Dependencies:** P1-002
**Related Docs:** data-model.md

**Description:**
Define entity classes for Channel, Episode, and DownloadQueue matching the database schema.

**Acceptance Criteria:**
- [ ] Channel entity with all fields from schema
- [ ] Episode entity with all fields from schema
- [ ] DownloadQueue entity with all fields from schema
- [ ] SchemaVersion entity for migration tracking
- [ ] Enum types for status values (EpisodeStatus, QueueStatus, FeedType)

---

### 2.2 Database Context

**Task ID:** P2-002
**Title:** Implement DbContext with EF Core
**Estimated Effort:** 4 hours
**Dependencies:** P2-001
**Related Docs:** data-model.md

**Description:**
Set up Entity Framework Core DbContext with SQLite provider and proper configuration.

**Acceptance Criteria:**
- [ ] YallarhornDbContext class created
- [ ] DbSet properties for all entities
- [ ] Entity configurations with Fluent API
- [ ] Index definitions matching schema
- [ ] Foreign key relationships configured
- [ ] WAL mode enabled on connection

---

### 2.3 Repository Interfaces

**Task ID:** P2-003
**Title:** Define Repository Interfaces
**Estimated Effort:** 2 hours
**Dependencies:** P2-001
**Related Docs:** data-model.md

**Description:**
Create repository interfaces for data access abstraction.

**Acceptance Criteria:**
- [ ] IChannelRepository with CRUD methods
- [ ] IEpisodeRepository with CRUD and query methods
- [ ] IDownloadQueueRepository with queue-specific methods
- [ ] Generic IRepository<T> base interface
- [ ] All methods return Task for async operations

---

### 2.4 Repository Implementations

**Task ID:** P2-004
**Title:** Implement Repository Classes
**Estimated Effort:** 4 hours
**Dependencies:** P2-002, P2-003
**Related Docs:** data-model.md

**Description:**
Implement repository classes using EF Core for database operations.

**Acceptance Criteria:**
- [ ] ChannelRepository implemented
- [ ] EpisodeRepository implemented
- [ ] DownloadQueueRepository implemented
- [ ] Pagination support in list queries
- [ ] Efficient query patterns (no N+1 queries)
- [ ] Unit tests for all repository methods

---

### 2.5 Database Migrations

**Task ID:** P2-005
**Title:** Create Initial Migration
**Estimated Effort:** 3 hours
**Dependencies:** P2-002
**Related Docs:** data-model.md

**Description:**
Set up EF Core migrations and create initial schema migration.

**Acceptance Criteria:**
- [ ] Migrations enabled in project
- [ ] Initial migration creates all tables
- [ ] All indexes created
- [ ] Schema version tracking table
- [ ] Migration applies cleanly to new database
- [ ] Idempotent migration application

---

### 2.6 Configuration Schema

**Task ID:** P2-006
**Title:** Define Configuration Classes
**Estimated Effort:** 3 hours
**Dependencies:** P1-002
**Related Docs:** configuration.md

**Description:**
Create configuration option classes matching the YAML schema.

**Acceptance Criteria:**
- [ ] YallarhornOptions root class
- [ ] TranscodeOptions class
- [ ] ServerOptions class
- [ ] DatabaseOptions class
- [ ] LoggingOptions class
- [ ] AuthOptions with FeedCredentials and AdminAuth
- [ ] ChannelOptions class

---

### 2.7 YAML Configuration Loader

**Task ID:** P2-007
**Title:** Implement YAML Configuration Provider
**Estimated Effort:** 4 hours
**Dependencies:** P2-006
**Related Docs:** configuration.md

**Description:**
Create YAML configuration provider with environment variable substitution.

**Acceptance Criteria:**
- [ ] YAML file parsing using YamlDotNet
- [ ] Configuration file location search order
- [ ] Environment variable substitution (`${VAR}`, `${VAR:-default}`, `${VAR:?error}`)
- [ ] `.env` file loading support
- [ ] Configuration reloading on file changes (optional)

---

### 2.8 Configuration Validation

**Task ID:** P2-008
**Title:** Implement Configuration Validator
**Estimated Effort:** 3 hours
**Dependencies:** P2-006, P2-007
**Related Docs:** configuration.md

**Description:**
Create validation logic for all configuration values.

**Acceptance Criteria:**
- [ ] Data annotation validation
- [ ] Custom validators for URLs, bitrates, quality values
- [ ] Validate channel URLs are valid YouTube channel URLs
- [ ] Detailed error messages for validation failures
- [ ] Validation runs on startup (fail fast)

---

### 2.9 Seed Data Support

**Task ID:** P2-009
**Title:** Implement Database Seeding
**Estimated Effort:** 2 hours
**Dependencies:** P2-004, P2-005
**Related Docs:** data-model.md

**Description:**
Add support for seeding initial data from configuration.

**Acceptance Criteria:**
- [ ] Channels seeded from configuration file
- [ ] Idempotent seeding (no duplicates)
- [ ] Seed only on first run or explicit command
- [ ] Test data seeding for development

---

## Phase 3: Download/Transcoding Pipeline

### 3.1 yt-dlp Client

**Task ID:** P3-001
**Title:** Implement yt-dlp Process Client
**Estimated Effort:** 5 hours
**Dependencies:** P1-005
**Related Docs:** download-pipeline.md

**Description:**
Create a client wrapper for yt-dlp process execution with JSON output parsing.

**Acceptance Criteria:**
- [ ] YtDlpClient class wrapping process execution
- [ ] Video metadata extraction (no download)
- [ ] Channel video list extraction (flat playlist)
- [ ] Video download with progress callback
- [ ] JSON output parsing into typed models
- [ ] Timeout handling
- [ ] Error parsing from stderr

---

### 3.2 FFmpeg Client

**Task ID:** P3-002
**Title:** Implement FFmpeg Process Client
**Estimated Effort:** 4 hours
**Dependencies:** P1-005
**Related Docs:** download-pipeline.md

**Description:**
Create a client wrapper for FFmpeg transcoding operations.

**Acceptance Criteria:**
- [ ] FfmpegClient class wrapping process execution
- [ ] Audio transcoding (MP3, M4A, AAC, OGG)
- [ ] Video transcoding (H.264/MP4)
- [ ] Progress parsing from stderr
- [ ] Duration detection from input file
- [ ] Error handling and exit code checking
- [ ] Timeout support

---

### 3.3 Download Coordinator

**Task ID:** P3-003
**Title:** Implement Download Concurrency Control
**Estimated Effort:** 3 hours
**Dependencies:** P3-001
**Related Docs:** download-pipeline.md

**Description:**
Create coordinator that manages concurrent download slots using semaphores.

**Acceptance Criteria:**
- [ ] SemaphoreSlim-based concurrency limiting
- [ ] Configurable max concurrent downloads
- [ ] Slot acquisition and release
- [ ] Cancellation support
- [ ] Active download tracking

---

### 3.4 Transcoding Service

**Task ID:** P3-004
**Title:** Implement Transcoding Pipeline Service
**Estimated Effort:** 5 hours
**Dependencies:** P3-002, P2-006
**Related Docs:** download-pipeline.md

**Description:**
Create service that orchestrates transcoding based on channel feed type.

**Acceptance Criteria:**
- [ ] Transcode based on channel feed_type (audio/video/both)
- [ ] Apply transcode settings from configuration
- [ ] Per-channel custom transcoding settings
- [ ] Output file path generation
- [ ] Error handling and retry logic
- [ ] File size recording after completion

---

### 3.5 Channel Refresh Service

**Task ID:** P3-005
**Title:** Implement Channel Refresh Logic
**Estimated Effort:** 4 hours
**Dependencies:** P3-001, P2-004
**Related Docs:** download-pipeline.md

**Description:**
Create service that discovers new episodes from YouTube channels.

**Acceptance Criteria:**
- [ ] Fetch channel video list via yt-dlp
- [ ] Filter to rolling window (episode_count_config)
- [ ] Check for duplicates by video_id
- [ ] Create new Episode records
- [ ] Queue new episodes for download
- [ ] Update channel last_refresh_at
- [ ] Handle channel not found/errors

---

### 3.6 Download Queue Service

**Task ID:** P3-006
**Title:** Implement Queue Management Service
**Estimated Effort:** 4 hours
**Dependencies:** P2-004
**Related Docs:** download-pipeline.md, data-model.md

**Description:**
Create service that manages the download queue lifecycle.

**Acceptance Criteria:**
- [ ] Enqueue new episodes with priority
- [ ] Get next pending item (priority + time ordered)
- [ ] Mark in-progress, completed, failed
- [ ] Retry scheduling with exponential backoff
- [ ] Max attempts enforcement
- [ ] Cancel stuck in-progress downloads

---

### 3.7 Download Pipeline Orchestrator

**Task ID:** P3-007
**Title:** Implement Full Download Pipeline
**Estimated Effort:** 5 hours
**Dependencies:** P3-003, P3-004, P3-006
**Related Docs:** download-pipeline.md

**Description:**
Create orchestrator that runs the complete download-to-feed pipeline.

**Acceptance Criteria:**
- [ ] Download video via yt-dlp
- [ ] Transcode to target formats
- [ ] Update episode status through pipeline
- [ ] Record file sizes and paths
- [ ] Clean up temporary files
- [ ] Error handling at each stage
- [ ] Cancellation support

---

### 3.8 Background Refresh Worker

**Task ID:** P3-008
**Title:** Implement Refresh Scheduler Worker
**Estimated Effort:** 3 hours
**Dependencies:** P3-005
**Related Docs:** download-pipeline.md, configuration.md

**Description:**
Create background service that periodically refreshes all enabled channels.

**Acceptance Criteria:**
- [ ] IHostedService implementation
- [ ] Configurable poll interval
- [ ] Initial refresh on startup
- [ ] Error isolation per channel
- [ ] Graceful shutdown support

---

### 3.9 Background Download Worker

**Task ID:** P3-009
**Title:** Implement Download Queue Worker
**Estimated Effort:** 4 hours
**Dependencies:** P3-007, P3-006
**Related Docs:** download-pipeline.md

**Description:**
Create background service that continuously processes the download queue.

**Acceptance Criteria:**
- [ ] IHostedService implementation
- [ ] Poll queue for pending items
- [ ] Process with download pipeline
- [ ] Respect concurrency limits
- [ ] Retry scheduling
- [ ] Graceful shutdown (finish current downloads)

---

### 3.10 Episode Cleanup Service

**Task ID:** P3-010
**Title:** Implement Rolling Window Cleanup
**Estimated Effort:** 3 hours
**Dependencies:** P2-004
**Related Docs:** download-pipeline.md, data-model.md

**Description:**
Create service that removes episodes outside the rolling window.

**Acceptance Criteria:**
- [ ] Identify episodes outside window per channel
- [ ] Delete media files from disk
- [ ] Mark episodes as deleted in database
- [ ] Run on configurable schedule
- [ ] Track storage freed

---

### 3.11 Pipeline Metrics

**Task ID:** P3-011
**Title:** Implement Download Metrics Collection
**Estimated Effort:** 3 hours
**Dependencies:** P3-007
**Related Docs:** download-pipeline.md

**Description:**
Add metrics collection for monitoring download performance and health.

**Acceptance Criteria:**
- [ ] Downloads started/completed/failed counts
- [ ] Download duration histograms
- [ ] Transcode duration by format
- [ ] Queue depth gauges
- [ ] Error categorization
- [ ] Expose via /api/v1/status endpoint

---

## Phase 4: Feed Generation

### 4.1 RSS Feed Builder

**Task ID:** P4-001
**Title:** Implement RSS 2.0 Feed Generator
**Estimated Effort:** 5 hours
**Dependencies:** P2-004
**Related Docs:** feed-generation.md

**Description:**
Create RSS 2.0 compliant feed generator with proper XML structure.

**Acceptance Criteria:**
- [ ] XML generation with XmlWriter
- [ ] All required channel elements (title, link, description)
- [ ] All required item elements
- [ ] Enclosure tag with URL, length, type
- [ ] Proper XML declarations and encoding
- [ ] iTunes namespace extension
- [ ] Unit tests for feed structure

---

### 4.2 iTunes Podcast Extensions

**Task ID:** P4-002
**Title:** Implement iTunes Podcast Namespace
**Estimated Effort:** 3 hours
**Dependencies:** P4-001
**Related Docs:** feed-generation.md

**Description:**
Add iTunes podcast namespace elements for enhanced podcast client compatibility.

**Acceptance Criteria:**
- [ ] itunes:author, itunes:owner, itunes:summary
- [ ] itunes:image with thumbnail URL
- [ ] itunes:category from tags
- [ ] itunes:duration formatting (HH:MM:SS)
- [ ] itunes:episodeType, itunes:explicit
- [ ] content:encoded for HTML descriptions

---

### 4.3 Atom Feed Builder

**Task ID:** P4-003
**Title:** Implement Atom 1.0 Feed Generator
**Estimated Effort:** 3 hours
**Dependencies:** P2-004
**Related Docs:** feed-generation.md

**Description:**
Create Atom 1.0 compliant feed generator as alternative format.

**Acceptance Criteria:**
- [ ] Atom namespace and structure
- [ ] Proper ID generation (yt: prefix)
- [ ] Link elements (self, alternate, enclosure)
- [ ] Updated/published timestamps
- [ ] Author elements
- [ ] Enclosure via link@rel="enclosure"

---

### 4.4 Feed Service

**Task ID:** P4-004
**Title:** Implement Feed Generation Service
**Estimated Effort:** 4 hours
**Dependencies:** P4-001, P4-002, P4-003
**Related Docs:** feed-generation.md

**Description:**
Create service that generates feeds per channel with rolling window episodes.

**Acceptance Criteria:**
- [ ] Generate feed for specific channel
- [ ] Feed type selection (audio/video)
- [ ] Episode limiting by episode_count_config
- [ ] Base URL construction from config
- [ ] Episode file path resolution

---

### 4.5 Combined Feed

**Task ID:** P4-005
**Title:** Implement Global Combined Feed
**Estimated Effort:** 3 hours
**Dependencies:** P4-004
**Related Docs:** feed-generation.md

**Description:**
Create aggregated feed from all enabled channels.

**Acceptance Criteria:**
- [ ] Aggregate episodes from all channels
- [ ] Maximum 100 episodes in combined feed
- [ ] Channel attribution in each item
- [ ] Distinct GUIDs for cross-channel episodes
- [ ] Separate audio and video variants

---

### 4.6 Feed Caching

**Task ID:** P4-006
**Title:** Implement Feed Caching Layer
**Estimated Effort:** 3 hours
**Dependencies:** P4-004
**Related Docs:** feed-generation.md

**Description:**
Add in-memory caching with cache invalidation for generated feeds.

**Acceptance Criteria:**
- [ ] IMemoryCache integration
- [ ] Cache by channel ID and feed type
- [ ] Configurable cache duration (default 5 min)
- [ ] Invalidation on episode completion
- [ ] Invalidation on channel update
- [ ] ETag generation for conditional requests

---

### 4.7 Feed Endpoints

**Task ID:** P4-007
**Title:** Implement Feed HTTP Endpoints
**Estimated Effort:** 3 hours
**Dependencies:** P4-004, P4-005, P4-006
**Related Docs:** feed-generation.md, api-specification.md

**Description:**
Create HTTP endpoints for serving RSS and Atom feeds.

**Acceptance Criteria:**
- [ ] GET /feed/{channelId}/audio.rss
- [ ] GET /feed/{channelId}/video.rss
- [ ] GET /feed/{channelId}/atom.xml
- [ ] GET /feeds/all.rss (combined)
- [ ] GET /feeds/all-video.rss
- [ ] GET /feeds/{channel}/{type}/{file} (media)
- [ ] Content-Type headers (application/rss+xml)
- [ ] Conditional request support (304 Not Modified)

---

## Phase 5: API Implementation

### 5.1 API Infrastructure

**Task ID:** P5-001
**Title:** Set Up API Foundation
**Estimated Effort:** 2 hours
**Dependencies:** P1-006
**Related Docs:** api-specification.md

**Description:**
Create base API infrastructure including routing, pagination, and HATEOAS links.

**Acceptance Criteria:**
- [ ] /api/v1 route prefix
- [ ] PaginationQuery model (page, limit, sort, order)
- [ ] PaginatedResponse<T> model with metadata
- [ ] Link generation for HATEOAS
- [ ] Request ID middleware

---

### 5.2 Channel List Endpoint

**Task ID:** P5-002
**Title:** Implement GET /api/v1/channels
**Estimated Effort:** 3 hours
**Dependencies:** P5-001, P2-004
**Related Docs:** api-specification.md

**Description:**
Create endpoint for listing all channels with filtering and pagination.

**Acceptance Criteria:**
- [ ] Query parameters: page, limit, enabled, feed_type, sort, order
- [ ] Pagination metadata in response
- [ ] Episode count computed field
- [ ] HATEOAS links included
- [ ] 200 OK response

---

### 5.3 Channel Get Endpoint

**Task ID:** P5-003
**Title:** Implement GET /api/v1/channels/{id}
**Estimated Effort:** 2 hours
**Dependencies:** P5-001, P2-004
**Related Docs:** api-specification.md

**Description:**
Create endpoint for retrieving a single channel by ID.

**Acceptance Criteria:**
- [ ] Path parameter for channel ID
- [ ] Include computed episode_count
- [ ] HATEOAS links for episodes, refresh
- [ ] 200 OK or 404 Not Found

---

### 5.4 Channel Create Endpoint

**Task ID:** P5-004
**Title:** Implement POST /api/v1/channels
**Estimated Effort:** 3 hours
**Dependencies:** P5-001, P3-005
**Related Docs:** api-specification.md

**Description:**
Create endpoint for adding a new channel.

**Acceptance Criteria:**
- [ ] Request body validation
- [ ] URL validation (YouTube channel format)
- [ ] Duplicate URL check (409 Conflict)
- [ ] Fetch channel metadata from YouTube
- [ ] Create channel record
- [ ] Schedule initial refresh
- [ ] Return 201 Created with location header

---

### 5.5 Channel Update Endpoint

**Task ID:** P5-005
**Title:** Implement PUT /api/v1/channels/{id}
**Estimated Effort:** 2 hours
**Dependencies:** P5-001, P2-004
**Related Docs:** api-specification.md

**Description:**
Create endpoint for updating channel configuration.

**Acceptance Criteria:**
- [ ] Partial update support (only sent fields)
- [ ] Validation of update values
- [ ] Timestamp update
- [ ] Return 200 OK with updated entity
- [ ] 404 if channel not found

---

### 5.6 Channel Delete Endpoint

**Task ID:** P5-006
**Title:** Implement DELETE /api/v1/channels/{id}
**Estimated Effort:** 2 hours
**Dependencies:** P5-001, P2-004
**Related Docs:** api-specification.md

**Description:**
Create endpoint for deleting a channel and its episodes.

**Acceptance Criteria:**
- [ ] Delete files query parameter (default true)
- [ ] Cascade delete all episodes
- [ ] Delete media files from disk
- [ ] Return deleted metrics (episodes, files, bytes)
- [ ] 200 OK with deletion summary

---

### 5.7 Episode List Endpoint

**Task ID:** P5-007
**Title:** Implement GET /api/v1/channels/{channelId}/episodes
**Estimated Effort:** 3 hours
**Dependencies:** P5-001, P2-004
**Related Docs:** api-specification.md

**Description:**
Create endpoint for listing episodes for a channel.

**Acceptance Criteria:**
- [ ] Pagination support
- [ ] Filter by status
- [ ] Sort by published_at, downloaded_at, duration
- [ ] Include file paths and sizes
- [ ] HATEOAS links

---

### 5.8 Episode Get Endpoint

**Task ID:** P5-008
**Title:** Implement GET /api/v1/episodes/{id}
**Estimated Effort:** 2 hours
**Dependencies:** P5-001, P2-004
**Related Docs:** api-specification.md

**Description:**
Create endpoint for retrieving a single episode.

**Acceptance Criteria:**
- [ ] Full episode details
- [ ] File paths for audio/video
- [ ] Error message if failed
- [ ] Links to media files
- [ ] 200 OK or 404 Not Found

---

### 5.9 Episode Delete Endpoint

**Task ID:** P5-009
**Title:** Implement DELETE /api/v1/episodes/{id}
**Estimated Effort:** 2 hours
**Dependencies:** P5-001, P2-004
**Related Docs:** api-specification.md

**Description:**
Create endpoint for deleting an episode.

**Acceptance Criteria:**
- [ ] Delete files query parameter
- [ ] Remove from queue if pending
- [ ] Delete media files
- [ ] Return deletion summary
- [ ] 409 Conflict if currently downloading

---

### 5.10 System Status Endpoints

**Task ID:** P5-010
**Title:** Implement Status and Health Endpoints
**Estimated Effort:** 3 hours
**Dependencies:** P5-001, P3-011
**Related Docs:** api-specification.md

**Description:**
Create endpoints for system status monitoring.

**Acceptance Criteria:**
- [ ] GET /api/v1/status (comprehensive status)
- [ ] GET /api/v1/health (lightweight for load balancers)
- [ ] GET /api/v1/queue (download queue status)
- [ ] Storage metrics (used, free, percentage)
- [ ] Episode statistics by status
- [ ] Active downloads listing

---

### 5.11 Manual Trigger Endpoints

**Task ID:** P5-011
**Title:** Implement Manual Trigger Endpoints
**Estimated Effort:** 3 hours
**Dependencies:** P5-001, P3-005, P3-006
**Related Docs:** api-specification.md

**Description:**
Create endpoints for manual refresh and retry operations.

**Acceptance Criteria:**
- [ ] POST /api/v1/channels/{id}/refresh
- [ ] POST /api/v1/refresh-all
- [ ] POST /api/v1/episodes/{id}/retry
- [ ] Force refresh option
- [ ] Rate limiting on triggers
- [ ] 202 Accepted response

---

### 5.12 Rate Limiting

**Task ID:** P5-012
**Title:** Implement API Rate Limiting
**Estimated Effort:** 3 hours
**Dependencies:** P5-001
**Related Docs:** api-specification.md

**Description:**
Add rate limiting to protect API from abuse.

**Acceptance Criteria:**
- [ ] ASP.NET Core RateLimiter
- [ ] Different limits for read vs write operations
- [ ] Rate limit headers in responses
- [ ] 429 Too Many Requests response
- [ ] Per-user or per-IP partitioning

---

## Phase 6: Authentication

### 6.1 Basic Auth Handler

**Task ID:** P6-001
**Title:** Implement Custom Basic Auth Handler
**Estimated Effort:** 4 hours
**Dependencies:** P1-005
**Related Docs:** authentication.md

**Description:**
Create custom authentication handler for HTTP Basic Auth.

**Acceptance Criteria:**
- [ ] AuthenticationHandler<BasicAuthOptions> implementation
- [ ] Authorization header parsing
- [ ] Base64 decoding
- [ ] Credential separation (username:password)
- [ ] Challenge response with WWW-Authenticate header

---

### 6.2 Credential Validator

**Task ID:** P6-002
**Title:** Implement Credential Validation
**Estimated Effort:** 3 hours
**Dependencies:** P6-001, P2-006
**Related Docs:** authentication.md

**Description:**
Create credential validator supporting BCrypt and plaintext comparison.

**Acceptance Criteria:**
- [ ] BCrypt verification for hashed passwords
- [ ] Plaintext fallback for env var substitution
- [ ] Domain-aware validation (feed vs admin)
- [ ] Timing-safe comparison
- [ ] Logging of auth attempts (without passwords)

---

### 6.3 Domain-Specific Authorization

**Task ID:** P6-003
**Title:** Implement Dual Auth Domain Support
**Estimated Effort:** 3 hours
**Dependencies:** P6-001
**Related Docs:** authentication.md

**Description:**
Implement domain-specific authentication for feed vs admin endpoints.

**Acceptance Criteria:**
- [ ] Path-based domain detection (/feed/* vs /api/*)
- [ ] Separate policies for FeedAuth and AdminAuth
- [ ] Configurable enable/disable per domain
- [ ] Health endpoint excluded from auth
- [ ] Claims-based principal with auth domain

---

### 6.4 Password Hashing CLI

**Task ID:** P6-004
**Title:** Implement Password Hashing Command
**Estimated Effort:** 2 hours
**Dependencies:** P1-007
**Related Docs:** authentication.md

**Description:**
Create CLI command for generating BCrypt password hashes.

**Acceptance Criteria:**
- [ ] `yallarhorn auth hash-password` command
- [ ] Interactive password prompt (hidden input)
- [ ] Configurable work factor
- [ ] Output BCrypt hash string
- [ ] Verification option

---

### 6.5 Authorization Policies

**Task ID:** P6-005
**Title:** Configure Authorization Policies
**Estimated Effort:** 2 hours
**Dependencies:** P6-001, P6-003
**Related Docs:** authentication.md, api-specification.md

**Description:**
Set up authorization policies for different endpoint categories.

**Acceptance Criteria:**
- [ ] RequireFeedAuth policy for /feed/* endpoints
- [ ] RequireAdminAuth policy for /api/* endpoints
- [ ] AllowAnonymous for /api/v1/health
- [ ] Policy application via endpoint filters
- [ ] 401 Unauthorized responses

---

### 6.6 Auth Configuration Validation

**Task ID:** P6-006
**Title:** Validate Auth Configuration on Startup
**Estimated Effort:** 2 hours
**Dependencies:** P2-008, P6-002
**Related Docs:** authentication.md

**Description:**
Add validation to ensure auth is properly configured.

**Acceptance Criteria:**
- [ ] Validate password present if enabled
- [ ] Validate BCrypt hash format if password_hash used
- [ ] Warn if plaintext password detected
- [ ] Fail fast on invalid configuration
- [ ] Clear error messages

---

## Phase 7: Docker & Deployment

### 7.1 Dockerfile

**Task ID:** P7-001
**Title:** Create Multi-Stage Dockerfile
**Estimated Effort:** 4 hours
**Dependencies:** Phase 1-6 complete
**Related Docs:** docker-deployment.md

**Description:**
Create optimized multi-stage Dockerfile for production deployment.

**Acceptance Criteria:**
- [ ] Build stage with .NET SDK
- [ ] Tools stage with yt-dlp and FFmpeg
- [ ] Runtime stage with ASP.NET runtime
- [ ] Non-root user (UID 1000)
- [ ] Health check instruction
- [ ] Volume mount points
- [ ] Image size under 500MB

---

### 7.2 Docker Ignore

**Task ID:** P7-002
**Title:** Create .dockerignore File
**Estimated Effort:** 1 hour
**Dependencies:** P7-001
**Related Docs:** docker-deployment.md

**Description:**
Create .dockerignore to optimize build context and image size.

**Acceptance Criteria:**
- [ ] Exclude bin/, obj/, .vs/
- [ ] Exclude test results
- [ ] Exclude documentation
- [ ] Exclude .git directory
- [ ] Exclude Docker files themselves

---

### 7.3 Docker Compose

**Task ID:** P7-003
**Title:** Create Docker Compose Configuration
**Estimated Effort:** 3 hours
**Dependencies:** P7-001
**Related Docs:** docker-deployment.md

**Description:**
Create Docker Compose file for easy deployment.

**Acceptance Criteria:**
- [ ] Version 3.8 compose file
- [ ] Service definition with volumes
- [ ] Environment variable support
- [ ] Health check configuration
- [ ] Optional nginx reverse proxy service
- [ ] Named volumes for persistence

---

### 7.4 Kubernetes Manifests

**Task ID:** P7-004
**Title:** Create Kubernetes Deployment Manifests
**Estimated Effort:** 3 hours
**Dependencies:** P7-001
**Related Docs:** docker-deployment.md

**Description:**
Create Kubernetes manifests for production deployments.

**Acceptance Criteria:**
- [ ] Deployment manifest
- [ ] Service manifest
- [ ] PersistentVolumeClaim
- [ ] ConfigMap for configuration
- [ ] Secret for credentials
- [ ] Liveness and readiness probes

---

### 7.5 CI/CD Pipeline

**Task ID:** P7-005
**Title:** Create CI/CD Pipeline Configuration
**Estimated Effort:** 4 hours
**Dependencies:** P7-001
**Related Docs:** docker-deployment.md

**Description:**
Create CI/CD pipeline for automated builds and deployments.

**Acceptance Criteria:**
- [ ] GitHub Actions workflow
- [ ] Build and test on PR
- [ ] Docker image build and push on merge
- [ ] Version tagging strategy
- [ ] Security scanning (Trivy or similar)
- [ ] Release automation

---

### 7.6 Configuration Templates

**Task ID:** P7-006
**Title:** Create Example Configuration Files
**Estimated Effort:** 2 hours
**Dependencies:** P2-006
**Related Docs:** configuration.md, docker-deployment.md

**Description:**
Create example configuration files for different deployment scenarios.

**Acceptance Criteria:**
- [ ] yallarhorn.example.yaml
- [ ] .env.example
- [ ] Production configuration template
- [ ] Development configuration template
- [ ] Comments explaining each section

---

### 7.7 Deployment Documentation

**Task ID:** P7-007
**Title:** Write Deployment Guide
**Estimated Effort:** 3 hours
**Dependencies:** P7-001, P7-003, P7-004
**Related Docs:** docker-deployment.md

**Description:**
Write comprehensive deployment documentation.

**Acceptance Criteria:**
- [ ] Prerequisites section
- [ ] Quick start guide
- [ ] Docker deployment steps
- [ ] Docker Compose deployment steps
- [ ] Kubernetes deployment steps
- [ ] Environment variable reference
- [ ] Troubleshooting common issues

---

### 7.8 Update Strategy

**Task ID:** P7-008
**Title:** Define Update and Rollback Procedures
**Estimated Effort:** 2 hours
**Dependencies:** P7-001
**Related Docs:** docker-deployment.md

**Description:**
Document update and rollback procedures.

**Acceptance Criteria:**
- [ ] Database migration handling during updates
- [ ] Rolling update strategy
- [ ] Rollback procedure
- [ ] Backup requirements before update
- [ ] Version compatibility notes

---

### 7.9 Monitoring Setup

**Task ID:** P7-009
**Title:** Create Monitoring and Alerting Configuration
**Estimated Effort:** 4 hours
**Dependencies:** P3-011
**Related Docs:** docker-deployment.md

**Description:**
Set up monitoring, logging, and alerting infrastructure.

**Acceptance Criteria:**
- [ ] Prometheus metrics endpoint
- [ ] Grafana dashboard template
- [ ] Alert rules for common issues
- [ ] Log aggregation configuration
- [ ] Health check integration

---

## Phase 8: Testing & Documentation

### 8.1 Unit Tests - Core

**Task ID:** P8-001
**Title:** Write Unit Tests for Core Services
**Estimated Effort:** 6 hours
**Dependencies:** Phase 2 complete
**Related Docs:** All design docs

**Description:**
Write comprehensive unit tests for core service layer.

**Acceptance Criteria:**
- [ ] Configuration loader tests
- [ ] Repository tests with in-memory SQLite
- [ ] Feed generator tests
- [ ] Transcoding service tests (mock FFmpeg)
- [ ] 80%+ code coverage

---

### 8.2 Unit Tests - Pipeline

**Task ID:** P8-002
**Title:** Write Unit Tests for Download Pipeline
**Estimated Effort:** 5 hours
**Dependencies:** Phase 3 complete
**Related Docs:** download-pipeline.md

**Description:**
Write unit tests for download and transcoding pipeline.

**Acceptance Criteria:**
- [ ] yt-dlp client tests (mock process)
- [ ] FFmpeg client tests (mock process)
- [ ] Queue service tests
- [ ] Retry logic tests
- [ ] Error handling tests

---

### 8.3 Integration Tests - API

**Task ID:** P8-003
**Title:** Write API Integration Tests
**Estimated Effort:** 5 hours
**Dependencies:** Phase 5 complete
**Related Docs:** api-specification.md

**Description:**
Write integration tests for all API endpoints using WebApplicationFactory.

**Acceptance Criteria:**
- [ ] Channel CRUD tests
- [ ] Episode management tests
- [ ] Status endpoint tests
- [ ] Authentication tests
- [ ] Error response format tests

---

### 8.4 Integration Tests - Feeds

**Task ID:** P8-004
**Title:** Write Feed Generation Integration Tests
**Estimated Effort:** 4 hours
**Dependencies:** Phase 4 complete
**Related Docs:** feed-generation.md

**Description:**
Write integration tests for feed generation.

**Acceptance Criteria:**
- [ ] RSS feed validation against spec
- [ ] Atom feed validation against spec
- [ ] iTunes namespace validation
- [ ] Feed caching tests
- [ ] Episode ordering tests

---

### 8.5 End-to-End Tests

**Task ID:** P8-005
**Title:** Write End-to-End Workflow Tests
**Estimated Effort:** 6 hours
**Dependencies:** Phase 1-6 complete
**Related Docs:** All design docs

**Description:**
Write end-to-end tests for complete workflows.

**Acceptance Criteria:**
- [ ] Channel add -> refresh -> download -> feed available
- [ ] Episode deletion workflow
- [ ] Rolling window cleanup workflow
- [ ] Authentication workflow
- [ ] Uses test containers for yt-dlp (or mock)

---

### 8.6 User Documentation

**Task ID:** P8-006
**Title:** Write User Guide
**Estimated Effort:** 5 hours
**Dependencies:** Phase 1-7 complete
**Related Docs:** All design docs

**Description:**
Write comprehensive user documentation.

**Acceptance Criteria:**
- [ ] Installation guide
- [ ] Configuration guide
- [ ] Channel management guide
- [ ] Troubleshooting guide
- [ ] FAQ section
- [ ] Screenshots/diagrams where helpful

---

### 8.7 API Documentation

**Task ID:** P8-007
**Title:** Generate OpenAPI Documentation
**Estimated Effort:** 3 hours
**Dependencies:** Phase 5 complete
**Related Docs:** api-specification.md

**Description:**
Generate and expose OpenAPI/Swagger documentation.

**Acceptance Criteria:**
- [ ] Swashbuckle.AspNetCore integration
- [ ] OpenAPI 3.0 spec generation
- [ ] Swagger UI at /swagger
- [ ] XML comment integration
- [ ] Authentication documentation

---

### 8.8 Developer Documentation

**Task ID:** P8-008
**Title:** Write Developer Guide
**Estimated Effort:** 4 hours
**Dependencies:** Phase 1-7 complete
**Related Docs:** All design docs

**Description:**
Write documentation for contributors and developers.

**Acceptance Criteria:**
- [ ] Development environment setup
- [ ] Architecture overview
- [ ] Code style guide
- [ ] PR requirements
- [ ] Release process
- [ ] Debugging tips

---

## Critical Path

The critical path represents the minimum time to complete the project if no parallelization is possible. The actual timeline can be shorter with parallel execution.

```
Phase 1 (Setup) ────────────────────────────────────────────────────────────────►
       │
       ▼
Phase 2 (Data & Config) ─────────────────────────────────────────────────────────►
       │                                           │
       │                                           ▼
       │                                      Phase 6 (Auth)
       │                                           │
       │                                           ▼
       ├───► Phase 3 (Pipeline) ───── CRITICAL ───────────────────────────────────►
       │        │                                   ▲
       │        │                                   │
       │        └───► Phase 4 (Feeds) ─────────────┘
       │                    
       └───► Phase 5 (API)
                    
Phase 7 (Docker) ────────────────────────────────────────────────────────────────►
       │
       ▼
Phase 8 (Testing & Docs) ────────────────────────────────────────────────────────►
```

### Critical Path Sequence

1. **P1-001 to P1-008** - Project Setup (16 hours)
2. **P2-001 to P2-009** - Data Layer & Configuration (24 hours)
3. **P3-001 to P3-011** - Download Pipeline (40 hours) ⚠️ **CRITICAL**
4. **P4-001 to P4-007** - Feed Generation (20 hours)
5. **P5-001 to P5-012** - API Implementation (32 hours)
6. **P7-001 to P7-009** - Docker & Deployment (20 hours)
7. **P8-001 to P8-008** - Testing & Documentation (24 hours)

**Total Critical Path Duration:** ~176 hours (22 working days at 8 hours/day)

---

## Parallel Work Opportunities

### Maximum Parallelization

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                         PARALLEL WORK TRACKS                                 │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                              │
│  Track A (Backend Core)          Track B (Infrastructure)                   │
│  ─────────────────────           ─────────────────────                      │
│  Phase 2: Data & Config          Phase 6: Authentication                    │
│        │                               │                                     │
│        ▼                               ▼                                     │
│  Phase 3: Pipeline               Phase 7: Docker Setup                      │
│        │                               │                                     │
│        ▼                               ▼                                     │
│  Phase 4: Feeds                  Phase 7: CI/CD                             │
│        │                                                                     │
│        ▼                                                                     │
│  Phase 5: API                                                                │
│        │                                                                     │
│        ▼                                                                     │
│  Phase 8: Testing                                                            │
│                                                                              │
├─────────────────────────────────────────────────────────────────────────────┤
│  Track C (Documentation)                                                     │
│  ─────────────────────                                                       │
│  API docs can start after Phase 5                                           │
│  User docs can start after Phase 4                                          │
│  Deployment docs can start after Phase 7                                    │
└─────────────────────────────────────────────────────────────────────────────┘
```

### Parallelization by Phase

| Phase | Can Run Parallel With |
|-------|----------------------|
| Phase 2 | Phase 6 (Auth) |
| Phase 3 | Phase 6 (Auth), Phase 7 (Docker setup) |
| Phase 4 | Phase 5, Phase 6, Phase 7 |
| Phase 5 | Phase 4, Phase 6, Phase 7 |
| Phase 6 | Phase 2, Phase 3, Phase 4 |
| Phase 7 | Phase 3, Phase 4, Phase 5 |
| Phase 8 | Only after Phase 5 |

### Team Parallelization

With a team of 3 developers:

| Developer | Primary Responsibilities |
|-----------|-------------------------|
| Dev 1 | Phase 2 → Phase 3 → Phase 8 |
| Dev 2 | Phase 6 → Phase 4 → Phase 8 |
| Dev 3 | Phase 7 → Phase 5 → Phase 8 |

---

## Testing Strategy

### Test Pyramid

```
                    ┌──────────────┐
                   │    E2E Tests  │  (Few, Slow, Expensive)
                  ┌┴──────────────┴┐
                 │ Integration Tests │  (Some, Medium Speed)
                ┌┴──────────────────┴┐
               │     Unit Tests       │  (Many, Fast, Cheap)
              └──────────────────────┘
```

### Test Categories

| Category | Count Estimate | Execution Time | Coverage Target |
|----------|---------------|----------------|-----------------|
| Unit Tests | 200+ | < 30 seconds | 80%+ |
| Integration Tests | 50+ | < 5 minutes | Key flows |
| E2E Tests | 10+ | < 10 minutes | Critical paths |

### Unit Test Focus Areas

1. **Configuration parsing and validation**
2. **Feed generation logic** (RSS/Atom XML structure)
3. **Slug generation and path handling**
4. **Duration and file size formatting**
5. **Retry logic and exponential backoff**
6. **Authentication validation**

### Integration Test Focus Areas

1. **API endpoints with database**
2. **Feed generation with real data**
3. **Authentication flow**
4. **Database migrations**
5. **Configuration loading**

### E2E Test Focus Areas

1. **Complete download pipeline** (with mocked yt-dlp)
2. **Channel addition → episode available in feed**
3. **Rolling window cleanup**
4. **Error recovery scenarios**

### Test Data Strategy

```csharp
// Use in-memory SQLite for tests
services.AddDbContext<YallarhornDbContext>(options =>
    options.UseSqlite("DataSource=:memory:"));

// Seed with test data
public static class TestDataFactory
{
    public static Channel CreateTestChannel(string id = "test-channel")
    public static Episode CreateTestEpisode(string channelId, string videoId)
    public static List<Episode> CreateTestEpisodes(int count)
}
```

### Mocking Strategy

| Component | Mock Strategy |
|-----------|---------------|
| yt-dlp | Mock process execution, return predefined JSON |
| FFmpeg | Mock process execution |
| File system | Use temp directories, cleanup after tests |
| HTTP client | Use HttpClient with mock handlers |

---

## Risk Mitigation

### Identified Risks

| Risk | Probability | Impact | Mitigation |
|------|-------------|--------|------------|
| yt-dlp API changes | High | High | Pin version, automated tests, update process |
| YouTube rate limiting | Medium | High | Request delays, caching, user-agent rotation |
| FFmpeg version incompatibility | Low | Medium | Pin version in Docker, test transcoding |
| SQLite concurrent access | Low | Medium | WAL mode, connection pooling |
| Feed validation failures | Medium | Medium | Validation tests, feed validators |

### Contingency Plans

1. **yt-dlp Breaking Changes**
   - Subscribe to yt-dlp releases
   - Automated daily smoke tests
   - Hotfix release process

2. **YouTube Rate Limiting**
   - Implement configurable delays
   - Add proxy support (future)
   - Reduce concurrent downloads

3. **Performance Issues**
   - Database query optimization
   - Feed caching strategy
   - Async processing improvements

---

## Appendix: Task Dependencies Graph

```
P1-001 ──► P1-002 ──┬──► P1-004
                    ├──► P1-005
                    ├──► P1-006
                    └──► P1-007

P1-002 ──► P2-001 ──► P2-002 ──► P2-003 ──► P2-004 ──► P2-005
                      │           │
                      │           └──► P2-009
                      │
                      └──► P2-006 ──► P2-007 ──► P2-008

P1-005 ──► P3-001 ──► P3-003 ──► P3-007
                      │
        ◄─── P3-002 ─┘           │
                                  ▼
P2-004 ──► P3-005 ──────► P3-006 ──► P3-007 ──► P3-008, P3-009
                      │
                      └──► P3-010

P2-004 ──► P4-001 ──► P4-002
                      │
        ◄─── P4-003 ─┘
                      │
                      ▼
                   P4-004 ──► P4-005 ──► P4-006 ──► P4-007

P1-006 ──► P5-001 ──┬──► P5-002 to P5-006
                    │
                    └──► P5-007 to P5-012

P1-005 ──► P6-001 ──► P6-002 ──► P6-003 ──► P6-005
          │
          └──► P6-004

Phase 1-6 ──► P7-001 ──► P7-002, P7-003, P7-004
                          │
                          ▼
                       P7-005 to P7-009

Phase 2 ──► P8-001
Phase 3 ──► P8-002
Phase 5 ──► P8-003
Phase 4 ──► P8-004
Phase 6 ──► P8-005
All      ──► P8-006 to P8-008
```

---

## Document References

| Document | Purpose |
|----------|---------|
| [data-model.md](./data-model.md) | Database schema and entity relationships |
| [configuration.md](./configuration.md) | YAML configuration schema |
| [api-specification.md](./api-specification.md) | REST API endpoints |
| [feed-generation.md](./feed-generation.md) | RSS/Atom feed design |
| [download-pipeline.md](./download-pipeline.md) | Download and transcoding workflow |
| [authentication.md](./authentication.md) | Auth system design |
| [docker-deployment.md](./docker-deployment.md) | Container deployment strategy |

---

*Document Version: 1.0*
*Last Updated: 2024*
*Estimated Total Effort: 192 hours*