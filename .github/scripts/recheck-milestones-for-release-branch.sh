#!/usr/bin/env bash
#################################################################################
# Licensed to the .NET Foundation under one or more agreements.                 #
# The .NET Foundation licenses this file to you under the MIT license.          #
# See the LICENSE file in the project root for more information.                #
#################################################################################
#
# recheck-milestones-for-release-branch.sh
#
# Re-runs the milestone check for open pull requests that are invalidated by the
# creation of a release branch.
#
# OVERVIEW
# --------
# check-milestone-branch.sh decides where a milestone belongs by asking whether
# release/<major>.<minor> exists. Cutting that branch therefore flips the
# expected target for every X.Y.* milestone from the default branch to the new
# release branch.
#
# Creating a branch emits no pull request activity, so an already-open PR keeps
# whatever result it last recorded. This script closes that gap: it finds the
# open PRs whose milestone now belongs to the new release branch and re-runs
# their most recent milestone check.
#
# Re-running is enough because check-milestone-branch.sh queries the live list
# of release branches. The replayed event payload still carries the correct
# milestone and base branch, since the check re-runs on every 'milestoned' and
# 'edited' activity, so the newest run always reflects the current PR state.
#
# REQUIRED ENVIRONMENT VARIABLES
# ------------------------------
#   RELEASE_BRANCH     The branch that was just created (e.g. "release/7.1").
#   DEFAULT_BRANCH     The repository's default branch (e.g. "main").
#   WORKFLOW_FILE      Workflow file name to re-run (e.g. "check-milestone.yml").
#   GITHUB_REPOSITORY  Owner/repo (e.g. "dotnet/SqlClient"). Set automatically by Actions.
#   GH_TOKEN           GitHub token for API calls. Needs 'actions: write'.
#
# OUTPUTS
# -------
#   Emits ::notice:: per PR re-run and ::warning:: for any PR that could not be
#   re-run. Exits 1 if at least one re-run failed, so the failure is visible in
#   the Actions UI; a maintainer can then re-run those checks by hand.
#
# USAGE
#   Called from the recheck-milestones.yml workflow. Can also be run locally:
#
#     export RELEASE_BRANCH="release/7.1"
#     export DEFAULT_BRANCH="main"
#     export WORKFLOW_FILE="check-milestone.yml"
#     export GITHUB_REPOSITORY="dotnet/SqlClient"
#     bash .github/scripts/recheck-milestones-for-release-branch.sh
#
#################################################################################
# 'set -e' is deliberately omitted: one PR failing to re-run must not abandon
# the rest. Failures are tracked explicitly and reported at the end.
set -uo pipefail

# -- Runtime help -------------------------------------------------------------
if [[ "${1:-}" == "--help" || "${1:-}" == "-h" ]]; then
  # Print the header comment block (between the license banner and the
  # closing banner), stripping the leading '# ' prefix.
  awk '/^#{2,}$/ { n++; next } n == 2 { sub(/^# ?/, ""); print }' "$0"
  exit 0
fi

# -- Input validation ---------------------------------------------------------
: "${RELEASE_BRANCH:?RELEASE_BRANCH environment variable is required}"
: "${DEFAULT_BRANCH:?DEFAULT_BRANCH environment variable is required}"
: "${WORKFLOW_FILE:?WORKFLOW_FILE environment variable is required}"
: "${GITHUB_REPOSITORY:?GITHUB_REPOSITORY environment variable is required}"

if [[ "${RELEASE_BRANCH}" =~ ^release/([0-9]+)\.([0-9]+)$ ]]; then
  MAJOR="${BASH_REMATCH[1]}"
  MINOR="${BASH_REMATCH[2]}"
else
  echo "::notice::'${RELEASE_BRANCH}' is not a 'release/<major>.<minor>' branch; nothing to reconcile."
  exit 0
fi

# -- Find the open PRs the new branch invalidates ------------------------------
if ! OPEN_PRS=$(gh pr list --repo "${GITHUB_REPOSITORY}" --base "${DEFAULT_BRANCH}" \
    --state open --limit 500 --json number,headRefOid,milestone \
    --jq '.[] | select(.milestone != null) | "\(.number) \(.headRefOid) \(.milestone.title)"' 2>&1); then
  echo "::error::Unable to list open pull requests for '${GITHUB_REPOSITORY}': ${OPEN_PRS}"
  exit 1
fi

FAILED=0
MATCHED=0

while read -r NUMBER HEAD_SHA MILESTONE_TITLE; do
  [[ -n "${NUMBER}" ]] || continue

  # Same milestone grammar as check-milestone-branch.sh.
  [[ "${MILESTONE_TITLE}" =~ ^([0-9]+)\.([0-9]+)\.[0-9]+([-+].*)?$ ]] || continue
  [[ "${BASH_REMATCH[1]}" == "${MAJOR}" && "${BASH_REMATCH[2]}" == "${MINOR}" ]] || continue

  MATCHED=$((MATCHED + 1))

  RUN_ID=$(gh api \
    "repos/${GITHUB_REPOSITORY}/actions/workflows/${WORKFLOW_FILE}/runs?head_sha=${HEAD_SHA}&per_page=1" \
    --jq '.workflow_runs[0].id // empty' 2>/dev/null)

  if [[ -z "${RUN_ID}" ]]; then
    echo "::warning::PR #${NUMBER} (milestone '${MILESTONE_TITLE}') has no milestone check run to re-run; re-check it manually."
    FAILED=$((FAILED + 1))
    continue
  fi

  if gh run rerun "${RUN_ID}" --repo "${GITHUB_REPOSITORY}" >/dev/null 2>&1; then
    echo "::notice::Re-ran the milestone check for PR #${NUMBER} (milestone '${MILESTONE_TITLE}', run ${RUN_ID})."
  else
    echo "::warning::Could not re-run the milestone check for PR #${NUMBER} (run ${RUN_ID}); re-check it manually."
    FAILED=$((FAILED + 1))
  fi
done <<< "${OPEN_PRS}"

if [[ "${MATCHED}" -eq 0 ]]; then
  echo "::notice::No open PR targeting '${DEFAULT_BRANCH}' carries a ${MAJOR}.${MINOR}.* milestone."
  exit 0
fi

if [[ "${FAILED}" -gt 0 ]]; then
  echo "::error::${FAILED} of ${MATCHED} affected pull requests could not be re-checked automatically."
  exit 1
fi

echo "::notice::Re-checked ${MATCHED} pull request(s) affected by '${RELEASE_BRANCH}'."
