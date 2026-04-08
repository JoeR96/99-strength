#!/bin/sh
set -e

# Resolve the connection string. Prefer the explicit A2S_CONNECTION_STRING used by
# the design-time factory; fall back to the standard ASP.NET ConnectionStrings binding.
CONN="${A2S_CONNECTION_STRING:-${ConnectionStrings__DefaultConnection:-}}"

if [ -z "$CONN" ]; then
    echo "ERROR: No database connection string set (A2S_CONNECTION_STRING or ConnectionStrings__DefaultConnection)." >&2
    exit 1
fi

echo "[entrypoint] Applying EF Core migrations via bundle..."
/app/efbundle --connection "$CONN"
echo "[entrypoint] Migrations applied. Starting API."

exec dotnet A2S.Api.dll
