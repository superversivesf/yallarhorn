# Docker Deployment Design

This document defines the container deployment strategy for Yallarhorn, a podcast server that downloads YouTube videos and serves them as RSS/Atom feeds.

## Table of Contents

- [Overview](#overview)
- [Container Architecture](#container-architecture)
- [Dockerfile](#dockerfile)
- [Base Images](#base-images)
- [Volume Mounts](#volume-mounts)
- [Environment Variables](#environment-variables)
- [Health Checks](#health-checks)
- [Docker Compose](#docker-compose)
- [Production Deployment](#production-deployment)
- [Updates & Maintenance](#updates--maintenance)
- [Resource Limits](#resource-limits)
- [Security Considerations](#security-considerations)
- [Troubleshooting](#troubleshooting)

---

## Overview

Yallarhorn is deployed as a single container with multi-stage build for optimized image size. The container requires:

- **.NET 10 Runtime**: Application runtime
- **yt-dlp**: YouTube video downloading
- **FFmpeg**: Audio/video transcoding

### Deployment Goals

1. **Minimal Image Size**: Multi-stage builds keep only runtime dependencies
2. **Stateless Container**: All persistent data via volume mounts
3. **Easy Configuration**: Environment variables for runtime config
4. **Observable**: Built-in health checks for orchestration
5. **Secure**: Non-root execution, minimal attack surface

---

## Container Architecture

### Multi-Stage Build Strategy

The Dockerfile uses three stages:

```
┌─────────────────────────────────────────────────────────────────┐
│  Stage 1: build                                                 │
│  - mcr.microsoft.com/dotnet/sdk:10.0                            │
│  - Restore dependencies                                         │
│  - Build application                                            │
│  - Run tests (optional)                                         │
│  - Publish self-contained artifact                              │
└─────────────────────────────────────────────────────────────────┘
                               │
                               ▼
┌─────────────────────────────────────────────────────────────────┐
│  Stage 2: tools                                                 │
│  - mcr.microsoft.com/dotnet/runtime-deps:10.0                   │
│  - Install yt-dlp                                               │
│  - Install FFmpeg                                               │
│  - Create non-root user                                         │
└─────────────────────────────────────────────────────────────────┘
                               │
                               ▼
┌─────────────────────────────────────────────────────────────────┐
│  Stage 3: runtime (final)                                       │
│  - Copy published application from build stage                  │
│  - Copy tools from tools stage                                  │
│  - Set up directories and permissions                           │
│  - Configure entrypoint                                         │
└─────────────────────────────────────────────────────────────────┘
```

### Single Container Design

Yallarhorn runs as a single container that handles:

- Web server (Kestrel)
- Background download queue
- Transcoding pipeline
- RSS/Atom feed generation

No separate sidecars or init containers are required.

---

## Dockerfile

```dockerfile
# =============================================================================
# Stage 1: Build
# =============================================================================
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

# Set working directory
WORKDIR /src

# Copy solution and project files for restore
COPY Yallarhorn.sln ./
COPY src/Yallarhorn/Yallarhorn.csproj ./src/Yallarhorn/
COPY tests/Yallarhorn.Tests/Yallarhorn.Tests.csproj ./tests/Yallarhorn.Tests/

# Restore dependencies
RUN dotnet restore

# Copy source code
COPY src/Yallarhorn ./src/Yallarhorn/
COPY tests/Yallarhorn.Tests ./tests/Yallarhorn.Tests/

# Build and test
RUN dotnet build --configuration Release --no-restore
RUN dotnet test --configuration Release --no-build --verbosity normal

# Publish application
RUN dotnet publish src/Yallarhorn/Yallarhorn.csproj \
    --configuration Release \
    --no-build \
    --output /app/publish \
    --runtime linux-x64 \
    --self-contained false

# =============================================================================
# Stage 2: Tools (install dependencies)
# =============================================================================
FROM mcr.microsoft.com/dotnet/runtime-deps:10.0-bookworm-slim AS tools

# Install dependencies for yt-dlp and ffmpeg
RUN apt-get update && apt-get install -y --no-install-recommends \
    curl \
    ca-certificates \
    gnupg \
    python3 \
    python3-pip \
    ffmpeg \
    && rm -rf /var/lib/apt/lists/*

# Install yt-dlp via pip (more reliable than binary download)
RUN pip3 install --no-cache-dir --break-system-packages yt-dlp

# Create non-root user for security
RUN groupadd --gid 1000 yallarhorn \
    && useradd --uid 1000 --gid yallarhorn --shell /bin/bash --create-home yallarhorn

# Create required directories
RUN mkdir -p /config /data/downloads /data/temp /data/db /data/logs \
    && chown -R yallarhorn:yallarhorn /config /data

# =============================================================================
# Stage 3: Runtime (final image)
# =============================================================================
FROM mcr.microsoft.com/dotnet/aspnet:10.0-bookworm-slim AS runtime

# Labels for image metadata
LABEL maintainer="Yallarhorn Team"
LABEL org.opencontainers.image.title="Yallarhorn"
LABEL org.opencontainers.image.description="YouTube to Podcast Server"
LABEL org.opencontainers.image.version="1.0.0"
LABEL org.opencontainers.image.source="https://github.com/example/yallarhorn"

# Copy tools from tools stage
COPY --from=tools /usr/bin/ffmpeg /usr/bin/ffmpeg
COPY --from=tools /usr/bin/ffprobe /usr/bin/ffprobe
COPY --from=tools /usr/local/bin/yt-dlp /usr/local/bin/yt-dlp
COPY --from=tools /usr/lib/python3.11 /usr/lib/python3.11
COPY --from=tools /usr/local/lib/python3.11 /usr/local/lib/python3.11

# Install runtime dependencies
RUN apt-get update && apt-get install -y --no-install-recommends \
    python3 \
    ca-certificates \
    && rm -rf /var/lib/apt/lists/* \
    && apt-get clean

# Copy user setup from tools stage
COPY --from=tools /etc/passwd /etc/passwd
COPY --from=tools /etc/group /etc/group

# Create directories with proper ownership
RUN mkdir -p /config /data/downloads /data/temp /data/db /data/logs \
    && chown -R yallarhorn:yallarhorn /config /data

# Copy published application from build stage
COPY --from=build /app/publish /app

# Set working directory
WORKDIR /app

# Switch to non-root user
USER yallarhorn

# Expose port
EXPOSE 8080

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=5s --retries=3 \
    CMD curl --fail http://localhost:8080/api/v1/health || exit 1

# Set environment variables
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1 \
    ASPNETCORE_URLS=http://0.0.0.0:8080 \
    ASPNETCORE_ENVIRONMENT=Production

# Volume mount points
VOLUME ["/config", "/data"]

# Entrypoint
ENTRYPOINT ["./Yallarhorn"]
CMD ["--config", "/config/yallarhorn.yaml"]
```

### Build Optimization Notes

| Technique | Description |
|-----------|-------------|
| Layer caching | Copy .csproj before source for better cache hits |
| Multi-stage | Build dependencies not included in final image |
| Self-contained | Using framework-dependent for smaller size |
| Order optimization | Least frequently changed layers first |

---

## Base Images

### Build Stage

| Image | Tag | Purpose |
|-------|-----|---------|
| `mcr.microsoft.com/dotnet/sdk` | `10.0` | .NET SDK for compilation |

**Includes**: Roslyn compiler, .NET runtime, NuGet client, build tools

### Runtime Stage

| Image | Tag | Purpose |
|-------|-----|---------|
| `mcr.microsoft.com/dotnet/aspnet` | `10.0-bookworm-slim` | ASP.NET Core runtime |

**Includes**: .NET runtime, ASP.NET Core, native dependencies

### Tools Installation

| Tool | Version | Installation Method |
|------|---------|---------------------|
| `yt-dlp` | Latest | pip3 install |
| `ffmpeg` | Debian package | apt-get |
| `python3` | 3.11 | Debian base |

### Image Size Optimization

```dockerfile
# Minimize layers by combining RUN commands
RUN apt-get update && apt-get install -y --no-install-recommends \
    tool1 \
    tool2 \
    && rm -rf /var/lib/apt/lists/* \
    && apt-get clean

# Use --no-install-recommends to avoid unnecessary packages
```

---

## Volume Mounts

### Directory Structure

```
/config
├── yallarhorn.yaml              # Main configuration file
└── .env                         # Environment file (optional)

/data
├── downloads/                   # Downloaded media files
│   ├── channel-1/
│   │   ├── audio/
│   │   └── video/
│   └── channel-2/
│       ├── audio/
│       └── video/
├── temp/                        # Temporary processing files
├── db/                          # SQLite database
│   └── yallarhorn.db
└── logs/                        # Application logs
    └── yallarhorn.log
```

### Volume Configuration

| Mount Point | Purpose | Recommended Size |
|-------------|---------|------------------|
| `/config` | Configuration files | 100 MB |
| `/data` | All persistent data | 100+ GB |

### Docker Run Example

```bash
docker run -d \
  --name yallarhorn \
  --restart unless-stopped \
  -p 8080:8080 \
  -v /path/to/config:/config:ro \
  -v /path/to/data:/data \
  -e FEED_PASSWORD=secure_password \
  -e ADMIN_PASSWORD=admin_password \
  yallarhorn:latest
```

### Named Volumes Alternative

```bash
# Create named volumes
docker volume create yallarhorn-config
docker volume create yallarhorn-data

# Run with named volumes
docker run -d \
  --name yallarhorn \
  -p 8080:8080 \
  -v yallarhorn-config:/config \
  -v yallarhorn-data:/data \
  yallarhorn:latest
```

### Permissions

The container runs as user `yallarhorn` (UID 1000, GID 1000). Ensure volume directories have correct permissions:

```bash
# Prepare host directories
mkdir -p /path/to/config /path/to/data
chown -R 1000:1000 /path/to/config /path/to/data
chmod -R 755 /path/to/config /path/to/data
```

---

## Environment Variables

### Core Configuration

| Variable | Description | Default | Example |
|----------|-------------|---------|---------|
| `ASPNETCORE_ENVIRONMENT` | Runtime environment | `Production` | `Development`, `Staging`, `Production` |
| `ASPNETCORE_URLS` | Server bind address | `http://0.0.0.0:8080` | `http://0.0.0.0:8080` |

### Yallarhorn Configuration

| Variable | Description | Required |
|----------|-------------|----------|
| `FEED_PASSWORD` | Password for feed access | If auth enabled |
| `ADMIN_PASSWORD` | Password for admin API | If auth enabled |
| `DATA_DIR` | Base data directory | No (default: `/data`) |
| `CONFIG_DIR` | Config directory | No (default: `/config`) |
| `LOG_LEVEL` | Logging level | No (default: `info`) |
| `POLL_INTERVAL` | Channel refresh interval (seconds) | No (default: `3600`) |
| `MAX_DOWNLOADS` | Max concurrent downloads | No (default: `3`) |

### Configuration File Substitution

Environment variables can be used in `yallarhorn.yaml`:

```yaml
# Configuration with environment variable substitution
auth:
  feed_credentials:
    enabled: true
    username: "feed-user"
    password: "${FEED_PASSWORD:?FEED_PASSWORD is required}"

  admin_auth:
    enabled: true
    username: "admin"
    password: "${ADMIN_PASSWORD:?ADMIN_PASSWORD is required}"

server:
  host: "0.0.0.0"
  port: 8080
  base_url: "${BASE_URL:-http://localhost:8080}"

database:
  path: "${DATA_DIR:-/data}/db/yallarhorn.db"

logging:
  level: "${LOG_LEVEL:-info}"
  file: "${DATA_DIR:-/data}/logs/yallarhorn.log"
```

### Environment File

Create `.env` file for local development:

```bash
# .env
FEED_PASSWORD=your-secure-feed-password
ADMIN_PASSWORD=your-secure-admin-password
BASE_URL=http://your-domain.com:8080
LOG_LEVEL=debug
POLL_INTERVAL=1800
MAX_DOWNLOADS=5
```

Load environment file:

```bash
docker run -d \
  --env-file .env \
  -v /path/to/config:/config \
  -v /path/to/data:/data \
  yallarhorn:latest
```

---

## Health Checks

### Built-in Health Check

The Dockerfile includes a health check that queries the API health endpoint:

```dockerfile
HEALTHCHECK --interval=30s --timeout=10s --start-period=5s --retries=3 \
    CMD curl --fail http://localhost:8080/api/v1/health || exit 1
```

### Health Check Parameters

| Parameter | Value | Description |
|-----------|-------|-------------|
| `--interval` | 30s | Time between checks |
| `--timeout` | 10s | Max time for check to complete |
| `--start-period` | 5s | Grace period for container startup |
| `--retries` | 3 | Failures before unhealthy |

### Health Endpoint Response

The `/api/v1/health` endpoint returns:

```json
{
  "status": "healthy",
  "version": "1.0.0",
  "timestamp": "2024-01-15T12:00:00.000Z"
}
```

### Health Status Values

| Status | Description |
|--------|-------------|
| `healthy` | All services operational |
| `degraded` | Non-critical issues (low disk, slow DB) |
| `unhealthy` | Critical issues (DB unreachable, storage failure) |

### Kubernetes Liveness/Readiness Probes

```yaml
livenessProbe:
  httpGet:
    path: /api/v1/health
    port: 8080
  initialDelaySeconds: 10
  periodSeconds: 30
  timeoutSeconds: 5
  failureThreshold: 3

readinessProbe:
  httpGet:
    path: /api/v1/health
    port: 8080
  initialDelaySeconds: 5
  periodSeconds: 10
  timeoutSeconds: 3
  failureThreshold: 2
```

### Docker Compose Health Check

```yaml
healthcheck:
  test: ["CMD", "curl", "--fail", "http://localhost:8080/api/v1/health"]
  interval: 30s
  timeout: 10s
  retries: 3
  start_period: 10s
```

---

## Docker Compose

### Basic Configuration

```yaml
version: '3.8'

services:
  yallarhorn:
    image: yallarhorn:latest
    container_name: yallarhorn
    restart: unless-stopped
    
    ports:
      - "8080:8080"
    
    volumes:
      - ./config:/config:ro
      - yallarhorn-data:/data
    
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - FEED_PASSWORD=${FEED_PASSWORD}
      - ADMIN_PASSWORD=${ADMIN_PASSWORD}
      - BASE_URL=${BASE_URL:-http://localhost:8080}
    
    healthcheck:
      test: ["CMD", "curl", "--fail", "http://localhost:8080/api/v1/health"]
      interval: 30s
      timeout: 10s
      retries: 3
      start_period: 10s

volumes:
  yallarhorn-data:
    driver: local
```

### Full Production Configuration

```yaml
version: '3.8'

services:
  yallarhorn:
    image: yallarhorn:latest
    container_name: yallarhorn
    restart: unless-stopped
    
    ports:
      - "8080:8080"
    
    volumes:
      - ./config:/config:ro
      - yallarhorn-data:/data
      - yallarhorn-logs:/data/logs
    
    environment:
      # Server configuration
      - ASPNETCORE_ENVIRONMENT=Production
      - ASPNETCORE_URLS=http://0.0.0.0:8080
      
      # Application configuration
      - FEED_PASSWORD=${FEED_PASSWORD}
      - ADMIN_PASSWORD=${ADMIN_PASSWORD}
      - BASE_URL=${BASE_URL}
      - LOG_LEVEL=${LOG_LEVEL:-info}
      - POLL_INTERVAL=${POLL_INTERVAL:-3600}
      - MAX_DOWNLOADS=${MAX_DOWNLOADS:-3}
      
      # .NET optimization
      - DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1
      - DOTNET_gcServer=1
    
    # Resource limits
    deploy:
      resources:
        limits:
          cpus: '2'
          memory: 4G
        reservations:
          cpus: '0.5'
          memory: 512M
    
    # Health check
    healthcheck:
      test: ["CMD", "curl", "--fail", "http://localhost:8080/api/v1/health"]
      interval: 30s
      timeout: 10s
      retries: 3
      start_period: 15s
    
    # Logging configuration
    logging:
      driver: "json-file"
      options:
        max-size: "10m"
        max-file: "5"
    
    # Security
    security_opt:
      - no-new-privileges:true
    read_only: false
    tmpfs:
      - /tmp:size=100M,mode=1777
    
    # Network
    networks:
      - yallarhorn-network

  # Optional: Reverse proxy
  nginx:
    image: nginx:alpine
    container_name: yallarhorn-proxy
    restart: unless-stopped
    
    ports:
      - "80:80"
      - "443:443"
    
    volumes:
      - ./nginx/nginx.conf:/etc/nginx/nginx.conf:ro
      - ./nginx/ssl:/etc/nginx/ssl:ro
    
    depends_on:
      yallarhorn:
        condition: service_healthy
    
    networks:
      - yallarhorn-network

networks:
  yallarhorn-network:
    driver: bridge

volumes:
  yallarhorn-data:
    driver: local
  yallarhorn-logs:
    driver: local
```

### Environment File

Create `.env` in the same directory as `docker-compose.yml`:

```bash
# Required
FEED_PASSWORD=your-secure-feed-password-here
ADMIN_PASSWORD=your-secure-admin-password-here
BASE_URL=https://your-domain.com

# Optional
LOG_LEVEL=info
POLL_INTERVAL=3600
MAX_DOWNLOADS=3
```

### Commands

```bash
# Start services
docker-compose up -d

# View logs
docker-compose logs -f yallarhorn

# Check health
docker-compose ps

# Restart service
docker-compose restart yallarhorn

# Stop services
docker-compose down

# Stop and remove volumes
docker-compose down -v

# Update to new image
docker-compose pull yallarhorn
docker-compose up -d yallarhorn
```

---

## Production Deployment

### Prerequisites

1. **Host Requirements**
   - Linux server (Debian 12+ or Ubuntu 22.04+ recommended)
   - Docker 24.0+ or Docker Compose v2
   - Minimum 2 GB RAM, 2 CPU cores
   - Sufficient storage for media (100+ GB recommended)

2. **Network Requirements**
   - Outbound HTTPS access (YouTube, yt-dlp updates)
   - Inbound HTTP/HTTPS for feed access

### Deployment Steps

#### 1. Prepare Host

```bash
# Create application directory
sudo mkdir -p /opt/yallarhorn/{config,data}
cd /opt/yallarhorn

# Set permissions (container runs as UID 1000)
sudo chown -R 1000:1000 config data

# Create configuration file
sudo nano config/yallarhorn.yaml
```

#### 2. Create Configuration

```yaml
# /opt/yallarhorn/config/yallarhorn.yaml
version: "1.0"

# Global settings
poll_interval: 3600
max_concurrent_downloads: 3
download_dir: "/data/downloads"
temp_dir: "/data/temp"

# Server configuration
server:
  host: "0.0.0.0"
  port: 8080
  base_url: "${BASE_URL}"

# Database
database:
  path: "/data/db/yallarhorn.db"
  pool_size: 5

# Logging
logging:
  level: "${LOG_LEVEL:-info}"
  file: "/data/logs/yallarhorn.log"

# Authentication
auth:
  feed_credentials:
    enabled: true
    username: "feed-user"
    password: "${FEED_PASSWORD:?FEED_PASSWORD is required}"
  
  admin_auth:
    enabled: true
    username: "admin"
    password: "${ADMIN_PASSWORD:?ADMIN_PASSWORD is required}"

# Transcoding
transcode_settings:
  audio_format: "mp3"
  audio_bitrate: "192k"
  video_format: "mp4"
  video_codec: "h264"

# Channels (configure as needed)
channels:
  - name: "Example Channel"
    url: "https://www.youtube.com/@example"
    episode_count: 50
    enabled: true
    feed_type: "audio"
```

#### 3. Create Environment File

```bash
# /opt/yallarhorn/.env
FEED_PASSWORD=$(openssl rand -base64 32)
ADMIN_PASSWORD=$(openssl rand -base64 32)
BASE_URL=https://your-domain.com
LOG_LEVEL=info
```

#### 4. Deploy with Docker Compose

```bash
# Pull latest image
docker-compose pull

# Start services
docker-compose up -d

# Check status
docker-compose ps
docker-compose logs -f
```

#### 5. Verify Deployment

```bash
# Health check
curl http://localhost:8080/api/v1/health

# Status check
curl -u admin:$ADMIN_PASSWORD http://localhost:8080/api/v1/status
```

### Systemd Service (Optional)

Create systemd service for Docker Compose:

```ini
# /etc/systemd/system/yallarhorn.service
[Unit]
Description=Yallarhorn Podcast Server
Requires=docker.service
After=docker.service

[Service]
Type=simple
User=root
WorkingDirectory=/opt/yallarhorn
ExecStart=/usr/bin/docker-compose up
ExecStop=/usr/bin/docker-compose down
Restart=on-failure
RestartSec=10

[Install]
WantedBy=multi-user.target
```

```bash
# Enable and start
sudo systemctl daemon-reload
sudo systemctl enable yallarhorn
sudo systemctl start yallarhorn

# Check status
sudo systemctl status yallarhorn
```

---

## Updates & Maintenance

### Updating the Application

#### Rolling Update (Zero Downtime)

```bash
# 1. Pull new image
docker-compose pull yallarhorn

# 2. Start new container alongside old one
docker-compose up -d --no-deps --scale yallarhorn=2 yallarhorn

# 3. Wait for new container to be healthy
sleep 30

# 4. Scale down to single instance
docker-compose up -d --no-deps --scale yallarhorn=1 yallarhorn

# 5. Clean up old containers
docker-compose up -d
```

#### Standard Update (Brief Downtime)

```bash
# 1. Pull new image
docker-compose pull yallarhorn

# 2. Recreate container
docker-compose up -d --force-recreate yallarhorn

# 3. Check logs
docker-compose logs -f yallarhorn
```

### Database Backup

```bash
# Create backup
docker exec yallarhorn sqlite3 /data/db/yallarhorn.db ".backup /data/db/backup.db"

# Copy backup to host
docker cp yallarhorn:/data/db/backup.db ./backup-$(date +%Y%m%d).db

# Or simple file copy
cp /opt/yallarhorn/data/db/yallarhorn.db ./backup-$(date +%Y%m%d).db
```

### Configuration Updates

```bash
# 1. Edit configuration
nano /opt/yallarhorn/config/yallarhorn.yaml

# 2. Validate configuration
docker exec yallarhorn ./Yallarhorn config validate

# 3. Restart to apply changes
docker-compose restart yallarhorn
```

### yt-dlp Updates

yt-dlp is updated frequently to handle YouTube changes. Update the container to get the latest version:

```bash
# Rebuild image with latest yt-dlp
docker-compose build --no-cache yallarhorn
docker-compose up -d yallarhorn
```

Or update yt-dlp in a running container (temporary):

```bash
docker exec yallarhorn pip3 install --upgrade yt-dlp
# Note: This will be lost on container restart
```

### Log Management

```bash
# View recent logs
docker-compose logs --tail=100 yallarhorn

# View application logs
docker exec yallarhorn cat /data/logs/yallarhorn.log

# Rotate logs (if not using Docker log rotation)
docker exec yallarhorn mv /data/logs/yallarhorn.log /data/logs/yallarhorn.log.old
docker-compose restart yallarhorn
```

---

## Resource Limits

### Memory Requirements

| Component | Minimum | Recommended |
|-----------|---------|-------------|
| Application | 256 MB | 512 MB |
| FFmpeg transcoding | 256 MB per stream | 512 MB per stream |
| yt-dlp downloads | 128 MB | 256 MB |
| **Total** | 1 GB | 2-4 GB |

### CPU Requirements

| Operation | CPU Impact |
|-----------|------------|
| Web serving | Low |
| Download (1 stream) | 1 core |
| Transcoding (1 stream) | 1-2 cores |
| Background refresh | Low |

### Docker Resource Limits

```yaml
services:
  yallarhorn:
    deploy:
      resources:
        limits:
          cpus: '2.0'
          memory: 4G
        reservations:
          cpus: '0.5'
          memory: 512M
```

### Kubernetes Resource Limits

```yaml
resources:
  requests:
    memory: "512Mi"
    cpu: "500m"
  limits:
    memory: "4Gi"
    cpu: "2000m"
```

### Storage Planning

| Content Type | Size per Episode | Est. 50 Episodes |
|--------------|------------------|------------------|
| Audio (MP3 192kbps) | ~50-80 MB | ~3-4 GB |
| Video (MP4 720p) | ~300-500 MB | ~15-25 GB |
| Video (MP4 1080p) | ~500-800 MB | ~25-40 GB |

**Total for 10 channels (audio only)**: ~30-40 GB
**Total for 10 channels (video)**: ~150-400 GB

---

## Security Considerations

### Container Security

1. **Non-root User**: Container runs as `yallarhorn` (UID 1000)
2. **Read-only Filesystem**: Config volume mounted read-only
3. **No New Privileges**: `no-new-privileges` security option
4. **Minimal Image**: Only required runtime dependencies

### Network Security

```yaml
# Restrict to internal network
services:
  yallarhorn:
    networks:
      - internal

  nginx:
    networks:
      - internal
      - external

networks:
  internal:
    internal: true
  external:
    driver: bridge
```

### Reverse Proxy (HTTPS)

Use nginx or Traefik for TLS termination:

```nginx
# nginx/nginx.conf
server {
    listen 443 ssl http2;
    server_name podcast.your-domain.com;

    ssl_certificate /etc/nginx/ssl/cert.pem;
    ssl_certificate_key /etc/nginx/ssl/key.pem;

    client_max_body_size 100M;

    location / {
        proxy_pass http://yallarhorn:8080;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }
}
```

### Secrets Management

Avoid storing secrets in environment files:

```yaml
# Using Docker secrets (Swarm mode)
secrets:
  feed_password:
    external: true
  admin_password:
    external: true

services:
  yallarhorn:
    secrets:
      - feed_password
      - admin_password
```

```bash
# Create secrets
echo "your-secure-password" | docker secret create feed_password -
echo "your-admin-password" | docker secret create admin_password -
```

---

## Troubleshooting

### Common Issues

#### Container Won't Start

```bash
# Check logs
docker-compose logs yallarhorn

# Common causes:
# 1. Missing configuration file
# 2. Invalid YAML syntax
# 3. Missing required environment variables
# 4. Permission issues on volumes
```

#### Permission Denied

```bash
# Fix volume permissions
sudo chown -R 1000:1000 /opt/yallarhorn/data
sudo chown -R 1000:1000 /opt/yallarhorn/config
```

#### Downloads Failing

```bash
# Check yt-dlp version
docker exec yallarhorn yt-dlp --version

# Update yt-dlp
docker exec yallarhorn pip3 install --upgrade yt-dlp

# Test download manually
docker exec -it yallarhorn bash
yt-dlp --simulate "https://www.youtube.com/watch?v=VIDEO_ID"
```

#### Database Locked

```bash
# Stop container
docker-compose stop yallarhorn

# Check for lock file
ls -la /opt/yallarhorn/data/db/

# Remove lock file if exists
rm /opt/yallarhorn/data/db/yallarhorn.db-wal
rm /opt/yallarhorn/data/db/yallarhorn.db-shm

# Restart
docker-compose start yallarhorn
```

### Debug Mode

Enable debug logging:

```bash
# Set environment variable
docker-compose exec yallarhorn env LOG_LEVEL=debug

# Or in docker-compose.yml
environment:
  - LOG_LEVEL=debug
```

### Health Check Failures

```bash
# Manual health check
docker exec yallarhorn curl -v http://localhost:8080/api/v1/health

# Check application status
docker-compose exec yallarhorn ./Yallarhorn status
```

### Log Analysis

```bash
# View recent container logs
docker-compose logs --tail=200 yallarhorn

# Follow logs in real-time
docker-compose logs -f yallarhorn

# View application logs
docker exec yallarhorn tail -f /data/logs/yallarhorn.log

# Search logs
docker exec yallarhorn grep -i error /data/logs/yallarhorn.log
```

### Performance Issues

```bash
# Check resource usage
docker stats yallarhorn

# Check disk usage
docker exec yallarhorn df -h

# Check database size
docker exec yallarhorn ls -lh /data/db/

# Monitor active downloads
curl -u admin:$ADMIN_PASSWORD http://localhost:8080/api/v1/queue
```

---

## Appendix: Quick Reference

### Docker Commands

```bash
# Build image
docker build -t yallarhorn:latest .

# Run container
docker run -d \
  --name yallarhorn \
  -p 8080:8080 \
  -v $(pwd)/config:/config:ro \
  -v $(pwd)/data:/data \
  yallarhorn:latest

# View logs
docker logs -f yallarhorn

# Execute command in container
docker exec -it yallarhorn bash

# Check health
docker inspect --format='{{.State.Health.Status}}' yallarhorn
```

### Docker Compose Commands

```bash
# Start
docker-compose up -d

# Stop
docker-compose down

# Restart
docker-compose restart

# Logs
docker-compose logs -f

# Update
docker-compose pull && docker-compose up -d

# Scale
docker-compose up -d --scale yallarhorn=2
```

### Environment Variables Reference

```bash
# Required
FEED_PASSWORD=          # Feed authentication password
ADMIN_PASSWORD=         # Admin API password

# Optional
BASE_URL=               # Public URL for feed generation
LOG_LEVEL=              # debug, info, warn, error
POLL_INTERVAL=          # Channel refresh interval (seconds)
MAX_DOWNLOADS=          # Max concurrent downloads
ASPNETCORE_ENVIRONMENT= # Development, Staging, Production
```

---

## See Also

- [Configuration Schema](./configuration.md) - YAML configuration reference
- [API Specification](./api-specification.md) - REST API documentation
- [Data Model](./data-model.md) - Database schema documentation
- [Download Pipeline](./download-pipeline.md) - Download and transcoding details