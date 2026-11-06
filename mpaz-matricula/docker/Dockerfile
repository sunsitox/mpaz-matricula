# syntax=docker/dockerfile:1
# Entrega final · Portal de Matrícula MPAZ
# Contexto de construcción: carpeta mpaz-matricula

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY MarcelaPaz.Domain/MarcelaPaz.Domain.csproj MarcelaPaz.Domain/
COPY MarcelaPaz.Application/MarcelaPaz.Application.csproj MarcelaPaz.Application/
COPY MarcelaPaz.Infrastructure/MarcelaPaz.Infrastructure.csproj MarcelaPaz.Infrastructure/
COPY MarcelaPaz.Web/MarcelaPaz.Web.csproj MarcelaPaz.Web/
RUN dotnet restore MarcelaPaz.Web/MarcelaPaz.Web.csproj

COPY . .
RUN dotnet publish MarcelaPaz.Web/MarcelaPaz.Web.csproj \
        -c Release --no-restore -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080 \
    DOTNET_NOLOGO=1 \
    DOTNET_CLI_TELEMETRY_OPTOUT=1

# Claves de Data Protection: se guardan en un volumen para no cerrar sesiones
# en cada reinicio del contenedor.
RUN mkdir -p /keys && chown -R "$APP_UID":"0" /keys

COPY --from=build /app/publish .

EXPOSE 8080
USER $APP_UID

ENTRYPOINT ["dotnet", "MarcelaPaz.Web.dll"]
