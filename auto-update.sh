#!/bin/bash
# FeuerwehrListen Auto-Update (Cron alle 5 Min)
# Cronjob: */5 * * * * /bin/bash /home/docker/FeuerwehrListen/auto-update.sh

set -euo pipefail

REPO_DIR="/home/docker/FeuerwehrListen"
LOG_FILE="$REPO_DIR/auto-update.log"

log() { echo "[$(date '+%Y-%m-%d %H:%M:%S')] $1" >> "$LOG_FILE"; }

# Lock gegen parallele Ausführung
LOCK_FILE="/tmp/feuerwehrlisten-update.lock"
exec 200>"$LOCK_FILE"
flock -n 200 || exit 0

cd "$REPO_DIR"

git fetch origin master --quiet
git pull origin master --quiet
docker compose up --build -d >> "$LOG_FILE" 2>&1
docker image prune -f > /dev/null 2>&1 || true

log "Update durchgeführt: $(git rev-parse --short HEAD)"

# Log auf 500 Zeilen begrenzen
tail -n 500 "$LOG_FILE" > "$LOG_FILE.tmp" && mv "$LOG_FILE.tmp" "$LOG_FILE"
