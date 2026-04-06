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
    git clone <repository-url>
    cd FeuerwehrListen
    ```

2.  **Anwendung starten:**
    Führen Sie den folgenden Befehl im Hauptverzeichnis des Projekts aus. Der `sudo` wird benötigt, da Docker Root-Berechtigungen erfordert.
    ```sh
    sudo docker compose up --build -d
    ```
    - `--build`: Erstellt das Image beim ersten Start und bei Code-Änderungen.
    - `-d`: Startet den Container im "detached" Modus (im Hintergrund).

### 3. Zugriff & Verwaltung

- **Zugriff:** Die Anwendung ist jetzt unter `http://IHRE_SERVER_IP:8080` erreichbar.
- **Logs ansehen:** `sudo docker compose logs -f`
- **Stoppen:** `sudo docker compose down`

### 4. Auto-Update einrichten

Die Anwendung kann sich automatisch aktualisieren. Alle 5 Minuten prüft ein Cron-Job ob neue Commits auf dem Master-Branch vorliegen und baut den Container automatisch neu.

Das Setup-Script erledigt alles automatisch (Docker, Anwendung, Cron):

```sh
sudo bash setup.sh
```

#### Manuelles Update

Falls nötig, kann ein Update auch manuell ausgelöst werden:

```sh
sudo bash auto-update.sh
```

Oder klassisch ohne Auto-Update:

```sh
git pull
sudo docker compose up --build -d
```

#### Update-Log einsehen

```sh
tail -f auto-update.log
```

#### Auto-Update deaktivieren

```sh
sudo crontab -e
# Zeile mit "feuerwehrlisten-auto-update" entfernen
```

### Datenpersistenz

Die SQLite-Datenbank wird in einem Docker-Volume gespeichert. Ihre Daten bleiben auch nach einem Neustart des Containers (`sudo docker compose up`) oder des Servers erhalten.
