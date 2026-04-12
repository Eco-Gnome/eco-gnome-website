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

# Stash container-owned assets so the entrypoint can copy them
# into the volume-mounted directory at startup
RUN mkdir -p /app/assets-seed \
    && mv wwwroot/assets/eco-icons /app/assets-seed/eco-icons \
    && mv wwwroot/assets/lang /app/assets-seed/lang

# Create directories for runtime-generated assets
RUN mkdir -p wwwroot/assets wwwroot/videos

COPY entrypoint.sh /app/entrypoint.sh
RUN sed -i 's/\r$//' /app/entrypoint.sh \
    && chmod +x /app/entrypoint.sh

EXPOSE 8080
ENTRYPOINT ["/app/entrypoint.sh"]
