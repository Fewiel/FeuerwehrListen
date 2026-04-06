#!/bin/bash
# =====================================================
# FeuerwehrListen Server Setup Script
# =====================================================
# Richtet die Anwendung auf einem Ubuntu-Server ein:
# - Installiert Docker (falls nötig)
# - Startet die Anwendung
# - Richtet Auto-Update per Cron ein
#
# Nutzung: sudo bash setup.sh
# =====================================================

set -euo pipefail

# --- Farben ---
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m'

log_info()    { echo -e "${BLUE}[INFO]${NC} $1"; }
log_success() { echo -e "${GREEN}[✓]${NC} $1"; }
log_warning() { echo -e "${YELLOW}[!]${NC} $1"; }
log_error()   { echo -e "${RED}[✗]${NC} $1"; }

# --- Root-Check ---
if [ "$EUID" -ne 0 ]; then
    log_error "Bitte als root ausführen: sudo bash setup.sh"
    exit 1
fi

echo ""
echo "======================================================"
echo "  FeuerwehrListen Server Setup"
echo "======================================================"
echo ""

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

# =====================================================
# SCHRITT 1: Docker installieren (falls nötig)
# =====================================================
if command -v docker &> /dev/null; then
    log_success "Docker ist bereits installiert: $(docker --version)"
else
    log_info "Installiere Docker..."

    apt-get update -qq
    apt-get install -y -qq ca-certificates curl > /dev/null

    install -m 0755 -d /etc/apt/keyrings
    curl -fsSL https://download.docker.com/linux/ubuntu/gpg -o /etc/apt/keyrings/docker.asc
    chmod a+r /etc/apt/keyrings/docker.asc

    echo \
      "deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/docker.asc] https://download.docker.com/linux/ubuntu \
      $(. /etc/os-release && echo "$VERSION_CODENAME") stable" | \
      tee /etc/apt/sources.list.d/docker.list > /dev/null

    apt-get update -qq
    apt-get install -y -qq docker-ce docker-ce-cli containerd.io docker-buildx-plugin docker-compose-plugin > /dev/null

    log_success "Docker installiert"
fi

# =====================================================
# SCHRITT 2: Anwendung starten
# =====================================================
echo ""
log_info "Starte Anwendung..."

cd "$SCRIPT_DIR"
docker compose up --build -d

log_success "Anwendung gestartet"

# =====================================================
# SCHRITT 3: Auto-Update Cron einrichten
# =====================================================
echo ""
log_info "Richte Auto-Update ein..."

# Script ausführbar machen
chmod +x "$SCRIPT_DIR/auto-update.sh"

# Cron-Eintrag (alle 5 Minuten)
CRON_CMD="*/5 * * * * cd $SCRIPT_DIR && /bin/bash $SCRIPT_DIR/auto-update.sh"
CRON_MARKER="# feuerwehrlisten-auto-update"

# Bestehenden Eintrag entfernen falls vorhanden
crontab -l 2>/dev/null | grep -v "$CRON_MARKER" | crontab - 2>/dev/null || true

# Neuen Eintrag hinzufügen
(crontab -l 2>/dev/null; echo "$CRON_CMD $CRON_MARKER") | crontab -

log_success "Auto-Update Cron eingerichtet (alle 5 Minuten)"

# =====================================================
# ABSCHLUSS
# =====================================================
echo ""
echo "======================================================"
log_success "Setup abgeschlossen!"
echo "======================================================"
echo ""
log_info "Zusammenfassung:"
echo "  • Anwendung:   http://$(hostname -I | awk '{print $1}'):8080"
echo "  • Auto-Update: Alle 5 Minuten (Cron)"
echo "  • Update-Log:  $SCRIPT_DIR/auto-update.log"
echo ""
log_info "Nützliche Befehle:"
echo "  • Logs ansehen:       sudo docker compose logs -f"
echo "  • Update-Log ansehen: tail -f $SCRIPT_DIR/auto-update.log"
echo "  • Manuell updaten:    sudo bash $SCRIPT_DIR/auto-update.sh"
echo "  • Stoppen:            sudo docker compose down"
echo ""
