#!/bin/bash
# Reset script - clears database and download queue for fresh start
# Usage: ./reset-data.sh

set -e

echo "=== This will DELETE all episodes and download queue entries ==="
echo "Channels will be preserved but their episodes cleared."
echo ""
read -p "Continue? (y/N) " -n 1 -r
echo ""

if [[ ! $REPLY =~ ^[Yy]$ ]]; then
    echo "Aborted."
    exit 1
fi

DB_PATH="${1:-./data/yallarhorn.db}"

if [ ! -f "$DB_PATH" ]; then
    echo "Database not found at: $DB_PATH"
    exit 1
fi

echo "Backing up database..."
cp "$DB_PATH" "${DB_PATH}.backup-$(date +%Y%m%d%H%M%S)"

echo "Clearing episodes..."
sqlite3 "$DB_PATH" "DELETE FROM episodes;"

echo "Clearing download queue..."
sqlite3 "$DB_PATH" "DELETE FROM download_queue;"

echo "Resetting channel last_refresh_at..."
sqlite3 "$DB_PATH" "UPDATE channels SET last_refresh_at = NULL;"

echo ""
echo "=== Done ==="
echo "Database reset. Run setup-test-channels.sh to add channels and refresh."