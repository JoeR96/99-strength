# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy csproj files first for layer caching
COPY src/A2S.Api/A2S.Api.csproj src/A2S.Api/
COPY src/A2S.Application/A2S.Application.csproj src/A2S.Application/
COPY src/A2S.Domain/A2S.Domain.csproj src/A2S.Domain/
COPY src/A2S.Infrastructure/A2S.Infrastructure.csproj src/A2S.Infrastructure/
COPY src/A2S.Integration.Hevy/A2S.Integration.Hevy.csproj src/A2S.Integration.Hevy/
RUN dotnet restore src/A2S.Api/A2S.Api.csproj

# Copy source and publish
COPY src/ src/
RUN dotnet publish src/A2S.Api/A2S.Api.csproj -c Release -o /app/publish --no-restore

# Stage 1b: EF Core migration bundle
# Produces a self-contained executable that applies migrations as a deploy step.
# A placeholder connection string is required only because the design-time factory
# needs to instantiate the DbContext; the bundle reads the real --connection at runtime.
FROM build AS migrations
WORKDIR /src
ENV A2S_CONNECTION_STRING="Host=placeholder;Database=placeholder;Username=placeholder;Password=placeholder"
RUN dotnet tool install --global dotnet-ef --version 9.0.0
ENV PATH="${PATH}:/root/.dotnet/tools"
RUN dotnet ef migrations bundle \
    --project src/A2S.Infrastructure/A2S.Infrastructure.csproj \
    --startup-project src/A2S.Api/A2S.Api.csproj \
    --self-contained -r linux-x64 \
    -o /app/efbundle

# Stage 2: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .
COPY --from=migrations /app/efbundle /app/efbundle
COPY docker-entrypoint.sh /app/docker-entrypoint.sh
RUN chmod +x /app/docker-entrypoint.sh /app/efbundle

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

HEALTHCHECK --interval=30s --timeout=5s --start-period=20s --retries=3 \
    CMD curl -f http://localhost:8080/health || exit 1

ENTRYPOINT ["/app/docker-entrypoint.sh"]
