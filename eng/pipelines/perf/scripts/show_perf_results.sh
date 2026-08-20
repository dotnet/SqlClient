#!/usr/bin/env bash
####################################################################################################
# Licensed to the .NET Foundation under one or more agreements.  The .NET Foundation licenses this
# file to you under the MIT license.  See the LICENSE file in the project root for more information.
####################################################################################################
#
# show_perf_results.sh
#
# Agent-side post-test step for sqlclient-perf-pipeline.yml: lists the collected performance results
# and prints every BenchmarkDotNet GitHub-markdown report to the build log.  Kept in its own file
# (rather than inline in the pipeline YAML) for readability.
#
# Environment (set by the pipeline step's 'env' mapping):
#   RESULTS_DIR   Directory the VM results were copied back to on the agent.
#
####################################################################################################
set -euo pipefail

resultsDir="${RESULTS_DIR:?RESULTS_DIR must be set}"

echo "=== Performance results ($resultsDir) ==="
if [ ! -d "$resultsDir" ]; then
    echo "##vso[task.logissue type=warning]No results directory was collected from the VM."
    exit 0
fi
find "$resultsDir" -type f | sort
echo
# Print every BenchmarkDotNet GitHub-markdown report that was produced.
while IFS= read -r report; do
    echo "----------------------------------------------------------------"
    echo "### $report"
    echo "----------------------------------------------------------------"
    cat "$report"
    echo
done < <(find "$resultsDir" -type f -name '*-report-github.md' | sort)
