#!/bin/bash
set -e

# =====================================================
# FeuerwehrListen Update Script
# =====================================================
# Dieses Script aktualisiert die FeuerwehrListen-Anwendung
# OHNE die Datenbank zu zerstören!
# =====================================================

# --- KONFIGURATION ---
APP_ROOT="/var/www/feuerwehrlisten"
REPO_DIR="$APP_ROOT/repo"
PUBLISH_DIR="$APP_ROOT/publish"
TEMP_PUBLISH_DIR="/tmp/feuerwehrlisten-publish-$(date +%s)"
DB_DIR="$APP_ROOT/data"
DB_FILE="$DB_DIR/feuerwehr.db"
BACKUP_DIR="$APP_ROOT/backups"
SERVICE_NAME="feuerwehrlisten"

# Farben für Output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# --- LOGGING FUNKTIONEN ---
log_info() {
    echo -e "${BLUE}[INFO]${NC} $1"
}

log_success() {
    echo -e "${GREEN}[✓]${NC} $1"
}

log_warning() {
    echo -e "${YELLOW}[!]${NC} $1"
}

log_error() {
    echo -e "${RED}[✗]${NC} $1"
}

# --- FEHLERBEHANDLUNG ---
cleanup_on_error() {
    log_error "Ein Fehler ist aufgetreten! Führe Cleanup durch..."
    rm -rf "$TEMP_PUBLISH_DIR"
    systemctl start $SERVICE_NAME 2>/dev/null || true
    exit 1
}

trap cleanup_on_error ERR

# =====================================================
# SCHRITT 1: VORBEREITUNG
# =====================================================
echo ""
echo "======================================================"
echo "  FeuerwehrListen Update"
echo "======================================================"
echo ""

log_info "Prüfe Systemvoraussetzungen..."

# Prüfe, ob Script als root läuft
if [ "$EUID" -ne 0 ]; then 
    log_error "Dieses Script muss als root ausgeführt werden!"
    exit 1
fi

# Prüfe, ob wichtige Verzeichnisse existieren
if [ ! -d "$REPO_DIR" ]; then
    log_error "Repository-Verzeichnis nicht gefunden: $REPO_DIR"
    exit 1
fi

log_success "Systemvoraussetzungen erfüllt"

# =====================================================
# SCHRITT 2: BACKUP DER DATENBANK
# =====================================================
echo ""
log_info "Erstelle Datenbank-Backup..."

# Erstelle Backup-Verzeichnis falls nicht vorhanden
mkdir -p "$BACKUP_DIR"

if [ -f "$DB_FILE" ]; then
    BACKUP_FILE="$BACKUP_DIR/feuerwehr_$(date +%Y%m%d_%H%M%S).db"
    cp "$DB_FILE" "$BACKUP_FILE"
    log_success "Backup erstellt: $BACKUP_FILE"
    
    # Behalte nur die letzten 10 Backups
    ls -t "$BACKUP_DIR"/feuerwehr_*.db | tail -n +11 | xargs -r rm
    log_info "Alte Backups aufgeräumt (max. 10 werden behalten)"
else
    log_warning "Keine Datenbank gefunden zum Backup"
fi

# =====================================================
# SCHRITT 3: SERVICE STOPPEN
# =====================================================
echo ""
log_info "Stoppe FeuerwehrListen-Service..."

if systemctl is-active --quiet $SERVICE_NAME; then
    systemctl stop $SERVICE_NAME
    log_success "Service gestoppt"
else
    log_warning "Service war bereits gestoppt"
fi

# Warte kurz, damit alle Prozesse beendet sind
sleep 2

# =====================================================
# SCHRITT 4: GIT UPDATE
# =====================================================
echo ""
log_info "Lade neueste Änderungen aus Git..."

cd "$REPO_DIR"

# Zeige aktuelle Version
CURRENT_COMMIT=$(git rev-parse --short HEAD 2>/dev/null || echo "unknown")
log_info "Aktuelle Version: $CURRENT_COMMIT"

# Update aus Git
git fetch origin
git reset --hard origin/master

