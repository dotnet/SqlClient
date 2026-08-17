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
# Alternative to --baseline-version: benchmark against Microsoft.Data.SqlClient built from ANOTHER
# git ref of this repository (e.g. 'main') instead of a released NuGet package.  Used by the PR perf
# pipeline, which compares the branch under test against the source it would merge into.  The two
# baseline selectors are mutually exclusive.
baselineSourceRef=""
# Remote used to obtain the baseline ref when it cannot be fetched from the copied checkout's own
# 'origin' (e.g. the source tree reached the VM without its .git directory, or origin needs auth).
baselineRepoUrl="https://github.com/dotnet/SqlClient.git"
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
# Optional SqlClient behaviour flags (true/false).  Empty means "leave the checked-in runnerconfig
# default untouched".  These are written into the runner config the benchmarks run against AND, via
# the pipeline's Kusto translation (--config-override), recorded in PerfRun.Config so a run's
# settings are queryable/filterable in the dashboard.
useManagedSniOnWindows=""
useOptimizedAsyncBehaviour=""
useConnectionPoolV2=""
# Alternative to --baseline-version/--baseline-source-ref: an A/B experiment on ONE runner-config
# switch.  Both passes build the SAME source; only the named switch differs (baseline=false,
# current=true), which is the only way to compare a switch whose value is latched process-wide (e.g.
# UseConnectionPoolV2 is read and cached the first time a pool is created).  Mutually exclusive with
# the other two baseline selectors, and overrides the matching --use-* flag (which would otherwise be
# ambiguous: one value cannot describe two passes).
switchUnderTest=""
# Runner-config switches this script is allowed to A/B.  Restricted to a known list so a typo fails
# fast here instead of silently writing an inert key into the runner config and reporting a
# meaningless zero-delta comparison.
SUPPORTED_SWITCHES=("UseConnectionPoolV2" "UseOptimizedAsyncBehaviour" "UseManagedSniOnWindows")

usage() {
    echo "Usage: $0 [--configuration <cfg>] [--framework <tfm>] [--results-subdir <dir>]" \
         "[--baseline-version <ver> | --baseline-source-ref <ref> [--baseline-repo-url <url>] |" \
         "--switch-under-test <${SUPPORTED_SWITCHES[*]}>]" \
         "[--regression-threshold <pct>] [--fail-on-regression]" \
         "[--run-mode interleaved|sequential] [--confirmation-runs <N>]" \
         "[--use-managed-sni-on-windows true|false] [--use-optimized-async-behaviour true|false]" \
         "[--use-connection-pool-v2 true|false]" >&2
}

while [[ $# -gt 0 ]]; do
    case "$1" in
        --configuration) configuration="$2"; shift 2 ;;
        --framework)     framework="$2";     shift 2 ;;
        --results-subdir) resultsSubDir="$2"; shift 2 ;;
        --baseline-version) baselineVersion="$2"; shift 2 ;;
        --baseline-source-ref) baselineSourceRef="$2"; shift 2 ;;
        --baseline-repo-url) baselineRepoUrl="$2"; shift 2 ;;
        --switch-under-test) switchUnderTest="$2"; shift 2 ;;
        --regression-threshold) regressionThreshold="$2"; shift 2 ;;
        --fail-on-regression) failOnRegression="true"; shift 1 ;;
        --run-mode) runMode="$2"; shift 2 ;;
        --confirmation-runs) confirmationRuns="$2"; shift 2 ;;
        --use-managed-sni-on-windows) useManagedSniOnWindows="$2"; shift 2 ;;
        --use-optimized-async-behaviour) useOptimizedAsyncBehaviour="$2"; shift 2 ;;
        --use-connection-pool-v2) useConnectionPoolV2="$2"; shift 2 ;;
        -h|--help)       usage; exit 0 ;;
        *) echo "Unknown argument: $1" >&2; usage; exit 2 ;;
    esac
done

# Validate queue-time inputs up front so a typo fails fast with a clear message instead of silently
# falling back to a different code path (e.g. '--run-mode interleave' would otherwise run sequentially)
# or erroring out obscurely much later.
case "${runMode}" in
    interleaved|sequential) ;;
    *) echo "ERROR: --run-mode must be 'interleaved' or 'sequential' (got '${runMode}')." >&2
       usage; exit 2 ;;
