#!/usr/bin/env bash
####################################################################################################
# Licensed to the .NET Foundation under one or more agreements.  The .NET Foundation licenses this
# file to you under the MIT license.  See the LICENSE file in the project root for more information.
####################################################################################################
#
# run-perf-tests.sh
#
# Entry point executed ON the Perf Test Lab Linux VM by the InternalDriverTools/PerfTest extends
# template (v1/Perf.Test.Job.yml).  The template SCPs the driver source tree to the VM, runs this
# script over SSH, then SCPs the results sub-directory back and publishes it as a pipeline artifact.
#
# Responsibilities:
#   1. Install the .NET SDK pinned by the repo's global.json (plus the runtime for the target TFM).
#   2. Create the perf database on the VM's SQL Server (the benchmark runners create tables but not
#      the database).
#   3. Inject the VM's SQL Server connection string into the benchmark runner config.
#   4. Pin the benchmark client to the reserved CPU set (PERF_CLIENT_CPUS) so it does not contend
#      with SQL Server (which is pinned to the disjoint PERF_SQL_CPUS set).
#   5. Run the BenchmarkDotNet performance tests.
#   6. Collect the BenchmarkDotNet artifacts into the results sub-directory.
#
# Environment variables injected by the template (see wiki "Performance Test Automation"):
#   SQL_SERVER         Host/IP of the SQL Server on the perf VM (e.g. localhost).
#   SQL_PASSWORD       SQL Server 'sa' password.
#   PERF_CLIENT_CPUS   Core range reserved for the test client, e.g. "16-31".
#   PERF_SQL_CPUS      Core range SQL Server is pinned to, e.g. "0-15" (informational).
#
set -euo pipefail

####################################################################################################
# Argument parsing
####################################################################################################

configuration="Release"
framework="net9.0"
resultsSubDir="perf-results"

usage() {
    echo "Usage: $0 [--configuration <cfg>] [--framework <tfm>] [--results-subdir <dir>]" >&2
}

while [[ $# -gt 0 ]]; do
    case "$1" in
        --configuration) configuration="$2"; shift 2 ;;
        --framework)     framework="$2";     shift 2 ;;
        --results-subdir) resultsSubDir="$2"; shift 2 ;;
        -h|--help)       usage; exit 0 ;;
        *) echo "Unknown argument: $1" >&2; usage; exit 2 ;;
    esac
done

####################################################################################################
# Resolve paths
####################################################################################################

# This script lives at <repo>/eng/pipelines/perf/scripts/run-perf-tests.sh, so the repo root is four
# levels up.  Deriving it from the script location keeps us independent of the working directory the
# template runs us from.
SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" >/dev/null 2>&1 && pwd)"
REPO_ROOT="$(cd -- "${SCRIPT_DIR}/../../../.." >/dev/null 2>&1 && pwd)"

PERF_PROJECT="${REPO_ROOT}/src/Microsoft.Data.SqlClient/tests/PerformanceTests/Microsoft.Data.SqlClient.PerformanceTests.csproj"
PERF_DIR="$(dirname -- "${PERF_PROJECT}")"
RESULTS_DIR="${REPO_ROOT}/${resultsSubDir}"

echo "=================================================================="
echo " SqlClient Performance Tests"
echo "=================================================================="
echo "  Repo root      : ${REPO_ROOT}"
echo "  Perf project   : ${PERF_PROJECT}"
echo "  Configuration  : ${configuration}"
echo "  Framework      : ${framework}"
echo "  Results dir    : ${RESULTS_DIR}"
echo "  SQL_SERVER     : ${SQL_SERVER:-<unset, will default to localhost>}"
echo "  PERF_CLIENT_CPUS: ${PERF_CLIENT_CPUS:-<unset>}"
echo "  PERF_SQL_CPUS  : ${PERF_SQL_CPUS:-<unset>}"
echo "=================================================================="

if [[ ! -f "${PERF_PROJECT}" ]]; then
    echo "ERROR: Performance test project not found at ${PERF_PROJECT}" >&2
    exit 1
fi

: "${SQL_SERVER:=localhost}"
if [[ -z "${SQL_PASSWORD:-}" ]]; then
    echo "ERROR: SQL_PASSWORD environment variable is not set (expected from the perf template)." >&2
    exit 1
