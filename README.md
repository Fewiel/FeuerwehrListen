# Feuerwehr Listen

Eine Webanwendung zur Verwaltung von Anwesenheits- und Einsatzlisten für Feuerwehren.

## Installation & Ausführung auf einem Ubuntu Server mit Docker

Diese Anleitung beschreibt die Einrichtung und den Betrieb der Anwendung auf einem Ubuntu-Server.

### 1. Docker & Docker Compose installieren

Verbinden Sie sich mit Ihrem Server und führen Sie die folgenden Befehle aus, um Docker zu installieren.

```sh
# System aktualisieren
sudo apt-get update
sudo apt-get install ca-certificates curl

# Docker GPG-Schlüssel hinzufügen
sudo install -m 0755 -d /etc/apt/keyrings
sudo curl -fsSL https://download.docker.com/linux/ubuntu/gpg -o /etc/apt/keyrings/docker.asc
sudo chmod a+r /etc/apt/keyrings/docker.asc

# Docker Repository hinzufügen
echo \
  "deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/docker.asc] https://download.docker.com/linux/ubuntu \
  $(. /etc/os-release && echo "$VERSION_CODENAME") stable" | \
  sudo tee /etc/apt/sources.list.d/docker.list > /dev/null
sudo apt-get update

# Docker Engine & Compose installieren
sudo apt-get install docker-ce docker-ce-cli containerd.io docker-buildx-plugin docker-compose-plugin -y
```

### 2. Anwendung einrichten

1.  **Repository klonen:**
    ```sh
    cd /home/docker
    git clone https://github.com/Fewiel/FeuerwehrListen.git
    cd FeuerwehrListen
    ```

2.  **Lokale Konfiguration anlegen (optional):**

    Port und Datenbankname können per `docker-compose.override.yml` angepasst werden. Diese Datei ist gitignored und wird beim Update nicht überschrieben.

    ```sh
    cat > docker-compose.override.yml <<EOF
    services:
      feuerwehr-listen:
        ports:
          - "8090:8080"
        environment:
          - DATABASE_CONNECTION_STRING=Data Source=/app/data/feuerwehr-2026.db
    EOF
    ```

3.  **Anwendung starten:**
    ```sh
    docker compose up --build -d
    ```

### 3. Zugriff & Verwaltung

- **Zugriff:** `http://IHRE_SERVER_IP:8080` (oder `8090` mit Override)
- **Logs ansehen:** `docker compose logs -f`
- **Stoppen:** `docker compose down`

### 4. Auto-Update einrichten

Alle 5 Minuten prüft ein Cron-Job ob neue Commits auf dem Master-Branch vorliegen und baut den Container automatisch neu.

**Cronjob einrichten (als User `docker`):**

```sh
crontab -e -u docker
```

Folgende Zeile einfügen:

```
*/5 * * * * /bin/bash /home/docker/FeuerwehrListen/auto-update.sh
```

**Oder** das Setup-Script nutzen (installiert Docker + Cron automatisch):

```sh
sudo bash setup.sh
```

#### Manuelles Update

```sh
bash auto-update.sh
```

#### Update-Log einsehen

```sh
tail -f auto-update.log
```

#### Auto-Update deaktivieren

```sh
crontab -e -u docker
# Zeile mit auto-update.sh entfernen
```

### Datenpersistenz

Die SQLite-Datenbank wird in einem Docker-Volume gespeichert. Ihre Daten bleiben auch nach einem Neustart des Containers oder des Servers erhalten. Die `docker-compose.override.yml` ist gitignored und wird beim Auto-Update nicht überschrieben.
