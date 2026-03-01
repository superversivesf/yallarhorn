#!/bin/bash
# Quick setup script to add test channels and trigger refresh
# Usage: ./setup-test-channels.sh [BASE_URL]
# Example: ./setup-test-channels.sh http://localhost:8080

set -e

BASE_URL="${1:-http://localhost:8080}"

echo "=== Using base URL: $BASE_URL ==="

# Health check
echo "Checking health..."
HEALTH=$(curl -s "${BASE_URL}/health" 2>/dev/null || echo "failed")
if [ "$HEALTH" == "failed" ]; then
    echo "ERROR: Server not responding at ${BASE_URL}"
    exit 1
fi
echo "Server is healthy!"
echo ""

echo "=== Adding test channels ==="

# Add Nate B Jones channel
echo "Adding Nate B Jones channel..."
curl -s -X POST "${BASE_URL}/api/v1/channels" \
  -H "Content-Type: application/json" \
  -d '{
    "url": "https://www.youtube.com/@NateBJones",
    "title": "Nate B Jones",
    "episodeCount": 3,
    "enabled": true,
    "feedType": "audio"
  }' | jq '.' 2>/dev/null || echo "(jq not installed)"
echo ""

# Add AI Species channel  
echo "Adding AI Species channel..."
curl -s -X POST "${BASE_URL}/api/v1/channels" \
  -H "Content-Type: application/json" \
  -d '{
    "url": "https://www.youtube.com/@AISpecies",
    "title": "AI Species",
    "episodeCount": 3,
    "enabled": true,
    "feedType": "audio"
  }' | jq '.' 2>/dev/null || echo "(jq not installed)"
echo ""

echo "=== Listing all channels ==="
curl -s "${BASE_URL}/api/v1/channels" | jq '.data[] | {id, title, enabled}' 2>/dev/null || \
curl -s "${BASE_URL}/api/v1/channels"
echo ""

echo "=== Triggering refresh on all enabled channels ==="
# Get enabled channel IDs and refresh each
ENABLED_IDS=$(curl -s "${BASE_URL}/api/v1/channels?enabled=true" | jq -r '.data[] | select(.enabled == true) | .id' 2>/dev/null)

if [ -n "$ENABLED_IDS" ]; then
    for ID in $ENABLED_IDS; do
        echo "Triggering refresh for: $ID"
        curl -s -X POST "${BASE_URL}/api/v1/channels/${ID}/refresh" > /dev/null &
    done
    wait
    echo "Refresh triggered for all channels"
else
    echo "No enabled channels found or jq not available - trying to refresh by URL pattern"
    # Fallback: find channels by known URLs
    for URL in "@NateBJones" "@AISpecies"; do
        ID=$(curl -s "${BASE_URL}/api/v1/channels" | jq -r ".data[] | select(.url | contains(\"$URL\")) | .id" 2>/dev/null | head -1)
        if [ -n "$ID" ]; then
            echo "Found channel $ID for $URL, refreshing..."
            curl -s -X POST "${BASE_URL}/api/v1/channels/${ID}/refresh" > /dev/null &
        fi
    done
    wait
fi

echo ""
echo "=== Checking download queue ==="
sleep 2
curl -s "${BASE_URL}/api/v1/status" | jq '.' 2>/dev/null || \
echo "(status endpoint not available)"
echo ""

echo "=== Done ===" 
echo "Monitor logs with: docker logs -f <container>"
echo "Check downloads: ls -la downloads/"