# Zeige neue Version
NEW_COMMIT=$(git rev-parse --short HEAD)
log_success "Git aktualisiert auf Version: $NEW_COMMIT"

if [ "$CURRENT_COMMIT" = "$NEW_COMMIT" ]; then
    log_info "Keine neuen Änderungen verfügbar"
fi

# =====================================================
# SCHRITT 5: BUILD & PUBLISH
# =====================================================
echo ""
log_info "Baue Anwendung neu..."

# Lösche temporäres Publish-Verzeichnis falls vorhanden
rm -rf "$TEMP_PUBLISH_DIR"

# Sauberer Build
log_info "Führe 'dotnet clean' aus..."
dotnet clean "$REPO_DIR/FeuerwehrListen/FeuerwehrListen.csproj" -c Release --nologo

# Publish in temporäres Verzeichnis
log_info "Führe 'dotnet publish' aus..."
dotnet publish "$REPO_DIR/FeuerwehrListen/FeuerwehrListen.csproj" \
    -c Release \
    -o "$TEMP_PUBLISH_DIR" \
    --nologo \
    /p:UseAppHost=true

log_success "Anwendung erfolgreich gebaut"

# =====================================================
# SCHRITT 6: DEPLOYMENT
# =====================================================
echo ""
log_info "Aktualisiere Anwendungsdateien..."

# WICHTIG: Nur App-Dateien aktualisieren, NICHT das data/-Verzeichnis!
# Lösche alle Dateien AUSSER dem data/-Verzeichnis
find "$PUBLISH_DIR" -mindepth 1 -maxdepth 1 ! -name 'data' -exec rm -rf {} +

# Kopiere neue App-Dateien
cp -r "$TEMP_PUBLISH_DIR"/* "$PUBLISH_DIR/"

log_success "Anwendungsdateien aktualisiert"

# Aufräumen des temporären Verzeichnisses
rm -rf "$TEMP_PUBLISH_DIR"
log_info "Temporäre Dateien aufgeräumt"

# =====================================================
# SCHRITT 7: BERECHTIGUNGEN & DATENBANK
# =====================================================
echo ""
log_info "Setze Berechtigungen..."

# Stelle sicher, dass Datenverzeichnis existiert
mkdir -p "$DB_DIR"

# Erstelle leere Datenbank falls nicht vorhanden
if [ ! -f "$DB_FILE" ]; then
    log_warning "Datenbank existiert nicht - erstelle neue"
    touch "$DB_FILE"
fi

# Setze Besitzer und Berechtigungen
chown -R www-data:www-data "$APP_ROOT"
chmod 755 "$DB_DIR"
chmod 644 "$DB_FILE"
chmod 755 "$PUBLISH_DIR"

log_success "Berechtigungen gesetzt"

# Zeige Datenbank-Info
DB_SIZE=$(du -h "$DB_FILE" | cut -f1)
log_info "Datenbank-Größe: $DB_SIZE"

# =====================================================
# SCHRITT 8: SERVICE STARTEN
# =====================================================
echo ""
log_info "Starte FeuerwehrListen-Service..."

systemctl start $SERVICE_NAME

# Warte kurz und prüfe Status
sleep 3

if systemctl is-active --quiet $SERVICE_NAME; then
    log_success "Service erfolgreich gestartet"
else
    log_error "Service konnte nicht gestartet werden!"
    systemctl status $SERVICE_NAME --no-pager
    exit 1
fi

# =====================================================
# SCHRITT 9: ABSCHLUSS
# =====================================================
echo ""
echo "======================================================"
log_success "Update erfolgreich abgeschlossen!"
echo "======================================================"
echo ""
log_info "Zusammenfassung:"
echo "  • Version:         $CURRENT_COMMIT → $NEW_COMMIT"
echo "  • Datenbank:       $DB_SIZE (gesichert)"
echo "  • Service-Status:  $(systemctl is-active $SERVICE_NAME)"
echo ""

# Zeige Service-Status
systemctl status $SERVICE_NAME --no-pager -l

echo ""
log_info "Letzte Backups:"
ls -lht "$BACKUP_DIR"/feuerwehr_*.db 2>/dev/null | head -n 3 || echo "  (keine)"
echo ""
