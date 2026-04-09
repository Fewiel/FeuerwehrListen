# Stage 1: Build the application
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy solution and project files first to leverage Docker layer caching
COPY ["FeuerwehrListen.sln", "."]
COPY ["FeuerwehrListen/FeuerwehrListen.csproj", "FeuerwehrListen/"]

# Restore dependencies for the entire solution
RUN dotnet restore "FeuerwehrListen.sln"

# Copy the rest of the application source code
COPY . .
WORKDIR "/src/FeuerwehrListen"

# Publish the application
RUN dotnet publish "FeuerwehrListen.csproj" -c Release -o /app/publish --no-restore

# Stage 2: Create the final runtime image
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
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
