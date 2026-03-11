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
export CONFIG_DEFAULT_FILE CONFIG_FILE SQL_HOST SQL_PORT SA_PASSWORD
python3 - <<'PY'
import json
import os

config_default_path = os.environ.get("CONFIG_DEFAULT_FILE")
config_path = os.environ.get("CONFIG_FILE")
sql_host = os.environ.get("SQL_HOST")
sql_port = os.environ.get("SQL_PORT")
sa_password = os.environ.get("SA_PASSWORD")

if not config_default_path or not config_path:
    raise SystemExit("CONFIG_DEFAULT_FILE and CONFIG_FILE environment variables must be set.")

with open(config_default_path, "r", encoding="utf-8") as f:
    config = json.load(f)

tcp_connection_string = (
    f"Data Source=tcp:{sql_host},{sql_port};"
    f"Database=Northwind;"
    f"User Id=sa;"
    f"Password={sa_password};"
    "Encrypt=false;"
    "TrustServerCertificate=true"
)

config["TCPConnectionString"] = tcp_connection_string
config["SupportsIntegratedSecurity"] = False

with open(config_path, "w", encoding="utf-8") as f:
    json.dump(config, f, indent=4)
    f.write("\n")
PY
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