esac
# The three baseline selectors describe different builds/configs of the same "baseline" pass, so
# requesting more than one is always a mistake; fail fast rather than silently honouring one of them.
if [[ -n "${baselineVersion}" && -n "${baselineSourceRef}" ]]; then
    echo "ERROR: --baseline-version and --baseline-source-ref are mutually exclusive." >&2
    usage; exit 2
fi
if [[ -n "${switchUnderTest}" && ( -n "${baselineVersion}" || -n "${baselineSourceRef}" ) ]]; then
    echo "ERROR: --switch-under-test is mutually exclusive with --baseline-version and --baseline-source-ref: it compares the SAME source build with one switch flipped, so mixing in a source change would make the delta unattributable." >&2
    usage; exit 2
fi
if [[ -n "${switchUnderTest}" ]]; then
    switchSupported="false"
    for supported in "${SUPPORTED_SWITCHES[@]}"; do
        [[ "${switchUnderTest}" == "${supported}" ]] && switchSupported="true"
    done
    if [[ "${switchSupported}" != "true" ]]; then
        echo "ERROR: --switch-under-test must be one of: ${SUPPORTED_SWITCHES[*]} (got '${switchUnderTest}')." >&2
        usage; exit 2
    fi
fi
if ! [[ "${confirmationRuns}" =~ ^[0-9]+$ ]] || [[ "${confirmationRuns}" -lt 1 ]]; then
    echo "ERROR: --confirmation-runs must be a positive integer (got '${confirmationRuns}')." >&2
    usage; exit 2
fi
# Each behaviour flag must be empty (leave as-is) or a lowercase boolean.
validate_bool() {  # $1 = flag name (for the message), $2 = value
    case "$2" in
        ""|true|false) ;;
        *) echo "ERROR: --$1 must be 'true' or 'false' (got '$2')." >&2; usage; exit 2 ;;
    esac
}
validate_bool use-managed-sni-on-windows "${useManagedSniOnWindows}"
validate_bool use-optimized-async-behaviour "${useOptimizedAsyncBehaviour}"
validate_bool use-connection-pool-v2 "${useConnectionPoolV2}"
# --switch-under-test forces its switch explicitly for each pass (baseline=false, current=true), so a
# separately-supplied --use-* flag for that SAME switch would be silently overridden; warn rather
# than let that go unnoticed.  Other --use-* flags still apply normally to both passes.
case "${switchUnderTest}" in
    UseConnectionPoolV2)       conflictingValue="${useConnectionPoolV2}";       conflictingFlag="--use-connection-pool-v2" ;;
    UseOptimizedAsyncBehaviour) conflictingValue="${useOptimizedAsyncBehaviour}"; conflictingFlag="--use-optimized-async-behaviour" ;;
    UseManagedSniOnWindows)    conflictingValue="${useManagedSniOnWindows}";    conflictingFlag="--use-managed-sni-on-windows" ;;
    *)                         conflictingValue="";                            conflictingFlag="" ;;
esac
if [[ -n "${conflictingValue}" ]]; then
    echo "WARNING: ${conflictingFlag} is ignored when --switch-under-test is ${switchUnderTest} (baseline forces false, current forces true)." >&2
fi

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
echo "  Baseline ref   : ${baselineSourceRef:-<none>}"
echo "  Switch A/B     : ${switchUnderTest:-<none>}${switchUnderTest:+ (baseline=false vs current=true)}"
echo "  Run mode       : ${runMode} (confirmation runs: ${confirmationRuns})"
echo "  SQL_SERVER     : ${SQL_SERVER:-<unset, will default to localhost>}"
echo "  PERF_CLIENT_CPUS: ${PERF_CLIENT_CPUS:-<unset>}"
echo "  PERF_SQL_CPUS  : ${PERF_SQL_CPUS:-<unset>}"
echo "=================================================================="

if [[ ! -f "${PERF_PROJECT}" ]]; then
    echo "ERROR: Performance test project not found at ${PERF_PROJECT}" >&2
    exit 1
fi

