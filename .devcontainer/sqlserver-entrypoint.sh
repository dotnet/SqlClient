#!/usr/bin/env bash
# -------------------------------------------------------------------
# Generates a random SA password and writes it to /sql-config/sa-password
# so both the devcontainer and sqlserver service can use it.
#
# Called as the sqlserver entrypoint (before SQL Server starts).
# -------------------------------------------------------------------
set -euo pipefail

PASSWORD_FILE="/sql-config/sa-password"

# Ensure the directory is writable (needed when running as root with a fresh volume).
chmod 777 /sql-config 2>/dev/null || true

# Generate a password only if one doesn't already exist (container restart).
if [ ! -f "$PASSWORD_FILE" ]; then
    # 32-byte random base64 + required character classes for SQL Server policy.
    RAW="$(head -c 32 /dev/urandom | base64 | tr -d '/+=' | head -c 24)"
    # SQL Server requires uppercase, lowercase, digit, and symbol.
    GENERATED="A1a@${RAW}"
    echo -n "$GENERATED" > "$PASSWORD_FILE"
fi

PASSWORD="$(cat "$PASSWORD_FILE")"
export MSSQL_SA_PASSWORD="$PASSWORD"

# Hand off to the real SQL Server entrypoint.
exec /opt/mssql/bin/sqlservr
