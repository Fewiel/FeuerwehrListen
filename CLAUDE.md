# Hinweise für Claude

* **Versionsnummer:** Vor jedem Push/Merge nach `master` die Version in `Directory.Build.props`
  nach den Regeln in `VERSIONING.md` erhöhen (feat → MINOR, fix → PATCH, großer Umbau → MAJOR)
  und den Verlauf in `VERSIONING.md` ergänzen.
* Entwickelt wird auf `feature/sso-provider`, nach Freigabe in `master` gemerged
  (Auto-Update auf dem Server zieht `master` alle 5 Minuten).
