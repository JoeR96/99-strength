# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy csproj files first for layer caching
COPY src/A2S.Api/A2S.Api.csproj src/A2S.Api/
COPY src/A2S.Application/A2S.Application.csproj src/A2S.Application/
COPY src/A2S.Domain/A2S.Domain.csproj src/A2S.Domain/
COPY src/A2S.Infrastructure/A2S.Infrastructure.csproj src/A2S.Infrastructure/
RUN dotnet restore src/A2S.Api/A2S.Api.csproj

# Copy source and publish
COPY src/ src/
RUN dotnet publish src/A2S.Api/A2S.Api.csproj -c Release -o /app/publish --no-restore

# Stage 2: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

HEALTHCHECK --interval=30s --timeout=5s --start-period=10s --retries=3 \
    CMD curl -f http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "A2S.Api.dll"]