# Export the (possibly defaulted) server so the inline Python config-rewrite below, which reads
# os.environ["SQL_SERVER"], sees it even when the template didn't inject SQL_SERVER.
export SQL_SERVER="${SQL_SERVER:-localhost}"
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
# Connection-churn benches (ConnectionPoolStress, ConnectionPoolRamp,
# ConnectionPoolThreadPoolPressure, ParallelAsyncConnection)
# exhaust ephemeral ports;
# widen the range and allow TIME_WAIT reuse so socket setup latency stays stable.  'sudo -n' keeps
# this non-interactive: on a VM without passwordless sudo it fails immediately instead of blocking
# on a password prompt, then we fall back to a non-sudo sysctl (and finally give up quietly).
if command -v sysctl >/dev/null 2>&1; then
    for kv in "net.ipv4.ip_local_port_range=1024 65535" "net.ipv4.tcp_tw_reuse=1"; do
        sudo -n sysctl -w "${kv}" >/dev/null 2>&1 || sysctl -w "${kv}" >/dev/null 2>&1 || true
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

# Optional SqlClient behaviour flag overrides applied to the runner config (empty = leave as-is).
# Exported so the JSON-patching python below can read them (its heredoc is single-quoted, so it does
# not expand shell variables directly).
export PERF_CFG_USE_MANAGED_SNI="${useManagedSniOnWindows}"
export PERF_CFG_USE_OPTIMIZED_ASYNC="${useOptimizedAsyncBehaviour}"
export PERF_CFG_USE_CONNECTION_POOL_V2="${useConnectionPoolV2}"

# write_runner_config <dst> [switch_name] [switch_value]
# Writes one runner config (checked-in runnerconfig.jsonc + injected connection string + behaviour
# overrides) to <dst>.  When <switch_name> is given, that config key is forced to <switch_value>
# ("true"/"false") regardless of the corresponding PERF_CFG_* value -- used by --switch-under-test,
# which needs a different value for the same switch in each pass.  With no switch name the config is
# built purely from the PERF_CFG_* values, exactly as before.
write_runner_config() {
    local dst="$1"
    local switch_name="${2:-}"
    local switch_value="${3:-}"
    python3 - "$PERF_DIR/runnerconfig.jsonc" "$dst" "$switch_name" "$switch_value" <<'PY'
import json, os, re, sys

src, dst, switch_name, switch_value = sys.argv[1], sys.argv[2], sys.argv[3], sys.argv[4]

with open(src, "r", encoding="utf-8-sig") as fh:
    text = fh.read()

# Strip // line comments so the .jsonc content parses as JSON.  The checked-in config has no "//"
# inside string values, so a simple line-comment strip is safe here.
text = re.sub(r'(?m)^\s*//.*$', '', text)
cfg = json.loads(text)

server = os.environ["SQL_SERVER"]
password = os.environ["SQL_PASSWORD"]
db = os.environ.get("DB_NAME", "sqlclient-perf-db")

# SqlClient connection-string values may be wrapped in double quotes; doubling any embedded double
# quote lets a password containing ';', '=', spaces or single quotes be parsed as a single literal
# value instead of corrupting the connection string.
password_value = '"' + password.replace('"', '""') + '"'

cfg["ConnectionString"] = (
    f"Server=tcp:{server},1433;User ID=sa;Password={password_value};"
    f"Initial Catalog={db};TrustServerCertificate=True;Encrypt=False;"
)

# Apply the optional SqlClient behaviour overrides supplied by the pipeline.  An empty value leaves
# the checked-in default untouched; otherwise the flag is forced to the requested boolean so the
# benchmarks run with (and PerfRun.Config records) exactly the requested behaviour.  The
# switch-under-test override (when this config names one) takes precedence over the corresponding
# PERF_CFG_* value, so a single checked-in template can be stamped out per pass with that one switch
# flipped and everything else identical.
for env_name, cfg_key in (
    ("PERF_CFG_USE_MANAGED_SNI", "UseManagedSniOnWindows"),
    ("PERF_CFG_USE_OPTIMIZED_ASYNC", "UseOptimizedAsyncBehaviour"),
    ("PERF_CFG_USE_CONNECTION_POOL_V2", "UseConnectionPoolV2"),
):
    val = switch_value if cfg_key == switch_name else os.environ.get(env_name, "")
    if val != "":
        cfg[cfg_key] = (val.lower() == "true")

with open(dst, "w", encoding="utf-8") as fh:
    json.dump(cfg, fh, indent=2)

print(f"Wrote runner config to {dst} (Server=tcp:{server},1433; Initial Catalog={db})")
PY
}

