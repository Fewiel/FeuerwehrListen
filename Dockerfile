# Stage 1: Build the application
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project files first to leverage Docker layer caching.
# Der Server referenziert das WASM-Client-Projekt -> beide csproj vor dem Restore kopieren.
COPY ["FeuerwehrListen/FeuerwehrListen.csproj", "FeuerwehrListen/"]
COPY ["FeuerwehrListen.Client/FeuerwehrListen.Client.csproj", "FeuerwehrListen.Client/"]

# Restore dependencies for the app project only (excludes test projects).
# Zieht das referenzierte Client-Projekt transitiv mit.
RUN dotnet restore "FeuerwehrListen/FeuerwehrListen.csproj"

# Copy the rest of the application source code
COPY . .

# Lokalen Render-Helfer (Windows, self-contained single-file) cross-publishen und als ZIP
# nach wwwroot/downloads legen -> Admins koennen ihn direkt aus der App herunterladen.
# (Cross-Compile aus dem Linux-SDK; laeuft nur beim Image-Build, nicht zur Laufzeit.)
RUN apt-get update && apt-get install -y --no-install-recommends zip && rm -rf /var/lib/apt/lists/*
RUN dotnet publish "tools/fwtag-helper/fwtag-helper.csproj" -c Release -r win-x64 --self-contained true \
        -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o /helper \
    && mkdir -p "FeuerwehrListen/wwwroot/downloads" \
    && (cd /helper && zip -r -q "/src/FeuerwehrListen/wwwroot/downloads/fwtag-helper.zip" fwtag-helper.exe scad)

WORKDIR "/src/FeuerwehrListen"

# Publish the application (die Helfer-ZIP unter wwwroot/downloads kommt als statische Datei mit)
RUN dotnet publish "FeuerwehrListen.csproj" -c Release -o /app/publish --no-restore

# Stage 2: Create the final runtime image
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
# libgdiplus/libc6-dev: PdfSharp (GDI). Tag-STL-Rendering (OpenSCAD) laeuft NICHT
# mehr auf dem Server (killte die kleine VM), sondern client-seitig via lokalem Helfer.
RUN apt-get update && apt-get install -y --no-install-recommends libgdiplus libc6-dev && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app/publish .

# Expose HTTPS port
EXPOSE 8080

# Configure Kestrel for HTTPS on port 8080
ENV ASPNETCORE_URLS=https://+:8080
ENV ASPNETCORE_HTTPS_PORTS=8080

# Define the entry point for the container
ENTRYPOINT ["dotnet", "FeuerwehrListen.dll"]