fi

mkdir -p "${RESULTS_DIR}"

export DOTNET_NOLOGO=1
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1

####################################################################################################
# 1. Install the .NET SDK (pinned by global.json) and the runtime for the target framework.
####################################################################################################

install_dotnet() {
    local sdkVersion
    # Extract the SDK version from global.json (strip // comments, then read "version").
    sdkVersion="$(sed 's://.*::' "${REPO_ROOT}/global.json" \
        | tr -d '\r' \
        | grep -oE '"version"[[:space:]]*:[[:space:]]*"[^"]+"' \
        | head -n1 \
        | sed -E 's/.*"version"[[:space:]]*:[[:space:]]*"([^"]+)".*/\1/')"

    if [[ -z "${sdkVersion}" ]]; then
        echo "ERROR: Could not determine SDK version from ${REPO_ROOT}/global.json" >&2
        exit 1
    fi

    export DOTNET_ROOT="${HOME}/.dotnet"
    export PATH="${DOTNET_ROOT}:${DOTNET_ROOT}/tools:${PATH}"

    echo "Installing .NET SDK ${sdkVersion} into ${DOTNET_ROOT} ..."
    local installScript
    installScript="$(mktemp)"
    curl -fsSL https://dot.net/v1/dotnet-install.sh -o "${installScript}"
    chmod +x "${installScript}"

    # SDK pinned by global.json (used to build MDS + the perf project).
    "${installScript}" --version "${sdkVersion}" --install-dir "${DOTNET_ROOT}" --no-path

    # Shared runtimes for the frameworks the benchmarks may run against.  Installing all three keeps
    # the script robust regardless of the --framework selected by the pipeline.
    local channel
    for channel in 8.0 9.0 10.0; do
        "${installScript}" --channel "${channel}" --runtime dotnet --install-dir "${DOTNET_ROOT}" --no-path
    done

    rm -f "${installScript}"
}

# Reuse a pre-installed SDK only if it already satisfies global.json; otherwise install locally.
if command -v dotnet >/dev/null 2>&1 && dotnet --list-sdks 2>/dev/null | grep -q '10\.0\.'; then
    echo "Using pre-installed dotnet: $(command -v dotnet)"
    export DOTNET_ROOT="$(dirname -- "$(command -v dotnet)")"
else
    install_dotnet
fi

dotnet --info

####################################################################################################
# 2. Create the perf database on the VM's SQL Server.
#
# The benchmark runners connect to Initial Catalog=sqlclient-perf-db and create their own tables,
# but they do NOT create the database itself, so we create it here (idempotently).
####################################################################################################

DB_NAME="sqlclient-perf-db"

find_sqlcmd() {
    if command -v sqlcmd >/dev/null 2>&1; then
        command -v sqlcmd
        return 0
    fi
    # mssql-tools default install locations.
    local candidate
    for candidate in /opt/mssql-tools18/bin/sqlcmd /opt/mssql-tools/bin/sqlcmd; do
        if [[ -x "${candidate}" ]]; then
            echo "${candidate}"
            return 0
        fi
    done
    return 1
}

echo "Ensuring database [${DB_NAME}] exists on ${SQL_SERVER} ..."
if SQLCMD="$(find_sqlcmd)"; then
    # -C trusts the server certificate (mssql-tools18 requires encryption by default).
    "${SQLCMD}" -S "${SQL_SERVER}" -U sa -P "${SQL_PASSWORD}" -C -b -l 30 \
        -Q "IF DB_ID('${DB_NAME}') IS NULL CREATE DATABASE [${DB_NAME}];"
    echo "Database [${DB_NAME}] is ready."
else
    echo "ERROR: sqlcmd was not found on the VM; cannot create the perf database [${DB_NAME}]." >&2
    echo "       Looked for 'sqlcmd' on PATH and in /opt/mssql-tools18/bin and /opt/mssql-tools/bin." >&2
    exit 1
fi

####################################################################################################
# 3. Inject the VM's SQL Server connection string into the benchmark runner config.
#
# The perf app reads its config from the file named by RUNNER_CONFIG (falling back to
# runnerconfig.jsonc in the working directory).  We copy the checked-in config and replace only the
# ConnectionString value so all benchmark tuning (iterations, row counts, enabled flags) is
# preserved.  python3 is used to JSON-escape the (potentially special-character) password safely.
####################################################################################################

