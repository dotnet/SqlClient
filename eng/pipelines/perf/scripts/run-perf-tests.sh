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
baselineVersion=""
regressionThreshold="10"

usage() {
    echo "Usage: $0 [--configuration <cfg>] [--framework <tfm>] [--results-subdir <dir>]" \
         "[--baseline-version <ver>] [--regression-threshold <pct>]" >&2
}

while [[ $# -gt 0 ]]; do
    case "$1" in
        --configuration) configuration="$2"; shift 2 ;;
        --framework)     framework="$2";     shift 2 ;;
        --results-subdir) resultsSubDir="$2"; shift 2 ;;
        --baseline-version) baselineVersion="$2"; shift 2 ;;
        --regression-threshold) regressionThreshold="$2"; shift 2 ;;
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
echo "  Baseline ver   : ${baselineVersion:-<none, current-only>}"
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

# Record VM-side run metadata (e.g. the perf VM hostname) for the agent-side Kusto translation.
{
    echo "MACHINE_NAME=$(hostname)"
} > "${RESULTS_DIR}/runinfo.env"

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
# Two passes are executed so the pipeline can compare the branch under test against a released
# baseline:
#   * baseline  -> Microsoft.Data.SqlClient restored from NuGet.org at ${baselineVersion}
#                  (ReferenceType=Package + CPM VersionOverride).  Skipped when no baseline is given.
#   * current   -> Microsoft.Data.SqlClient built from the source tree in this repo (ProjectReference).
#
# BenchmarkDotNet writes its artifacts to ./BenchmarkDotNet.Artifacts relative to the working
# directory, so each pass runs from its own directory and its artifacts are collected into
# results/<label>/.
####################################################################################################

# NuGet.config on the VM exposes only the governed feed; the baseline package (and its public deps)
# live on NuGet.org.  Central Package Management rejects multiple unmapped sources (NU1507), so the
# baseline restore uses a dedicated single-source config pointing only at NuGet.org.
BASELINE_NUGET_CONFIG="${REPO_ROOT}/perf-baseline-nuget.config"
write_baseline_nuget_config() {
    cat > "${BASELINE_NUGET_CONFIG}" <<'XML'
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
</configuration>
XML
}

# run_pass <label> <extra dotnet build/run args...>
# Builds and runs one benchmark pass, pinned to PERF_CLIENT_CPUS, collecting artifacts into
# ${RESULTS_DIR}/<label>.
run_pass() {
    local label="$1"; shift
    local extra_args=("$@")

    local run_dir="${REPO_ROOT}/perf-run-${label}"
    rm -rf "${run_dir}"
    mkdir -p "${run_dir}"

    echo "------------------------------------------------------------------"
    echo " Pass: ${label}"
    echo "   Extra args: ${extra_args[*]:-<none>}"
    echo "------------------------------------------------------------------"

    echo "Building performance tests (${configuration}, ${framework}) for '${label}' ..."
    dotnet build "${PERF_PROJECT}" -c "${configuration}" -f "${framework}" --nologo -v minimal \
        "${extra_args[@]}"

    local run_cmd=(dotnet run --project "${PERF_PROJECT}" -c "${configuration}" -f "${framework}" \
        --no-build "${extra_args[@]}")

    if [[ -n "${PERF_CLIENT_CPUS:-}" ]] && command -v taskset >/dev/null 2>&1; then
        echo "Pinning benchmark client to CPUs ${PERF_CLIENT_CPUS} via taskset."
        run_cmd=(taskset -c "${PERF_CLIENT_CPUS}" "${run_cmd[@]}")
    else
        echo "WARNING: PERF_CLIENT_CPUS unset or taskset unavailable; running without CPU pinning." >&2
    fi

    echo "Running (${label}): ${run_cmd[*]}"
    (
        cd "${run_dir}"
        "${run_cmd[@]}"
    )

    local artifacts_dir="${run_dir}/BenchmarkDotNet.Artifacts"
    local dest="${RESULTS_DIR}/${label}"
    mkdir -p "${dest}"
    if [[ -d "${artifacts_dir}" ]]; then
        echo "Collecting '${label}' BenchmarkDotNet artifacts into ${dest} ..."
        cp -R "${artifacts_dir}" "${dest}/BenchmarkDotNet.Artifacts"
        if [[ -d "${artifacts_dir}/results" ]]; then
            cp -R "${artifacts_dir}/results/." "${dest}/"
        fi
    else
        echo "WARNING: No BenchmarkDotNet.Artifacts directory was produced for '${label}' at ${artifacts_dir}." >&2
    fi
}

# --- Baseline pass (released NuGet package) -------------------------------------------------------
if [[ -n "${baselineVersion}" ]]; then
    write_baseline_nuget_config
    run_pass "baseline" \
        -p:ReferenceType=Package \
        -p:MdsPackageVersion="${baselineVersion}" \
        -p:RestoreConfigFile="${BASELINE_NUGET_CONFIG}"
else
    echo "No --baseline-version supplied; skipping the baseline pass."
fi

# --- Current pass (branch under test, built from source) ------------------------------------------
run_pass "current"

####################################################################################################
# 6. Compare the two passes and surface a delta (only when a baseline pass ran).
####################################################################################################

if [[ -n "${baselineVersion}" ]]; then
    echo "Comparing current branch against baseline ${baselineVersion} ..."
    mkdir -p "${RESULTS_DIR}/comparison"
    python3 "${SCRIPT_DIR}/compare_perf.py" \
        --baseline-dir "${RESULTS_DIR}/baseline" \
        --current-dir "${RESULTS_DIR}/current" \
        --baseline-version "${baselineVersion}" \
        --threshold "${regressionThreshold}" \
        --out-md "${RESULTS_DIR}/comparison/comparison.md" \
        --out-json "${RESULTS_DIR}/comparison/comparison.json"
    # Surface the comparison as the top-level run summary (collect-results.yml attaches results/*.md).
    cp "${RESULTS_DIR}/comparison/comparison.md" "${RESULTS_DIR}/summary.md"
fi

echo "Collected results:"
find "${RESULTS_DIR}" -type f | sort

echo "=================================================================="
echo " Performance run complete."
echo "=================================================================="