write_runner_config "${RUNNER_CONFIG}"

# --switch-under-test needs two DIFFERENT runner configs (baseline runs the switch off, current runs
# it on), so stamp out two more copies here alongside the shared one above.  Everything else in them
# is identical, so any measured delta is attributable to the switch alone.
BASELINE_RUNNER_CONFIG=""
CURRENT_RUNNER_CONFIG=""
if [[ -n "${switchUnderTest}" ]]; then
    BASELINE_RUNNER_CONFIG="${REPO_ROOT}/perf-runnerconfig-baseline.json"
    CURRENT_RUNNER_CONFIG="${REPO_ROOT}/perf-runnerconfig-current.json"
    write_runner_config "${BASELINE_RUNNER_CONFIG}" "${switchUnderTest}" "false"
    write_runner_config "${CURRENT_RUNNER_CONFIG}" "${switchUnderTest}" "true"
fi

####################################################################################################
# 4 & 5. Run the benchmarks, pinned to the reserved client CPU set.
#
# Two passes are executed so the pipeline can compare the branch under test against a baseline:
#   * baseline  -> either Microsoft.Data.SqlClient restored from NuGet.org at ${baselineVersion}
#                  (ReferenceType=Package + CPM VersionOverride), or - when ${baselineSourceRef} is
#                  given instead - the driver built from another git ref of this repository (e.g.
#                  'main', used by the PR perf pipeline).  Skipped when neither is given.
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

# --- Source baseline (--baseline-source-ref) ------------------------------------------------------
# Materialises another git ref of THIS repository (e.g. 'main') next to the checkout so the baseline
# pass can build the driver from that source instead of restoring a released package.  The tree is
# placed OUTSIDE the checkout so it is never picked up by the candidate build, by the results copy,
# or by a repo-root glob.
BASELINE_SRC_DIR="$(dirname -- "${REPO_ROOT}")/sqlclient-perf-baseline-src"
BASELINE_SRC_LABEL=""

# prepare_baseline_source <ref>
# Prefers the copied checkout's own 'origin' (no extra network round-trip, and it resolves the ref
# exactly as the pipeline's repository does); falls back to a shallow clone of --baseline-repo-url
# when the source tree arrived without .git, or when origin is unreachable/needs credentials.
#
# The checkout is copied to the VM WITHOUT credentials (ADO's checkout task defaults to
# persistCredentials:false), so a fetch from an authenticated origin has no way to succeed.  Every
# git command that may touch the network is therefore run through git_net(), which guarantees it
# fails fast and loudly instead of blocking the job on a credential prompt.
GIT_NET_TIMEOUT_SECS="${GIT_NET_TIMEOUT_SECS:-300}"

# git_net <log-name> <git args...>
# Runs a network-facing git command non-interactively, under a hard timeout, capturing all output to
# ${DIAG_DIR}/git-<log-name>.log.  Returns git's exit status (124 if the timeout fired).
#
#   * GIT_TERMINAL_PROMPT=0 turns "needs credentials" into an immediate error rather than a
#     'Username for ...' prompt that waits forever with nothing on the console.
#   * '-c credential.helper=' clears any configured helper (e.g. Git Credential Manager), which
#     would otherwise try to prompt through its own UI/stdin and hang in the same way.
#   * </dev/null stops git from consuming this script's stdin if it prompts anyway.
#   * timeout is a last-resort backstop for any OTHER network stall (DNS, proxy blackhole, ...) so a
#     single git call can never again burn the entire job.
git_net() {
    local logName="$1"; shift
    local log="${DIAG_DIR}/git-${logName}.log"
    local -a cmd=(git -c credential.helper= "$@")

    if command -v timeout >/dev/null 2>&1; then
        cmd=(timeout --signal=TERM --kill-after=30 "${GIT_NET_TIMEOUT_SECS}" "${cmd[@]}")
    fi

    GIT_TERMINAL_PROMPT=0 "${cmd[@]}" </dev/null >"${log}" 2>&1
}

