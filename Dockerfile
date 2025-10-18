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
WORKDIR /app
COPY --from=build /app/publish .

# Expose the port the app runs on
EXPOSE 8080

# Define the entry point for the container
ENTRYPOINT ["dotnet", "FeuerwehrListen.dll"]
