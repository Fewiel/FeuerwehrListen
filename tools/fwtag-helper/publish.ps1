# Baut eine eigenstaendige fwtag-helper.exe (single file, keine .NET-Installation noetig).
# Ergebnis liegt in .\publish\  ->  fwtag-helper.exe  +  scad\ (Vorlagen).
# Diesen Ordner an die 2-3 Admins verteilen.

$ErrorActionPreference = "Stop"
Set-Location -Path $PSScriptRoot

dotnet publish -c Release -r win-x64 --self-contained `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
    -o publish

Write-Host ""
Write-Host "Fertig. Verteilen:  $PSScriptRoot\publish\  (fwtag-helper.exe + scad\)" -ForegroundColor Green
