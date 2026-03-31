# =============================================================================
# Hali Worker — Dockerfile
# Background worker: NLP extraction, clustering, CIVIS scoring, notifications
# Shares the same base image as the API for layer cache efficiency
# =============================================================================

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS restore
WORKDIR /src

COPY ["src/Hali.Workers/Hali.Workers.csproj",        "src/Hali.Workers/"]
COPY ["src/Hali.Application/Hali.Application.csproj",   "src/Hali.Application/"]
COPY ["src/Hali.Domain/Hali.Domain.csproj",         "src/Hali.Domain/"]
COPY ["src/Hali.Infrastructure/Hali.Infrastructure.csproj", "src/Hali.Infrastructure/"]
COPY ["src/Hali.Contracts/Hali.Contracts.csproj",    "src/Hali.Contracts/"]

RUN dotnet restore "src/Hali.Workers/Hali.Workers.csproj"

FROM restore AS build
COPY . .
RUN dotnet build "src/Hali.Workers/Hali.Workers.csproj" \
    --no-restore \
    --configuration Release \
    -o /app/build

FROM build AS publish
RUN dotnet publish "src/Hali.Workers/Hali.Workers.csproj" \
    --no-restore \
    --configuration Release \
    --output /app/publish \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime

RUN addgroup --system --gid 1001 hali \
 && adduser  --system --uid 1001 --ingroup hali hali

WORKDIR /app
COPY --from=publish --chown=hali:hali /app/publish .
USER hali

# Workers don't expose HTTP but should respond to a process health signal.
# docker-compose and Kubernetes can check process liveness instead.

ENV DOTNET_RUNNING_IN_CONTAINER=true
ENV ASPNETCORE_ENVIRONMENT=Production

ENTRYPOINT ["dotnet", "Hali.Workers.dll"]
