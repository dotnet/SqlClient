#!/usr/bin/env bash
# -------------------------------------------------------------------
# Initializes the SQL Server instance running in the devcontainer
# companion service with the test databases required by the test suite.
# -------------------------------------------------------------------
set -euo pipefail

SQL_HOST="${SQL_SERVER_HOST:-sqlserver}"
SQL_PORT="${SQL_SERVER_PORT:-1433}"
SA_PASSWORD_FILE="${SQL_SERVER_SA_PASSWORD_FILE:-/sql-config/sa-password}"

# Wait for the auto-generated SA password file from the sqlserver service.
# The sqlserver entrypoint writes this file on startup; the shared volume
# may not be visible immediately when postCreateCommand begins.
echo "Waiting for SA password file at ${SA_PASSWORD_FILE}..."
for i in $(seq 1 30); do
    if [ -f "$SA_PASSWORD_FILE" ]; then
        break
    fi
    if [ "$i" -eq 30 ]; then
        echo "ERROR: SA password file not found at $SA_PASSWORD_FILE after 60 s" >&2
        exit 1
    fi
    sleep 2
done
SA_PASSWORD="$(cat "$SA_PASSWORD_FILE")"

REPO_ROOT="$(cd "$(dirname "$0")/.." && pwd)"

# Write test config file so the test suite can find the connection string.
CONFIG_DIR="${REPO_ROOT}/src/Microsoft.Data.SqlClient/tests/tools/Microsoft.Data.SqlClient.TestUtilities"
CONFIG_DEFAULT_FILE="${CONFIG_DIR}/config.default.json"
CONFIG_FILE="${CONFIG_DIR}/config.json"
echo "Writing test config to ${CONFIG_FILE} (based on ${CONFIG_DEFAULT_FILE})..."
TCP_CONN_STR="Data Source=tcp:${SQL_HOST},${SQL_PORT};Database=Northwind;User Id=sa;Password=${SA_PASSWORD};Encrypt=false;TrustServerCertificate=true"
# config.default.json contains JS-style comments (// ...) which are not valid JSON.
# Strip single-line comments before feeding to jq.
sed 's|//.*||' "${CONFIG_DEFAULT_FILE}" \
  | jq --arg cs "${TCP_CONN_STR}" \
       '.TCPConnectionString = $cs | .NPConnectionString = "" | .SupportsIntegratedSecurity = false' \
       > "${CONFIG_FILE}"
echo "Test config written."

echo "Waiting for SQL Server at ${SQL_HOST}:${SQL_PORT} to become available..."
for i in $(seq 1 30); do
    if sqlcmd -S "${SQL_HOST},${SQL_PORT}" -U sa -P "${SA_PASSWORD}" -C -Q "SELECT 1" &>/dev/null; then
        break
    fi
    if [ "$i" -eq 30 ]; then
        echo "ERROR: SQL Server did not become available in time." >&2
        exit 1
    fi
    sleep 2
done
echo "SQL Server is ready."

# Ensure Northwind database exists
echo "Ensuring Northwind database exists..."
DB_EXISTS_RAW="$(sqlcmd -S "${SQL_HOST},${SQL_PORT}" -U sa -P "${SA_PASSWORD}" -C -h -1 -W \
    -Q "SET NOCOUNT ON; SELECT COUNT(*) FROM sys.databases WHERE name = N'Northwind';" | tr -d '\r')"
DB_EXISTS="$(echo "$DB_EXISTS_RAW" | tr -d ' ')"
if [ "$DB_EXISTS" = "0" ]; then
    echo "Northwind database not found, creating..."
    sqlcmd -S "${SQL_HOST},${SQL_PORT}" -U sa -P "${SA_PASSWORD}" -C \
        -i "${REPO_ROOT}/tools/testsql/createNorthwindDb.sql"
    echo "Northwind database created."
else
    echo "Northwind database already exists; skipping creation script."
fi

echo "SQL Server test database setup complete."
echo ""
echo "Connection info:"
echo "  Host: ${SQL_HOST}"
echo "  Port: ${SQL_PORT}"
echo "  User: sa"
echo "  Password: ${SA_PASSWORD}"
echo ""
echo "Example sqlcmd:"
echo "  sqlcmd -S ${SQL_HOST},${SQL_PORT} -U sa -P '${SA_PASSWORD}' -C"
