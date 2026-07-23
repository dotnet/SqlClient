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
# When true, a candidate-slower-than-baseline regression fails the run (wiki 339 §3 gate).
# Default off so the pipeline reports deltas without blocking until the gate is trusted.
failOnRegression="false"
# Benchmark run model (wiki 339 §2.2/§2.3/§2.6):
#   interleaved -> run one unit at a time, baseline and candidate back-to-back, with best-of-N
#                  confirmation of flagged regressions (the noise-resistant default).
#   sequential  -> legacy: run the whole baseline suite, then the whole candidate suite, then compare.
runMode="interleaved"
# Best-of-N: total interleaved passes for a flagged unit before a regression is confirmed (1 disables).
confirmationRuns="3"

usage() {
    echo "Usage: $0 [--configuration <cfg>] [--framework <tfm>] [--results-subdir <dir>]" \
         "[--baseline-version <ver>] [--regression-threshold <pct>] [--fail-on-regression]" \
         "[--run-mode interleaved|sequential] [--confirmation-runs <N>]" >&2
}

while [[ $# -gt 0 ]]; do
    case "$1" in
        --configuration) configuration="$2"; shift 2 ;;
        --framework)     framework="$2";     shift 2 ;;
        --results-subdir) resultsSubDir="$2"; shift 2 ;;
        --baseline-version) baselineVersion="$2"; shift 2 ;;
        --regression-threshold) regressionThreshold="$2"; shift 2 ;;
        --fail-on-regression) failOnRegression="true"; shift 1 ;;
        --run-mode) runMode="$2"; shift 2 ;;
        --confirmation-runs) confirmationRuns="$2"; shift 2 ;;
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
echo "  Run mode       : ${runMode} (confirmation runs: ${confirmationRuns})"
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
# 'dotnet --version' evaluated from the repo root honours global.json (including its rollForward
# policy), so it succeeds only when the pinned SDK is actually available -- a hard-coded '10.0.*'
# match would accept the wrong SDK band and skip installing the pinned one.
if command -v dotnet >/dev/null 2>&1 && ( cd "${REPO_ROOT}" && dotnet --version >/dev/null 2>&1 ); then
    dotnetPath="$(command -v dotnet)"
    # 'dotnet' on PATH is frequently a symlink (e.g. /usr/bin/dotnet -> /usr/share/dotnet/dotnet);
    # resolve it so DOTNET_ROOT points at the real install root, not the symlink's directory.
    resolvedDotnet="$(readlink -f "${dotnetPath}" 2>/dev/null || echo "${dotnetPath}")"
    export DOTNET_ROOT="$(dirname -- "${resolvedDotnet}")"
    export PATH="${DOTNET_ROOT}:${DOTNET_ROOT}/tools:${PATH}"
    echo "Using pre-installed dotnet: ${dotnetPath} (DOTNET_ROOT=${DOTNET_ROOT})"
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

# Exported so the inline Python config-rewrite below (which reads os.environ["DB_NAME"]) sees the
# same database name instead of silently falling back to its own default.
export DB_NAME="sqlclient-perf-db"

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
# Noise-reduction controls (InternalDriverTools wiki 339, "Reducing Noise in Performance Tests").
#
# The Perf Test Lab already provides the isolated dedicated host, the tuned SQL instance and the
# disjoint client CPU set (PERF_CLIENT_CPUS, pinned per pass below).  These are the remaining
# harness-owned controls: allocator/network tuning, per-run diagnostics, a fail-loud preflight and
# a warm-up, so a run's mean/variance is steadier and a broken run cannot masquerade as a pass.
####################################################################################################

DIAG_DIR="${RESULTS_DIR}/diagnostics"
mkdir -p "${DIAG_DIR}"

# --- §2.8 Allocator tuning (exported so the 'dotnet run' children inherit it) ---------------------
# Large-buffer benches (AsyncLargeDataRead, SqlBulkCopy) re-mmap a big buffer every iteration under
# glibc malloc; keep those allocations on the heap and stop trimming freed pages so they are reused,
# which removes a major source of per-iteration variance.
export MALLOC_MMAP_THRESHOLD_="${MALLOC_MMAP_THRESHOLD_:-134217728}"   # 128 MiB
export MALLOC_TRIM_THRESHOLD_="${MALLOC_TRIM_THRESHOLD_:--1}"          # never trim

