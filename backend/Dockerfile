# syntax=docker/dockerfile:1

# ---- build stage: full SDK, compiles and publishes the API ----
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy only the project file first so `dotnet restore` is cached until dependencies change.
COPY src/TaskManager.Api/TaskManager.Api.csproj src/TaskManager.Api/
RUN dotnet restore src/TaskManager.Api/TaskManager.Api.csproj

COPY src/TaskManager.Api/ src/TaskManager.Api/
RUN dotnet publish src/TaskManager.Api/TaskManager.Api.csproj \
      -c Release -o /app/publish --no-restore /p:UseAppHost=false

# ---- runtime stage: small ASP.NET image, no SDK, runs as non-root ----
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080 \
    DOTNET_EnableDiagnostics=0
EXPOSE 8080

# `app` user (uid 1654) ships with the .NET 8 images.
USER $APP_UID
ENTRYPOINT ["dotnet", "TaskManager.Api.dll"]
