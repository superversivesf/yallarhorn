#!/bin/bash
# Deploy script for Yallarhorn Docker Compose deployment
# Preserves data volumes - use deploy-nuke.sh for fresh start

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

echo -e "${YELLOW}=== Yallarhorn Deployment Script ===${NC}"
echo -e "${YELLOW}(Preserving data volumes)${NC}\n"

# Check for .env file
if [ ! -f .env ]; then
    echo -e "${YELLOW}No .env file found, creating from .env.example${NC}"
    if [ -f .env.example ]; then
        cp .env.example .env
        echo -e "${GREEN}✓ Created .env file${NC}"
        echo -e "${YELLOW}NOTE: Edit .env to configure storage paths (especially YALLARHORN_DOWNLOADS_DIR)${NC}\n"
    fi
fi

# Step 1: Stop and remove current containers (preserve volumes)
echo -e "${YELLOW}[1/4] Stopping containers...${NC}"
docker compose down

# Step 2: Rebuild the Docker image
echo -e "${YELLOW}[2/4] Rebuilding Docker image...${NC}"
docker compose build --no-cache

# Step 3: Deploy with docker-compose
echo -e "${YELLOW}[3/4] Deploying containers...${NC}"
docker compose up -d

# Step 4: Wait for service to be healthy
echo -e "${YELLOW}[4/4] Waiting for service to be healthy...${NC}"
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

# Get and display version
echo -e "${YELLOW}Checking version...${NC}"
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