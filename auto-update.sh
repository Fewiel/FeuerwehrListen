#!/bin/bash
# =====================================================
# FeuerwehrListen Auto-Update Script
# =====================================================
# Prüft ob neue Commits auf dem Master-Branch vorliegen
# und aktualisiert den Docker-Container automatisch.
#
# Wird per Cron alle 5 Minuten ausgeführt.
# =====================================================

set -euo pipefail

# --- KONFIGURATION ---
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_DIR="$SCRIPT_DIR"
LOG_FILE="$REPO_DIR/auto-update.log"
LOCK_FILE="/tmp/feuerwehrlisten-update.lock"
MAX_LOG_LINES=500

# --- LOGGING ---
log() {
    echo "[$(date '+%Y-%m-%d %H:%M:%S')] $1" | tee -a "$LOG_FILE"
}

# --- LOG ROTATION ---
rotate_log() {
    if [ -f "$LOG_FILE" ] && [ "$(wc -l < "$LOG_FILE")" -gt "$MAX_LOG_LINES" ]; then
        tail -n "$MAX_LOG_LINES" "$LOG_FILE" > "$LOG_FILE.tmp"
        mv "$LOG_FILE.tmp" "$LOG_FILE"
    fi
}

# --- LOCK (verhindert parallele Ausführungen) ---
if [ -f "$LOCK_FILE" ]; then
    LOCK_PID=$(cat "$LOCK_FILE" 2>/dev/null || echo "")
    if [ -n "$LOCK_PID" ] && kill -0 "$LOCK_PID" 2>/dev/null; then
        log "Update läuft bereits (PID $LOCK_PID). Abbruch."
        exit 0
    fi
    rm -f "$LOCK_FILE"
fi
echo $$ > "$LOCK_FILE"
trap 'rm -f "$LOCK_FILE"' EXIT

# --- UPDATE PRÜFUNG ---
cd "$REPO_DIR"

# Aktuelle Version merken
LOCAL_COMMIT=$(git rev-parse HEAD)

# Remote abfragen
git fetch origin master --quiet 2>/dev/null

REMOTE_COMMIT=$(git rev-parse origin/master)

# Vergleichen
if [ "$LOCAL_COMMIT" = "$REMOTE_COMMIT" ]; then
    rotate_log
    exit 0
fi

# --- UPDATE DURCHFÜHREN ---
log "Neues Update gefunden!"
log "Lokal:  $(git rev-parse --short HEAD) - $(git log -1 --format='%s' HEAD)"
log "Remote: $(git rev-parse --short origin/master) - $(git log -1 --format='%s' origin/master)"

# Code aktualisieren
log "Lade neuen Code..."
git pull origin master --quiet

# Container neu bauen und starten
log "Baue Container neu..."
docker compose up --build -d 2>&1 | tee -a "$LOG_FILE"

# Alte Images aufräumen
docker image prune -f --filter "until=24h" > /dev/null 2>&1 || true

NEW_COMMIT=$(git rev-parse --short HEAD)
log "Update erfolgreich! Neue Version: $NEW_COMMIT"

rotate_log
