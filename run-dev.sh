#!/bin/bash
export ASPNETCORE_ENVIRONMENT=Development
export ASPNETCORE_URLS=http://localhost:5000
exec "/c/Program Files/dotnet/dotnet" run --project FeuerwehrListen/FeuerwehrListen.csproj
