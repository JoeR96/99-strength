#!/bin/sh
set -e

# Resolve the connection string. The app binds Database:ConnectionString
# (env var Database__ConnectionString); also accept A2S_CONNECTION_STRING
# (design-time factory) and ConnectionStrings__DefaultConnection as fallbacks.
CONN="${Database__ConnectionString:-${A2S_CONNECTION_STRING:-${ConnectionStrings__DefaultConnection:-}}}"

if [ -z "$CONN" ]; then
    echo "ERROR: No database connection string set (Database__ConnectionString, A2S_CONNECTION_STRING, or ConnectionStrings__DefaultConnection)." >&2
    exit 1
fi

echo "[entrypoint] Applying EF Core migrations via bundle..."
# A2SDbContextFactory (design-time) reads A2S_CONNECTION_STRING directly and
# takes precedence over --connection, so we must export it for the bundle.
A2S_CONNECTION_STRING="$CONN" /app/efbundle --connection "$CONN"
echo "[entrypoint] Migrations applied. Starting API."

exec dotnet A2S.Api.dll