RUNNER_CONFIG="${REPO_ROOT}/perf-runnerconfig.json"
export RUNNER_CONFIG

python3 - "$PERF_DIR/runnerconfig.jsonc" "$RUNNER_CONFIG" <<'PY'
import json, os, re, sys

src, dst = sys.argv[1], sys.argv[2]

with open(src, "r", encoding="utf-8-sig") as fh:
    text = fh.read()

# Strip // line comments so the .jsonc content parses as JSON.  The checked-in config has no "//"
# inside string values, so a simple line-comment strip is safe here.
text = re.sub(r'(?m)^\s*//.*$', '', text)
cfg = json.loads(text)

server = os.environ["SQL_SERVER"]
password = os.environ["SQL_PASSWORD"]
db = os.environ.get("DB_NAME", "sqlclient-perf-db")

cfg["ConnectionString"] = (
    f"Server=tcp:{server},1433;User ID=sa;Password={password};"
    f"Initial Catalog={db};TrustServerCertificate=True;Encrypt=False;"
)

with open(dst, "w", encoding="utf-8") as fh:
    json.dump(cfg, fh, indent=2)

print(f"Wrote runner config to {dst} (Server=tcp:{server},1433; Initial Catalog={db})")
PY

####################################################################################################
# 4 & 5. Run the benchmarks, pinned to the reserved client CPU set.
#
# BenchmarkDotNet writes its artifacts to ./BenchmarkDotNet.Artifacts relative to the working
# directory, so we run from a dedicated run directory and collect from there afterwards.
####################################################################################################

RUN_DIR="${REPO_ROOT}/perf-run"
rm -rf "${RUN_DIR}"
mkdir -p "${RUN_DIR}"

# Build once up-front so the timed run uses --no-build (no build noise inside the measured process).
echo "Building performance tests (${configuration}, ${framework}) ..."
dotnet build "${PERF_PROJECT}" -c "${configuration}" -f "${framework}" --nologo -v minimal

# Assemble the run command, pinning to PERF_CLIENT_CPUS when available.
run_cmd=(dotnet run --project "${PERF_PROJECT}" -c "${configuration}" -f "${framework}" --no-build)

if [[ -n "${PERF_CLIENT_CPUS:-}" ]] && command -v taskset >/dev/null 2>&1; then
    echo "Pinning benchmark client to CPUs ${PERF_CLIENT_CPUS} via taskset."
    run_cmd=(taskset -c "${PERF_CLIENT_CPUS}" "${run_cmd[@]}")
else
    echo "WARNING: PERF_CLIENT_CPUS unset or taskset unavailable; running without CPU pinning." >&2
fi

echo "Running: ${run_cmd[*]}"
(
    cd "${RUN_DIR}"
    "${run_cmd[@]}"
)

####################################################################################################
# 6. Collect BenchmarkDotNet artifacts into the results sub-directory.
####################################################################################################

ARTIFACTS_DIR="${RUN_DIR}/BenchmarkDotNet.Artifacts"
if [[ -d "${ARTIFACTS_DIR}" ]]; then
    echo "Collecting BenchmarkDotNet artifacts into ${RESULTS_DIR} ..."
    # Keep the full artifact tree (logs + per-run report folder) for detailed inspection.
    cp -R "${ARTIFACTS_DIR}" "${RESULTS_DIR}/BenchmarkDotNet.Artifacts"
    # Also flatten the report files (github markdown, csv, html) to the TOP of the results folder.
    # The collect-results template auto-attaches top-level results/*.md files as run summaries, so
    # placing the *-report-github.md reports here makes them show up on the pipeline run.
    if [[ -d "${ARTIFACTS_DIR}/results" ]]; then
        cp -R "${ARTIFACTS_DIR}/results/." "${RESULTS_DIR}/"
    fi
else
    echo "WARNING: No BenchmarkDotNet.Artifacts directory was produced at ${ARTIFACTS_DIR}." >&2
fi

echo "Collected results:"
find "${RESULTS_DIR}" -type f | sort

echo "=================================================================="
echo " Performance run complete."
echo "=================================================================="
