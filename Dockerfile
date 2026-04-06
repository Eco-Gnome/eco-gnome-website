FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy solution and project files
COPY ecocraft.sln .
COPY ecocraft/ecocraft.csproj ecocraft/

# Copy local DLLs needed for restore/build
COPY ecocraft/Libs/ ecocraft/Libs/

# Restore dependencies
RUN dotnet restore ecocraft/ecocraft.csproj

# Copy everything else
COPY ecocraft/ ecocraft/

# Build and publish
RUN dotnet publish ecocraft/ecocraft.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish .

# Create directories for runtime-generated assets
RUN mkdir -p wwwroot/assets wwwroot/videos

EXPOSE 8080
ENTRYPOINT ["dotnet", "ecocraft.dll"]
