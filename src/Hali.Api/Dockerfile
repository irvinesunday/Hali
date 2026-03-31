# =============================================================================
# Hali API — Dockerfile
# Multi-stage build: restore → build → publish → runtime
# Final image runs as non-root user on port 8080
# =============================================================================

# ── Stage 1: Restore dependencies ────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS restore
WORKDIR /src

# Copy solution and project files first (layer caching — only re-runs on .csproj changes)
COPY ["src/Hali.Api/Hali.Api.csproj",             "src/Hali.Api/"]
COPY ["src/Hali.Application/Hali.Application.csproj",   "src/Hali.Application/"]
COPY ["src/Hali.Domain/Hali.Domain.csproj",         "src/Hali.Domain/"]
COPY ["src/Hali.Infrastructure/Hali.Infrastructure.csproj", "src/Hali.Infrastructure/"]
COPY ["src/Hali.Contracts/Hali.Contracts.csproj",    "src/Hali.Contracts/"]
COPY ["src/Hali.Workers/Hali.Workers.csproj",        "src/Hali.Workers/"]

RUN dotnet restore "src/Hali.Api/Hali.Api.csproj"

# ── Stage 2: Build ────────────────────────────────────────────────────────────
FROM restore AS build
COPY . .
RUN dotnet build "src/Hali.Api/Hali.Api.csproj" \
    --no-restore \
    --configuration Release \
    -o /app/build

# ── Stage 3: Publish ──────────────────────────────────────────────────────────
FROM build AS publish
RUN dotnet publish "src/Hali.Api/Hali.Api.csproj" \
    --no-restore \
    --configuration Release \
    --output /app/publish \
    /p:UseAppHost=false

# ── Stage 4: Runtime (minimal image, non-root) ────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime

# Security: run as non-root
RUN addgroup --system --gid 1001 hali \
 && adduser  --system --uid 1001 --ingroup hali hali

WORKDIR /app

COPY --from=publish --chown=hali:hali /app/publish .

USER hali

# Health check — CI and deploy workflow both call /health
HEALTHCHECK --interval=30s --timeout=5s --start-period=15s --retries=3 \
    CMD wget -qO- http://localhost:8080/health || exit 1

EXPOSE 8080

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
ENV DOTNET_RUNNING_IN_CONTAINER=true

ENTRYPOINT ["dotnet", "Hali.Api.dll"]