# Echo the tail of a git_net log so a failure is explained in the build log instead of vanishing.
report_git_failure() {
    local logName="$1" status="$2"
    local log="${DIAG_DIR}/git-${logName}.log"
    if [[ "${status}" -eq 124 ]]; then
        echo "  (git ${logName} timed out after ${GIT_NET_TIMEOUT_SECS}s)" >&2
    else
        echo "  (git ${logName} exited ${status})" >&2
    fi
    [[ -s "${log}" ]] && sed 's/^/  | /' "${log}" >&2
    return 0
}

prepare_baseline_source() {
    local ref="$1"

    if ! command -v git >/dev/null 2>&1; then
        echo "ERROR: git is required for --baseline-source-ref but was not found on the VM." >&2
        exit 1
    fi

    rm -rf "${BASELINE_SRC_DIR}"

    local acquired="false"
    local status=0
    # '.git' is a directory in a normal clone but a FILE in a git worktree, so test for existence
    # rather than for a directory.
    if [[ -e "${REPO_ROOT}/.git" ]]; then
        echo "Fetching baseline ref '${ref}' from the checkout's origin ..."
        # Fetch into a private remote-tracking namespace so an existing local branch of the same name
        # (the checkout may itself be on 'main') is never used in place of the fetched ref.
        if git_net fetch -C "${REPO_ROOT}" fetch --no-tags --depth 1 origin \
                "+refs/heads/${ref}:refs/remotes/perfbaseline/${ref}"; then
            git -C "${REPO_ROOT}" worktree prune >/dev/null 2>&1 || true
            if git -C "${REPO_ROOT}" worktree add --detach "${BASELINE_SRC_DIR}" \
                    "refs/remotes/perfbaseline/${ref}" >"${DIAG_DIR}/git-worktree.log" 2>&1; then
                acquired="true"
            else
                report_git_failure worktree "$?"
            fi
        else
            report_git_failure fetch "$?"
        fi
        [[ "${acquired}" == "true" ]] \
            || echo "WARNING: could not materialise '${ref}' from the checkout's origin; falling back to ${baselineRepoUrl}." >&2
    fi

    if [[ "${acquired}" != "true" ]]; then
        echo "Cloning baseline ref '${ref}' from ${baselineRepoUrl} ..."
        rm -rf "${BASELINE_SRC_DIR}"
        # NOTE: '|| status=$?' rather than 'if ! git_net ...' - the '!' would reset $? to 0 and the
        # reported exit code would always be a misleading 0.
        status=0
        git_net clone clone --quiet --depth 1 --branch "${ref}" \
            "${baselineRepoUrl}" "${BASELINE_SRC_DIR}" || status=$?
        if [[ "${status}" -ne 0 ]]; then
            report_git_failure clone "${status}"
            echo "ERROR: could not obtain baseline ref '${ref}' from either the checkout's origin or ${baselineRepoUrl}." >&2
            exit 1
        fi
    fi

    BASELINE_PERF_PROJECT="${BASELINE_SRC_DIR}/src/Microsoft.Data.SqlClient/tests/PerformanceTests/Microsoft.Data.SqlClient.PerformanceTests.csproj"
    if [[ ! -f "${BASELINE_PERF_PROJECT}" ]]; then
        echo "ERROR: baseline source tree at ${BASELINE_SRC_DIR} has no performance test project (${BASELINE_PERF_PROJECT})." >&2
        exit 1
    fi

    local sha
    sha="$(git -C "${BASELINE_SRC_DIR}" rev-parse --short HEAD 2>/dev/null || echo unknown)"
    # Label recorded in the comparison output so a run states exactly which baseline commit it used.
    BASELINE_SRC_LABEL="${ref}@${sha}"
    echo "Baseline source ready: ${BASELINE_SRC_DIR} (${BASELINE_SRC_LABEL})"
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

# run_pass <label> <project> <extra dotnet build/run args...>
# Builds and runs one benchmark pass from <project>, pinned to PERF_CLIENT_CPUS, collecting artifacts
# into ${RESULTS_DIR}/<label>.  <project> is the candidate's perf project for every pass except a
# source-baseline pass, which builds the baseline ref's own perf project.
run_pass() {
    local label="$1"; shift
    local project="$1"; shift
    local extra_args=("$@")

    local run_dir="${REPO_ROOT}/perf-run-${label}"
    rm -rf "${run_dir}"
    mkdir -p "${run_dir}"

    echo "------------------------------------------------------------------"
    echo " Pass: ${label}"
    echo "   Project   : ${project}"
    echo "   Extra args: ${extra_args[*]:-<none>}"
    echo "------------------------------------------------------------------"

    echo "Building performance tests (${configuration}, ${framework}) for '${label}' ..."
    dotnet build "${project}" -c "${configuration}" -f "${framework}" --nologo -v minimal \
        "${extra_args[@]}"

    local run_cmd=(dotnet run --project "${project}" -c "${configuration}" -f "${framework}" \
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

# build_variant <label> <project> <extra dotnet build args...>
# Builds the PerformanceTests app once into its own output directory (perf-build-<label>) so the
# interleaved orchestrator can invoke it repeatedly without rebuilding.  The two variants (baseline
# package or baseline source vs candidate source) must go to distinct dirs because they would
# otherwise share the project's bin path.
build_variant() {
    local label="$1"; shift
    local project="$1"; shift
    local out_dir="${REPO_ROOT}/perf-build-${label}"
    rm -rf "${out_dir}"
    mkdir -p "${out_dir}"
    echo "Building '${label}' variant (${configuration}, ${framework}) from ${project} into ${out_dir} ..."
    dotnet build "${project}" -c "${configuration}" -f "${framework}" --nologo -v minimal \
        -o "${out_dir}" "$@"
}

# --- Resolve how the baseline pass is produced ----------------------------------------------------
# Both baseline flavours end up as "a perf project plus build args"; resolving them once here keeps
# the interleaved and sequential paths below identical for the package and source baselines.
baselineLabel=""
baselineProject="${PERF_PROJECT}"
baselineBuildArgs=()

if [[ -n "${baselineVersion}" ]]; then
    # Released package baseline: candidate's perf project, MDS swapped to a NuGet package reference.
    write_baseline_nuget_config
    baselineLabel="${baselineVersion}"
    baselineBuildArgs=(
        -p:ReferenceType=Package
        -p:MdsPackageVersion="${baselineVersion}"
        -p:RestoreConfigFile="${BASELINE_NUGET_CONFIG}"
    )
elif [[ -n "${baselineSourceRef}" ]]; then
    # Source baseline: build the baseline ref's OWN perf project so the driver under measurement is
    # that ref's source (ProjectReference), exactly as the candidate pass builds this branch's.
    prepare_baseline_source "${baselineSourceRef}"
    baselineLabel="${BASELINE_SRC_LABEL}"
    baselineProject="${BASELINE_PERF_PROJECT}"
elif [[ -n "${switchUnderTest}" ]]; then
    # Switch A/B: SAME source/project for both passes (baselineProject/baselineBuildArgs are already
    # the candidate's, set above), so only the runner config differs (see BASELINE_RUNNER_CONFIG /
    # CURRENT_RUNNER_CONFIG written above: the named switch off vs on).
    baselineLabel="${switchUnderTest}=false"
fi

# Record the resolved baseline label (for a source baseline this is '<ref>@<sha>') in the results
# tree.  The results directory is copied back to the agent, so a post-test step can read this and
# tag the build with the exact baseline that was measured - something the pipeline itself cannot do,
# since the SHA is only known once the ref has been resolved here on the VM.
if [[ -n "${baselineLabel}" ]]; then
    printf '%s\n' "${baselineLabel}" > "${RESULTS_DIR}/baseline-label.txt"
fi

if [[ -n "${baselineLabel}" && "${runMode}" == "interleaved" ]]; then
    ################################################################################################
    # Interleaved + best-of-N (wiki 339 §2.2/§2.3/§2.6).  Build both variants once, then let the
    # orchestrator run one unit at a time (baseline then candidate) and confirm any flagged
    # regression across N passes before it counts toward the gate.
    ################################################################################################
    if [[ -n "${switchUnderTest}" ]]; then
        # Switch A/B measures one build against itself with a switch flipped, so building the same
        # project twice would just burn several minutes producing identical bits.  Build once and
        # point both variants at it; the orchestrator runs each variant in its own working directory
        # (rep<N>/<variant>/<unit>), so a shared exe dir cannot cross-contaminate their artifacts.
        build_variant "current" "${PERF_PROJECT}"
        baselineExeDir="${REPO_ROOT}/perf-build-current"
        currentExeDir="${REPO_ROOT}/perf-build-current"
    else
        build_variant "baseline" "${baselineProject}" ${baselineBuildArgs[@]+"${baselineBuildArgs[@]}"}
        build_variant "current" "${PERF_PROJECT}"
        baselineExeDir="${REPO_ROOT}/perf-build-baseline"
        currentExeDir="${REPO_ROOT}/perf-build-current"
    fi

    interleave_args=(
        --baseline-exe-dir "${baselineExeDir}"
        --current-exe-dir "${currentExeDir}"
        --assembly "PerformanceTests.dll"
        --results-dir "${RESULTS_DIR}"
        --threshold "${regressionThreshold}"
        --reps "${confirmationRuns}"
        --baseline-version "${baselineLabel}"
        --client-cpus "${PERF_CLIENT_CPUS:-}"
    )
    # --switch-under-test: baseline and current subprocesses need DIFFERENT RUNNER_CONFIG values (the
    # switch off vs on), even though both are otherwise the same build/env; every other baseline
    # flavour keeps sharing the single ambient RUNNER_CONFIG set above.
    if [[ -n "${switchUnderTest}" ]]; then
        interleave_args+=(
            --baseline-runner-config "${BASELINE_RUNNER_CONFIG}"
            --current-runner-config "${CURRENT_RUNNER_CONFIG}"
        )
        # A switch experiment flips intended behaviour, so benchmarks that measure that behaviour
        # regress by design. If the switch has an annotation file, hand it to the comparison so those
        # show up as expected differences instead of being re-litigated every run. Optional: a switch
        # with no annotated benchmarks simply has no file.
        expectedDifferencesFile="${SCRIPT_DIR}/../expected-differences/${switchUnderTest}.json"
        if [[ -f "${expectedDifferencesFile}" ]]; then
            echo "Using expected-differences annotations: ${expectedDifferencesFile}"
            interleave_args+=(--expected-differences "${expectedDifferencesFile}")
        else
            echo "No expected-differences file for ${switchUnderTest}; all regressions will be reported as such."
        fi
    fi
    if [[ "${failOnRegression}" == "true" ]]; then
        echo "Regression gate ENABLED: a CONFIRMED candidate-slower regression (> ${regressionThreshold}%) will fail the run."
        interleave_args+=(--fail-on-regression)
    fi
    echo "Running interleaved benchmarks (best-of-${confirmationRuns}) ..."
    python3 "${SCRIPT_DIR}/interleave_perf.py" "${interleave_args[@]}"

elif [[ -n "${baselineLabel}" ]]; then
    # --- Legacy sequential path: full baseline pass, then full candidate pass, then compare -------
    # --switch-under-test needs a different RUNNER_CONFIG per pass; every other baseline flavour
    # keeps using the single ambient RUNNER_CONFIG exported above (unchanged behaviour).
    if [[ -n "${switchUnderTest}" ]]; then
        export RUNNER_CONFIG="${BASELINE_RUNNER_CONFIG}"
    fi
    run_pass "baseline" "${baselineProject}" ${baselineBuildArgs[@]+"${baselineBuildArgs[@]}"}
    if [[ -n "${switchUnderTest}" ]]; then
        export RUNNER_CONFIG="${CURRENT_RUNNER_CONFIG}"
    fi
    run_pass "current" "${PERF_PROJECT}"

    echo "Comparing current branch against baseline ${baselineLabel} ..."
    mkdir -p "${RESULTS_DIR}/comparison"
    compare_args=(
        --baseline-dir "${RESULTS_DIR}/baseline"
        --current-dir "${RESULTS_DIR}/current"
        --baseline-version "${baselineLabel}"
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
    echo "Neither --baseline-version nor --baseline-source-ref supplied; running current only (no comparison)."
    run_pass "current" "${PERF_PROJECT}"
fi

echo "Collected results:"
find "${RESULTS_DIR}" -type f | sort

echo "=================================================================="
echo " Performance run complete."
echo "=================================================================="
