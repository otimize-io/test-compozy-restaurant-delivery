# syntax=docker/dockerfile:1
#
# Single, DRY, parameterized multi-stage build for every .NET service AND the gateway.
# Each compose service supplies two build args:
#   PROJECT  - path to the .csproj relative to the repo root (the build context),
#              e.g. src/Services/Order/Order.csproj
#   APP_DLL  - the published entry assembly name, e.g. Order.dll (Gateway.dll for the gateway)
#
# The build context is the REPO ROOT so that each service's ProjectReference to the
# Shared/* projects (Bootstrap, Contracts, Platform) resolves during restore/publish.

# ---- build stage -------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG PROJECT
WORKDIR /src

# Copy the whole source tree (bin/obj/etc. are excluded by .dockerignore) so project
# references and the central global.json (SDK pin) are all present for restore + publish.
COPY . .

# Restore + publish only the requested project. Publishing pulls in the referenced
# Shared projects automatically. No host runtime needed in the final image (framework-dependent).
RUN dotnet restore "$PROJECT"
RUN dotnet publish "$PROJECT" -c Release -o /app/publish --no-restore /p:UseAppHost=false

# ---- runtime stage -----------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
ARG APP_DLL
WORKDIR /app

# curl is used by the compose healthcheck (GET /health). The aspnet image does not ship it.
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

# Kestrel listens on 8080 inside the container (ASP.NET Core default for the aspnet image).
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

# APP_DLL is an ARG, so bake it into an ENV the shell-less entrypoint can read at runtime.
ENV APP_DLL=${APP_DLL}
# exec form via shell so $APP_DLL expands; "exec" keeps dotnet as PID 1 for clean signals.
ENTRYPOINT ["/bin/sh", "-c", "exec dotnet \"$APP_DLL\""]
