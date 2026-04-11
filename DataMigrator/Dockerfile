FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY ecocraft.sln .
COPY ecocraft/ecocraft.csproj ecocraft/
COPY DataMigrator/DataMigrator.csproj DataMigrator/
COPY ecocraft/Libs/ ecocraft/Libs/

RUN dotnet restore DataMigrator/DataMigrator.csproj

COPY ecocraft/ ecocraft/
COPY DataMigrator/DataMigrator.cs DataMigrator/

RUN dotnet publish DataMigrator/DataMigrator.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "DataMigrator.dll"]