# --- §2.9 Network tuning (best-effort; needs privilege, so it must never fail the run) ------------
# Connection-churn benches (ConnectionPoolStress, ParallelAsyncConnection) exhaust ephemeral ports;
# widen the range and allow TIME_WAIT reuse so socket setup latency stays stable.
if command -v sysctl >/dev/null 2>&1; then
    for kv in "net.ipv4.ip_local_port_range=1024 65535" "net.ipv4.tcp_tw_reuse=1"; do
        sudo sysctl -w "${kv}" >/dev/null 2>&1 || sysctl -w "${kv}" >/dev/null 2>&1 || true
    done
fi

# --- §2.11 Capture host CPU topology (static, once per run) ---------------------------------------
{ command -v lscpu >/dev/null 2>&1 && lscpu; } > "${DIAG_DIR}/cpu-info.txt" 2>&1 || true

# --- §2.11 Capture the SQL instance configuration (confirm the lab tuning actually took effect) ---
"${SQLCMD}" -S "${SQL_SERVER}" -U sa -P "${SQL_PASSWORD}" -C -b -l 30 -h -1 -W \
    -Q "SET NOCOUNT ON;
        SELECT name, value_in_use FROM sys.configurations
          WHERE name IN ('max degree of parallelism','cost threshold for parallelism',
                         'max server memory (MB)','min server memory (MB)','affinity mask',
                         'affinity I/O mask');
        SELECT 'tempdb_data_files' AS setting, COUNT(*) AS value FROM tempdb.sys.database_files WHERE type = 0;
        SELECT @@VERSION;" \
    > "${DIAG_DIR}/sql-config.txt" 2>&1 \
    && echo "Captured SQL instance config -> ${DIAG_DIR}/sql-config.txt" \
    || echo "WARNING: could not capture SQL instance config (continuing)." >&2

# --- §2.10 / §2.5 Fail loud on an unreachable server, and warm the buffer pool / plan cache -------
# A benchmark suite that "skips" when the server is down produces an empty comparison that reads
# green; verify connectivity up front and touch the target DB so the first measured benchmark is not
# paying cold-cache costs.
if ! "${SQLCMD}" -S "${SQL_SERVER}" -U sa -P "${SQL_PASSWORD}" -C -b -l 15 \
        -Q "SET NOCOUNT ON; USE [${DB_NAME}]; SELECT 1;" >/dev/null 2>&1; then
    echo "ERROR: SQL Server ${SQL_SERVER} (db ${DB_NAME}) is unreachable; refusing to run so an empty perf comparison cannot be reported as a pass." >&2
    exit 1
fi
echo "Preflight: SQL Server ${SQL_SERVER} (db ${DB_NAME}) is reachable and warmed."

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

# The perf app also loads datatypes.json via the DATATYPES_CONFIG env var, falling back to
# "datatypes.json" in the working directory.  Each pass runs from an otherwise-empty
# perf-run-<label> dir, so without this the app throws FileNotFoundException for datatypes.json.
# It needs no per-run modification, so point the env var at the checked-in file directly.
DATATYPES_CONFIG="${PERF_DIR}/datatypes.json"
export DATATYPES_CONFIG

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

# capture_cpu_telemetry <label> <before|after>
# §2.11 telemetry snapshot: per-core frequency and thermal state around each measured pass, so a
# drifting/throttling result can be explained after the fact.  Best-effort; never fails the run.
capture_cpu_telemetry() {
    local label="$1" when="$2"
    {
        date -u +"%Y-%m-%dT%H:%M:%SZ"
        grep -E "^cpu MHz" /proc/cpuinfo 2>/dev/null || true
        for z in /sys/class/thermal/thermal_zone*/temp; do
            [[ -r "${z}" ]] && echo "thermal ${z}=$(cat "${z}" 2>/dev/null)"
        done
    } > "${DIAG_DIR}/cpu-${label}-${when}.txt" 2>&1 || true
}

# count_benchmark_results <dir>
# Counts the BenchmarkDotNet results recorded under <dir> (the "Benchmarks" array of every
# *-report-full.json).  Used to fail loud when a pass produced nothing.
count_benchmark_results() {
    python3 - "$1" <<'PY'
import glob, json, os, sys
root = sys.argv[1]
total = 0
for f in glob.glob(os.path.join(root, "**", "*-report-full.json"), recursive=True):
    try:
        with open(f, encoding="utf-8-sig") as fh:
            total += len(json.load(fh).get("Benchmarks", []))
    except Exception:
        pass
print(total)
PY
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
    capture_cpu_telemetry "${label}" "before"
    (
        cd "${run_dir}"
        "${run_cmd[@]}"
    )
    capture_cpu_telemetry "${label}" "after"

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

    # §2.10 Fail loud: a pass that produced zero benchmark results (server dropped, all benches
    # errored, exporter disabled) must not flow through to an empty comparison that reads green.
    local nresults
    nresults="$(count_benchmark_results "${dest}")"
    echo "Pass '${label}' produced ${nresults} benchmark result(s)."
    if [[ "${nresults}" -eq 0 ]]; then
        echo "ERROR: pass '${label}' produced no benchmark results; failing the run (a broken benchmark pass must not be reported as a pass)." >&2
        exit 1
    fi
}

