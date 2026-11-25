# syntax=docker/dockerfile:1
# Build context: the repository root (see the app profile in docker-compose.yml).

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# global.json is left out: it pins a 4xx SDK band, the official image ships another band.
# .editorconfig carries the analyzer settings, such as generated_code for the migrations.
COPY .editorconfig Directory.Build.props dotnet-tools.json ./
COPY src/CityPark.Domain/CityPark.Domain.csproj src/CityPark.Domain/
COPY src/CityPark.Application/CityPark.Application.csproj src/CityPark.Application/
COPY src/CityPark.Infrastructure/CityPark.Infrastructure.csproj src/CityPark.Infrastructure/
COPY src/CityPark.Api/CityPark.Api.csproj src/CityPark.Api/
# Only the project files so far, so this layer stays cached until a dependency changes.
RUN dotnet restore src/CityPark.Api/CityPark.Api.csproj && dotnet tool restore

COPY src/ src/
RUN dotnet publish src/CityPark.Api/CityPark.Api.csproj -c Release --no-restore -o /out/api -p:UseAppHost=false
# One executable that applies the pending migrations; it reads the connection string from CITYPARK_DB.
RUN dotnet ef migrations bundle -c Release \
    --project src/CityPark.Infrastructure --startup-project src/CityPark.Infrastructure \
    -o /out/efbundle

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS migrator
WORKDIR /app
COPY --from=build /out/efbundle ./efbundle
USER $APP_UID
ENTRYPOINT ["./efbundle"]

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS api
WORKDIR /app
COPY --from=build /out/api ./
# Non-root user built into the .NET images; Kestrel listens on 8080 (ASPNETCORE_HTTP_PORTS).
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "CityPark.Api.dll"]
