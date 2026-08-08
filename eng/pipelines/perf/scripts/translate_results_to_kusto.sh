#!/usr/bin/env bash
####################################################################################################
# Licensed to the .NET Foundation under one or more agreements.  The .NET Foundation licenses this
# file to you under the MIT license.  See the LICENSE file in the project root for more information.
####################################################################################################
#
# translate_results_to_kusto.sh
#
# Agent-side post-test step for sqlclient-perf-pipeline.yml.  Translates the BenchmarkDotNet "full"
# JSON from the current (and optional baseline) pass into the Kusto perf-results schema (wiki 270):
# one PerfRun row + N PerfBenchmarkResult rows per pass, written as NDJSON.  Kept in its own file
# (rather than inline in the pipeline YAML) for readability; all pipeline context is passed in via
# environment variables so this script stays free of ADO macro/expression syntax.
#
# Environment (set by the pipeline step's 'env' mapping):
#   REPO_DIR                    Checkout root of the driver repo on the agent.
#   RESULTS_DIR                 Directory holding the collected 'current'/'baseline' results.
#   KUSTO_OUT                   Output directory for the translated NDJSON payloads.
#   COLLECTION_URI, TEAM_PROJECT, BUILD_ID   Used to build the pipeline-run URL recorded on each row.
#   AGENT_MACHINE_NAME          Fallback machine name when the VM didn't report its hostname.
#   SOURCE_VERSION, SOURCE_BRANCH   Commit + branch under test.
#   PLATFORM, RUN_MODE          Operating system + benchmark run mode recorded on each row.
#   BASELINE_VERSION            Released package version for the baseline pass (empty when none ran).
#   CFG_USE_MANAGED_SNI, CFG_USE_OPTIMIZED_ASYNC, CFG_USE_CONNECTION_POOL_V2
#                               SqlClient behaviour flags recorded in PerfRun.Config for both passes.
#
####################################################################################################
set -euo pipefail

repoDir="${REPO_DIR:?REPO_DIR must be set}"
resultsDir="${RESULTS_DIR:?RESULTS_DIR must be set}"
kustoOut="${KUSTO_OUT:?KUSTO_OUT must be set}"
scriptsDir="$repoDir/eng/pipelines/perf/scripts"
mkdir -p "$kustoOut"

if [ ! -d "$resultsDir/current" ]; then
    echo "##vso[task.logissue type=warning]No 'current' results found; skipping Kusto translation."
    exit 0
fi

buildUrl="${COLLECTION_URI}${TEAM_PROJECT}/_build/results?buildId=${BUILD_ID}"

machineName="${AGENT_MACHINE_NAME}"
# The VM writes its hostname into runinfo.env; prefer it when present.  Extract the value as data
# (grep/cut) instead of sourcing the file, so unexpected/corrupted file content can never execute
# as shell code.
if [ -f "$resultsDir/runinfo.env" ]; then
    envMachineName="$(grep -m1 '^MACHINE_NAME=' "$resultsDir/runinfo.env" 2>/dev/null | cut -d= -f2- || true)"
    # A Windows VM writes runinfo.env with CRLF endings; strip any trailing CR so the hostname
    # doesn't leak a '\r' into PerfRun.MachineName.
    envMachineName="${envMachineName%$'\r'}"
    machineName="${envMachineName:-$machineName}"
fi

commitHash="${SOURCE_VERSION}"
commitDate="$(git -C "$repoDir" show -s --format=%cI "$commitHash" 2>/dev/null || true)"
shortSha="$(git -C "$repoDir" rev-parse --short "$commitHash" 2>/dev/null || echo "$commitHash")"

# The benchmark runner's config governs which SqlClient behaviours a run exercised; its top-level
# boolean flags are recorded in PerfRun.Config for both passes.
runnerConfig="$repoDir/src/Microsoft.Data.SqlClient/tests/PerformanceTests/runnerconfig.jsonc"
configOverrides=(
    --config-override "UseManagedSniOnWindows=${CFG_USE_MANAGED_SNI}"
    --config-override "UseOptimizedAsyncBehaviour=${CFG_USE_OPTIMIZED_ASYNC}"
    --config-override "UseConnectionPoolV2=${CFG_USE_CONNECTION_POOL_V2}"
)

# --- current (branch under test) ---
python3 "$scriptsDir/perf_to_kusto.py" \
    --input-dir "$resultsDir/current" \
    --out-run "$kustoOut/current.PerfRun.ndjson" \
    --out-results "$kustoOut/current.PerfBenchmarkResult.ndjson" \
    --driver-name "Microsoft.Data.SqlClient" \
    --machine-name "$machineName" \
    --agent-name "$AGENT_MACHINE_NAME" \
    --operating-system "$PLATFORM" \
    --run-type "$RUN_MODE" \
    --pipeline-run-id "$BUILD_ID" \
    --build-url "$buildUrl" \
    --branch-name "$SOURCE_BRANCH" \
    --version-string "$shortSha" \
    --commit-hash "$commitHash" \
    --commit-date "$commitDate" \
    --runner-config "$runnerConfig" \
    "${configOverrides[@]}" \
    --is-comparable-base false

# --- baseline (released NuGet package), when a baseline pass ran ---
if [ -d "$resultsDir/baseline" ]; then
    baseVer="${BASELINE_VERSION}"
    python3 "$scriptsDir/perf_to_kusto.py" \
        --input-dir "$resultsDir/baseline" \
        --out-run "$kustoOut/baseline.PerfRun.ndjson" \
        --out-results "$kustoOut/baseline.PerfBenchmarkResult.ndjson" \
        --driver-name "Microsoft.Data.SqlClient" \
        --machine-name "$machineName" \
        --agent-name "$AGENT_MACHINE_NAME" \
        --operating-system "$PLATFORM" \
        --run-type "$RUN_MODE" \
        --pipeline-run-id "$BUILD_ID" \
        --build-url "$buildUrl" \
        --branch-name "refs/tags/v$baseVer" \
        --version-string "$baseVer" \
        --commit-hash "v$baseVer" \
        --runner-config "$runnerConfig" \
        "${configOverrides[@]}" \
        --is-comparable-base true
fi

echo "Translated Kusto payloads:"
find "$kustoOut" -type f | sort