# build_variant <label> <extra dotnet build args...>
# Builds the PerformanceTests app once into its own output directory (perf-build-<label>) so the
# interleaved orchestrator can invoke it repeatedly without rebuilding.  The two variants (baseline
# package vs candidate source) must go to distinct dirs because they share the project's bin path.
build_variant() {
    local label="$1"; shift
    local out_dir="${REPO_ROOT}/perf-build-${label}"
    rm -rf "${out_dir}"
    mkdir -p "${out_dir}"
    echo "Building '${label}' variant (${configuration}, ${framework}) into ${out_dir} ..."
    dotnet build "${PERF_PROJECT}" -c "${configuration}" -f "${framework}" --nologo -v minimal \
        -o "${out_dir}" "$@"
}

# --- Baseline pass (released NuGet package) -------------------------------------------------------
if [[ -n "${baselineVersion}" && "${runMode}" == "interleaved" ]]; then
    ################################################################################################
    # Interleaved + best-of-N (wiki 339 §2.2/§2.3/§2.6).  Build both variants once, then let the
    # orchestrator run one unit at a time (baseline then candidate) and confirm any flagged
    # regression across N passes before it counts toward the gate.
    ################################################################################################
    write_baseline_nuget_config
    build_variant "baseline" \
        -p:ReferenceType=Package \
        -p:MdsPackageVersion="${baselineVersion}" \
        -p:RestoreConfigFile="${BASELINE_NUGET_CONFIG}"
    build_variant "current"

    interleave_args=(
        --baseline-exe-dir "${REPO_ROOT}/perf-build-baseline"
        --current-exe-dir "${REPO_ROOT}/perf-build-current"
        --assembly "PerformanceTests.dll"
        --results-dir "${RESULTS_DIR}"
        --threshold "${regressionThreshold}"
        --reps "${confirmationRuns}"
        --baseline-version "${baselineVersion}"
        --client-cpus "${PERF_CLIENT_CPUS:-}"
    )
    if [[ "${failOnRegression}" == "true" ]]; then
        echo "Regression gate ENABLED: a CONFIRMED candidate-slower regression (> ${regressionThreshold}%) will fail the run."
        interleave_args+=(--fail-on-regression)
    fi
    echo "Running interleaved benchmarks (best-of-${confirmationRuns}) ..."
    python3 "${SCRIPT_DIR}/interleave_perf.py" "${interleave_args[@]}"

elif [[ -n "${baselineVersion}" ]]; then
    # --- Legacy sequential path: full baseline pass, then full candidate pass, then compare -------
    write_baseline_nuget_config
    run_pass "baseline" \
        -p:ReferenceType=Package \
        -p:MdsPackageVersion="${baselineVersion}" \
        -p:RestoreConfigFile="${BASELINE_NUGET_CONFIG}"
    run_pass "current"

    echo "Comparing current branch against baseline ${baselineVersion} ..."
    mkdir -p "${RESULTS_DIR}/comparison"
    compare_args=(
        --baseline-dir "${RESULTS_DIR}/baseline"
        --current-dir "${RESULTS_DIR}/current"
        --baseline-version "${baselineVersion}"
        --threshold "${regressionThreshold}"
        --out-md "${RESULTS_DIR}/comparison/comparison.md"
        --out-json "${RESULTS_DIR}/comparison/comparison.json"
    )
    # §3 gate: only a candidate-slower regression fails, and only when explicitly enabled.
    if [[ "${failOnRegression}" == "true" ]]; then
        echo "Regression gate ENABLED: a candidate-slower regression (> ${regressionThreshold}%) will fail the run."
        compare_args+=(--fail-on-regression)
    fi
    python3 "${SCRIPT_DIR}/compare_perf.py" "${compare_args[@]}"
    # Surface the comparison as the top-level run summary (collect-results.yml attaches results/*.md).
    cp "${RESULTS_DIR}/comparison/comparison.md" "${RESULTS_DIR}/summary.md"

else
    # --- No baseline: current-only run (no comparison) --------------------------------------------
    echo "No --baseline-version supplied; running current only (no comparison)."
    run_pass "current"
fi

echo "Collected results:"
find "${RESULTS_DIR}" -type f | sort

echo "=================================================================="
echo " Performance run complete."
echo "=================================================================="
