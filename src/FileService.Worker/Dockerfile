# ============================================================
# Build stage
# ============================================================
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

COPY ["src/Shared/Shared.csproj", "src/Shared/"]
COPY ["src/FileService.Worker/FileService.Worker.csproj", "src/FileService.Worker/"]
RUN dotnet restore "src/FileService.Worker/FileService.Worker.csproj"

COPY src/Shared/ src/Shared/
COPY src/FileService.Worker/ src/FileService.Worker/

WORKDIR "/src/src/FileService.Worker"
RUN dotnet build "FileService.Worker.csproj" -c $BUILD_CONFIGURATION -o /app/build && \
    dotnet publish "FileService.Worker.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# ============================================================
# Runtime stage с FFmpeg
# ============================================================
FROM mcr.microsoft.com/dotnet/runtime:8.0 AS runtime

RUN apt-get update && \
    apt-get install -y --no-install-recommends \
        ffmpeg \
        curl \
        procps \
        ca-certificates && \
    apt-get clean && \
    rm -rf /var/lib/apt/lists/*

RUN ffmpeg -version

RUN addgroup --system --gid 1000 appgroup && \
    adduser --system --uid 1000 --ingroup appgroup appuser

WORKDIR /app

COPY --from=build --chown=appuser:appgroup /app/publish .

RUN mkdir -p /tmp/video-processing/input \
             /tmp/video-processing/output \
             /tmp/video-processing/chunks && \
    chown -R appuser:appgroup /tmp/video-processing

ENV DOTNET_ENVIRONMENT=Development
ENV FFmpeg__TempFolder=/tmp/video-processing

HEALTHCHECK --interval=30s --timeout=5s --start-period=30s --retries=3 \
    CMD pgrep -f "FileService.Worker.dll" || exit 1

USER appuser

ENTRYPOINT ["dotnet", "FileService.Worker.dll"]