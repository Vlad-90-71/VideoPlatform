# ============================================================
# Build stage
# ============================================================
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

COPY ["src/Shared/Shared.csproj", "src/Shared/"]
COPY ["src/LessonService/LessonService.csproj", "src/LessonService/"]
RUN dotnet restore "src/LessonService/LessonService.csproj"

COPY src/Shared/ src/Shared/
COPY src/LessonService/ src/LessonService/

WORKDIR "/src/src/LessonService"
RUN dotnet build "LessonService.csproj" -c $BUILD_CONFIGURATION -o /app/build && \
    dotnet publish "LessonService.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# ============================================================
# Runtime stage
# ============================================================
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime

RUN addgroup --system --gid 1000 appgroup && \
    adduser --system --uid 1000 --ingroup appgroup appuser

RUN apt-get update && \
    apt-get install -y --no-install-recommends curl ca-certificates && \
    apt-get clean && \
    rm -rf /var/lib/apt/lists/*

WORKDIR /app

COPY --from=build --chown=appuser:appgroup /app/publish .

RUN mkdir -p /https && \
    chown -R appuser:appgroup /https

ENV ASPNETCORE_URLS=http://+:8080;https://+:8081
ENV ASPNETCORE_ENVIRONMENT=Development

EXPOSE 8080
EXPOSE 8081

HEALTHCHECK --interval=30s --timeout=5s --start-period=30s --retries=3 \
    CMD curl -k -f https://localhost:8081/health || exit 1

USER appuser

ENTRYPOINT ["dotnet", "LessonService.dll"]