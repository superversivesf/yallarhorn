#!/bin/bash
# Deploy script for Yallarhorn Docker Compose deployment
# WARNING: Removes all data volumes for a fresh start

set -e  # Exit on any error

# Color codes for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Configuration
SERVICE_PORT=5001
MAX_HEALTH_RETRIES=30
HEALTH_CHECK_INTERVAL=2

echo -e "${RED}=== Yallarhorn Deployment Script (NUKE) ===${NC}"
echo -e "${RED}WARNING: This will delete all data!${NC}\n"

# Step 1: Stop and remove current containers
echo -e "${YELLOW}[1/6] Stopping and removing containers...${NC}"
docker compose down -v

# Step 2: Remove all yallarhorn volumes
echo -e "${YELLOW}[2/6] Removing yallarhorn volumes...${NC}"
VOLUMES=$(docker volume ls -q | grep yallarhorn || true)
if [ -n "$VOLUMES" ]; then
    echo "Removing volumes: $VOLUMES"
    echo "$VOLUMES" | xargs docker volume rm
else
    echo "No yallarhorn volumes found to remove"
fi

# Step 3: Rebuild the Docker image
echo -e "${YELLOW}[3/6] Rebuilding Docker image...${NC}"
docker compose build --no-cache

# Step 4: Deploy with docker-compose
echo -e "${YELLOW}[4/6] Deploying containers...${NC}"
docker compose up -d

# Step 5: Wait for service to be healthy
echo -e "${YELLOW}[5/6] Waiting for service to be healthy...${NC}"
RETRY_COUNT=0
while [ $RETRY_COUNT -lt $MAX_HEALTH_RETRIES ]; do
    if curl -f -s http://localhost:${SERVICE_PORT}/health > /dev/null 2>&1; then
        echo -e "${GREEN}✓ Service is healthy!${NC}"
        break
    fi
    
    RETRY_COUNT=$((RETRY_COUNT + 1))
    echo "  Attempt $RETRY_COUNT/$MAX_HEALTH_RETRIES - Service not ready yet..."
    sleep $HEALTH_CHECK_INTERVAL
done

if [ $RETRY_COUNT -eq $MAX_HEALTH_RETRIES ]; then
    echo -e "${RED}✗ Service failed to become healthy after $MAX_HEALTH_RETRIES attempts${NC}"
    echo "Checking container logs..."
    docker compose logs --tail=50
    exit 1
fi

# Step 6: Get and display version
echo -e "${YELLOW}[6/6] Checking version...${NC}"
VERSION_RESPONSE=$(curl -f -s http://localhost:${SERVICE_PORT}/version)

if [ $? -eq 0 ]; then
    echo -e "${GREEN}=== Deployment Successful ===${NC}"
    echo -e "${GREEN}Version: ${VERSION_RESPONSE}${NC}\n"
else
    echo -e "${RED}✗ Failed to get version from /version endpoint${NC}"
    exit 1
fi

# Show container status
echo -e "${YELLOW}Container Status:${NC}"
docker compose ps