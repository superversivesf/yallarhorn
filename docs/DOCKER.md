# Docker Deployment Guide

This guide covers deploying Yallarhorn using Docker and Docker Compose. Yallarhorn is a podcast server that downloads YouTube videos and serves them as RSS/Atom feeds.

## Table of Contents

- [Prerequisites](#prerequisites)
- [Quick Start](#quick-start)
- [Building the Image](#building-the-image)
- [Running with Docker](#running-with-docker)
- [Docker Compose](#docker-compose)
- [Configuration](#configuration)
- [Environment Variables](#environment-variables)
- [Volume Mounts](#volume-mounts)
- [Health Checks](#health-checks)
- [Updating](#updating)
- [Troubleshooting](#troubleshooting)

---

## Prerequisites

Before deploying Yallarhorn, ensure you have:

| Requirement | Version | Purpose |
|-------------|---------|---------|
| Docker | 20.10+ | Container runtime |
| Docker Compose | v2.0+ | Multi-container orchestration |
| Storage | 50GB+ | For downloaded media files |

### Verify Prerequisites

```bash
# Check Docker version
docker --version

# Check Docker Compose
docker compose version
```

---

## Quick Start

The fastest way to get Yallarhorn running:

```bash
# 1. Clone the repository (if not already)
git clone https://github.com/yourorg/yallarhorn.git
cd yallarhorn

# 2. Create configuration file
cp yallarhorn.yaml yallarhorn.yaml.local
# Edit yallarhorn.yaml.local with your channel settings

# 3. Build and run
docker compose up -d

# 4. Check status
docker compose ps

# 5. View logs
docker compose logs -f app
```

Access the application at: `http://localhost:5001`

---

## Building the Image

### Standard Build

```bash
# Build with default tag
docker build -t yallarhorn:latest .

# Build with specific tag
docker build -t yallarhorn:v1.0.0 .
```

### Build with No Cache

Useful when updating yt-dlp or other dependencies:

```bash
docker build --no-cache -t yallarhorn:latest .
```

### Build Arguments

The Dockerfile supports the following build-time configuration:

```bash
# Build with specific .NET version (if needed)
docker build --build-arg DOTNET_VERSION=10.0 -t yallarhorn:latest .
```

### Image Details

| Aspect | Details |
|--------|---------|
| Base Image | `mcr.microsoft.com/dotnet/aspnet:10.0` |
| Build Image | `mcr.microsoft.com/dotnet/sdk:10.0` |
| Port | 5001 |
| Included Tools | yt-dlp, ffmpeg, curl |

---

## Running with Docker

### Basic Container Run

```bash
docker run -d \
  --name yallarhorn \
  --restart unless-stopped \
  -p 5001:5001 \
  -v ./yallarhorn.yaml:/app/yallarhorn.yaml:ro \
  -v yallarhorn-data:/app/data \
  yallarhorn:latest
```

### Full Container Run

```bash
docker run -d \
  --name yallarhorn \
  --restart unless-stopped \
  -p 5001:5001 \
  -v $(pwd)/yallarhorn.yaml:/app/yallarhorn.yaml:ro \
  -v yallarhorn-data:/app/data \
  -v yallarhorn-downloads:/app/downloads \
  -v yallarhorn-temp:/app/temp \
  -e ASPNETCORE_ENVIRONMENT=Production \
  --health-cmd="curl -f http://localhost:5001/health || exit 1" \
  --health-interval=30s \
  --health-timeout=10s \
  --health-retries=3 \
  --health-start-period=40s \
  yallarhorn:latest
```

### Run with Host Volumes

For direct access to data on the host:

```bash
# Create directories
mkdir -p ./data ./downloads ./temp

# Run with host mounts
docker run -d \
  --name yallarhorn \
  -p 5001:5001 \
  -v $(pwd)/yallarhorn.yaml:/app/yallarhorn.yaml:ro \
  -v $(pwd)/data:/app/data \
  -v $(pwd)/downloads:/app/downloads \
  -v $(pwd)/temp:/app/temp \
  yallarhorn:latest
```

---

## Docker Compose

Yallarhorn includes a `docker-compose.yml` for easy deployment.

### Development Mode

The included `docker-compose.override.yml` is automatically loaded and configures development settings:

```bash
# Start with development settings
docker compose up -d
```

### Production Mode

To run without the override:

```bash
docker compose -f docker-compose.yml up -d
```

### Services

| Service | Description |
|---------|-------------|
| `app` | Main Yallarhorn application |

### Named Volumes

| Volume | Container Path | Purpose |
|--------|----------------|---------|
| `yallarhorn-data` | `/app/data` | SQLite database |
| `yallarhorn-downloads` | `/app/downloads` | Downloaded media files |
| `yallarhorn-temp` | `/app/temp` | Temporary processing files |

### Common Commands

```bash
# Start services
docker compose up -d

# Stop services
docker compose down

# View logs
docker compose logs -f app

# Restart service
docker compose restart app

# Check service status
docker compose ps

# Pull latest image (if using pre-built)
docker compose pull

# Rebuild and restart
docker compose up -d --build

# Stop and remove all resources
docker compose down -v
```

### Scaling (Not Recommended)

Yallarhorn uses SQLite and is designed as a single-instance application. Multiple instances would conflict on database access.

```bash
# NOT recommended - for reference only
docker compose up -d --scale app=2
```

---

## Configuration

### Configuration File

Yallarhorn uses a YAML configuration file (`yallarhorn.yaml`):

```yaml
# yallarhorn.yaml
version: "1.0"

# Polling interval in seconds
pollInterval: 3600

# Maximum concurrent downloads
maxConcurrentDownloads: 3

# Directories
downloadDir: "./downloads"
tempDir: "./temp"

# Server configuration
server:
  host: "0.0.0.0"
  port: 5001
  baseUrl: "http://localhost:5001"
  feedPath: "/feeds"

# Database
database:
  path: "./data/yallarhorn.db"

# Transcode settings
transcodeSettings:
  audioFormat: "mp3"
  audioBitrate: "192k"

# Channels to monitor
channels:
  - name: "My Favorite Channel"
    url: "https://www.youtube.com/@channel"
    episodeCount: 50
    enabled: true
    feedType: "audio"
```

### Mount Configuration

The configuration file is mounted read-only into the container:

```yaml
volumes:
  - ./yallarhorn.yaml:/app/yallarhorn.yaml:ro
```

To update configuration:

1. Edit the file on the host
2. Restart the container: `docker compose restart app`

---

## Environment Variables

### Core Variables

| Variable | Description | Default |
|----------|-------------|---------|
| `ASPNETCORE_ENVIRONMENT` | Runtime environment | `Production` |
| `ASPNETCORE_URLS` | Server bind address | `http://+:5001` |

### Usage Examples

```bash
# Override via command line
docker run -e ASPNETCORE_ENVIRONMENT=Development ...

# Using .env file with Docker Compose
# Create .env file:
ASPNETCORE_ENVIRONMENT=Production
POLL_INTERVAL=1800
```

### Docker Compose Environment

```yaml
services:
  app:
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - ASPNETCORE_URLS=http://+:5001
```

---

## Volume Mounts

### Directory Structure

Inside the container, Yallarhorn uses these directories:

```
/app/
├── yallarhorn.yaml    # Configuration (read-only mount)
├── data/              # SQLite database
│   └── yallarhorn.db
├── downloads/         # Downloaded media
│   └── channel-name/
│       ├── audio/
│       └── video/
└── temp/              # Temporary processing files
```

### Volume Types

#### Named Volumes (Recommended for Production)

```yaml
volumes:
  - yallarhorn-data:/app/data
  - yallarhorn-downloads:/app/downloads
  - yallarhorn-temp:/app/temp
```

Benefits:
- Managed by Docker
- Easier backups
- Portable across hosts

#### Host Bind Mounts (Alternative)

```yaml
volumes:
  - ./data:/app/data
  - ./downloads:/app/downloads
  - ./temp:/app/temp
```

Benefits:
- Direct host access
- Easy to inspect files
- Simpler backups with standard tools

### Backup Strategies

#### Named Volume Backup

```bash
# Create backup
docker run --rm \
  -v yallarhorn-data:/data \
  -v $(pwd)/backup:/backup \
  alpine tar czf /backup/data-backup.tar.gz /data

# Restore backup
docker run --rm \
  -v yallarhorn-data:/data \
  -v $(pwd)/backup:/backup \
  alpine tar xzf /backup/data-backup.tar.gz -C /
```

#### Host Mount Backup

```bash
# Simple backup with host mounts
tar czf yallarhorn-backup.tar.gz ./data ./downloads
```

---

## Health Checks

### Built-in Health Check

The container includes a health check that verifies the application is responding:

```yaml
healthcheck:
  test: ["CMD", "curl", "-f", "http://localhost:5001/health"]
  interval: 30s
  timeout: 10s
  retries: 3
  start_period: 40s
```

### Health Check Parameters

| Parameter | Value | Description |
|-----------|-------|-------------|
| `interval` | 30s | Time between checks |
| `timeout` | 10s | Max time for response |
| `retries` | 3 | Failures before unhealthy |
| `start_period` | 40s | Grace period on startup |

### Checking Health Status

```bash
# Check container health
docker inspect --format='{{.State.Health.Status}}' yallarhorn

# View health check history
docker inspect --format='{{json .State.Health}}' yallarhorn | jq

# Manual health check
curl -f http://localhost:5001/health
```

### Health Endpoint

The `/health` endpoint returns:

```json
{
  "status": "healthy",
  "timestamp": "2024-01-15T12:00:00Z"
}
```

---

## Updating

### Standard Update

```bash
# 1. Pull latest changes
git pull

# 2. Rebuild image
docker compose build

# 3. Restart with new image
docker compose up -d

# 4. Check logs
docker compose logs -f app
```

### Update yt-dlp Only

yt-dlp is frequently updated to handle YouTube changes. To update without rebuilding:

```bash
# Update in running container (temporary)
docker exec yallarhorn pip3 install --upgrade yt-dlp

# Verify version
docker exec yallarhorn yt-dlp --version
```

For permanent updates, rebuild the image:

```bash
docker compose build --no-cache
docker compose up -d
```

### Zero-Downtime Update (Advanced)

For production deployments requiring no downtime:

```bash
# 1. Build new image with different tag
docker build -t yallarhorn:v1.1.0 .

# 2. Update docker-compose.yml to use new tag
# image: yallarhorn:v1.1.0

# 3. Start new container (replaces old one)
docker compose up -d

# Docker handles the transition gracefully
```

---

## Troubleshooting

### Container Won't Start

```bash
# Check logs for errors
docker compose logs app

# Common issues:
# - Missing yallarhorn.yaml: Create the file
# - Invalid YAML syntax: Validate configuration
# - Port already in use: Change port mapping
```

### Permission Issues

```bash
# If using host mounts, check permissions
ls -la ./data ./downloads ./temp

# Fix permissions (if needed)
chmod -R 755 ./data ./downloads ./temp
```

### Downloads Failing

Check if yt-dlp is working:

```bash
# Enter container
docker exec -it yallarhorn /bin/bash

# Test yt-dlp
yt-dlp --simulate "https://www.youtube.com/watch?v=VIDEO_ID"

# Check ffmpeg
ffmpeg -version

# Update yt-dlp
pip3 install --upgrade yt-dlp
```

### Database Issues

```bash
# Check database exists
docker exec yallarhorn ls -la /app/data/

# Database locked - stop container first
docker compose stop app

# Remove lock files (if needed)
rm -f ./data/yallarhorn.db-wal ./data/yallarhorn.db-shm

# Restart
docker compose start app
```

### Performance Issues

```bash
# Check container resource usage
docker stats yallarhorn

# Check disk usage
docker exec yallarhorn df -h

# Check download queue size
docker exec yallarhorn ls -la /app/temp/
```

### Network Issues

```bash
# Check port binding
docker port yallarhorn

# Test connectivity from inside container
docker exec yallarhorn curl localhost:5001/health

# Test from host
curl http://localhost:5001/health
```

### Debug Mode

Enable verbose logging:

```bash
# Set environment variable
docker compose exec app env LOG_LEVEL=debug

# Or add to docker-compose.override.yml:
environment:
  - LOG_LEVEL=debug
```

### Common Error Messages

| Error | Solution |
|-------|----------|
| `port 5001 already in use` | Stop conflicting service or change port |
| `permission denied` | Check volume permissions |
| `config file not found` | Ensure yallarhorn.yaml exists and is mounted |
| `yt-dlp failed` | Update yt-dlp: `pip3 install --upgrade yt-dlp` |
| `ffmpeg not found` | Rebuild image to install ffmpeg |
| `database locked` | Ensure only one container instance |

### Getting Logs

```bash
# Container logs
docker compose logs --tail=100 app

# Follow in real-time
docker compose logs -f app

# Application logs (if configured)
docker exec yallarhorn cat /app/data/logs/yallarhorn.log

# Save logs to file
docker compose logs app > yallarhorn.log
```

---

## Quick Reference

### Essential Commands

```bash
# Build
docker build -t yallarhorn:latest .

# Run (docker-compose)
docker compose up -d

# Stop
docker compose down

# Logs
docker compose logs -f app

# Restart
docker compose restart app

# Update
docker compose build && docker compose up -d

# Health check
curl http://localhost:5001/health
```

### Volume Reference

| Container Path | Purpose | Recommended Size |
|----------------|---------|------------------|
| `/app/yallarhorn.yaml` | Configuration | < 1 MB |
| `/app/data` | Database | 100 MB - 1 GB |
| `/app/downloads` | Media files | 50+ GB |
| `/app/temp` | Processing | 5-10 GB |

### Port Reference

| Port | Protocol | Purpose |
|------|----------|---------|
| 5001 | HTTP | Web server / API / RSS feeds |

---

## See Also

- [Configuration Guide](./configuration.md) - Detailed configuration options
- [API Specification](./api-specification.md) - REST API documentation
- [Download Pipeline](./download-pipeline.md) - How downloads work