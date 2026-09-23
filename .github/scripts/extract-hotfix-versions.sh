#!/usr/bin/env bash
#################################################################################
# Licensed to the .NET Foundation under one or more agreements.                 #
# The .NET Foundation licenses this file to you under the MIT license.          #
# See the LICENSE file in the project root for more information.                #
#################################################################################
#
# extract-hotfix-versions.sh
#
# Parses "Hotfix X.Y.Z" labels from a merged GitHub PR and emits a JSON array
# of version strings suitable for use as a GitHub Actions matrix dimension.
#
# OVERVIEW
# --------
# This script handles three distinct trigger scenarios:
#
#   1. 'closed' event     — The PR was just merged. ALL "Hotfix X.Y.Z" labels
#      on the PR are processed, emitting one version per valid label. No
#      duplicate check is needed: the PR has never been processed before.
#
#   2. 'labeled' event     — A label was added to an already-merged PR (the
#      real GitHub webhook fired). Only the NEWLY ADDED label is considered,
#      and only if a cherry-pick for that version hasn't already been created
#      (branch or PR exists).
#
#   3. 'reconcile' event   — Used for a workflow_dispatch re-run (see
#      cherry-pick-hotfix.yml): sync-hotfix-label-from-issue.sh adds a
#      "Hotfix X.Y.Z" label to an already-merged PR using GITHUB_TOKEN, which
#      does not fire a new "labeled" webhook event (GitHub does not trigger
#      further workflow runs for events caused by the repository's own
#      GITHUB_TOKEN), so nothing would otherwise process that label. This mode
#      re-derives ALL "Hotfix X.Y.Z" labels currently on the PR — like
#      'closed' — but, like 'labeled', skips any version that already has a
#      cherry-pick branch or PR, since some of the PR's other labels may have
#      already been processed by an earlier event.
#
# Label names must match the exact pattern "Hotfix <major>.<minor>.<patch>"
# (e.g. "Hotfix 7.0.1"). All other labels are silently ignored.
#
# REQUIRED ENVIRONMENT VARIABLES
# ------------------------------
#   LABELS             Comma-separated list of all label names on the PR.
#   EVENT_ACTION       The GitHub event action: "closed", "labeled", or
#                      "reconcile".
#   EVENT_LABEL        For 'labeled' events, the name of the label that was added.
#                      Empty or unset for 'closed'/'reconcile' events.
#   PR_NUMBER          The pull request number (used to derive cherry-pick branch names).
#   GH_TOKEN           GitHub token for API calls (gh CLI auth).
#   GITHUB_REPOSITORY  Owner/repo (e.g. "dotnet/SqlClient"). Set automatically by Actions.
#
# OUTPUTS
# -------
#   Writes to $GITHUB_OUTPUT:
#     versions=<JSON array>   e.g. versions=["7.0.1","8.0.0"]
#
#   An empty array (versions=[]) means no work is needed.
#   The script exits with code 1 if the 'closed' event has no valid labels.
#
# USAGE
#   Called from the cherry-pick-hotfix.yml workflow. Can also be run locally
#   for testing by setting the required environment variables and providing a
#   writable GITHUB_OUTPUT file:
#
#     export LABELS="Hotfix 7.0.1,bug"
#     export EVENT_ACTION="closed"
#     export PR_NUMBER=42
#     export GITHUB_OUTPUT=$(mktemp)
#     bash .github/scripts/extract-hotfix-versions.sh
#     cat "$GITHUB_OUTPUT"
#
#################################################################################
set -euo pipefail

# -- Runtime help -------------------------------------------------------------
if [[ "${1:-}" == "--help" || "${1:-}" == "-h" ]]; then
  # Print the header comment block (between the license banner and the
  # closing banner), stripping the leading '# ' prefix.
  awk '/^#{2,}$/ { n++; next } n == 2 { sub(/^# ?/, ""); print }' "$0"
  exit 0
fi

# -- Input validation ---------------------------------------------------------
: "${LABELS:?LABELS environment variable is required}"
: "${EVENT_ACTION:?EVENT_ACTION environment variable is required}"
: "${PR_NUMBER:?PR_NUMBER environment variable is required}"
: "${GITHUB_REPOSITORY:?GITHUB_REPOSITORY environment variable is required}"

# -- Shared helper: has a cherry-pick for VERSION already been created? ------
# Used by both the 'labeled' (single candidate) and 'reconcile' (every
# current Hotfix label) paths below.
#
# NOTE: We use the GitHub API rather than 'git ls-remote' because the
# detect-versions job does not check out the repository (no .git directory).
cherry_pick_already_exists() {
  local version="$1"
  local branch="dev/automation/pr-${PR_NUMBER}-to-${version}"

  if gh api "repos/${GITHUB_REPOSITORY}/git/ref/heads/${branch}" --silent 2>/dev/null; then
    echo "Cherry-pick branch '${branch}' already exists. Skipping."
    return 0
  fi

  local existing_pr
  existing_pr=$(gh pr list --repo "${GITHUB_REPOSITORY}" --head "${branch}" --state all \
    --json number --jq 'length')
  if [[ "${existing_pr}" -gt 0 ]]; then
    echo "A cherry-pick PR from '${branch}' already exists. Skipping."
    return 0
  fi

  return 1
}

if [[ "${EVENT_ACTION}" == "labeled" ]]; then
  # -- 'labeled' event: process only the newly added label --------------------
  # Extract version from the new label. If it doesn't match "Hotfix X.Y.Z",
  # this is a non-hotfix label — emit empty matrix and exit cleanly.
  if [[ "${EVENT_LABEL:-}" =~ ^Hotfix\ ([0-9]+\.[0-9]+\.[0-9]+)$ ]]; then
    CANDIDATE="${BASH_REMATCH[1]}"
  else
    CANDIDATE=""
  fi

  if [[ -z "${CANDIDATE}" ]]; then
    echo "Label '${EVENT_LABEL:-}' is not a valid 'Hotfix X.Y.Z' label. Skipping."
    echo "versions=[]" >> "${GITHUB_OUTPUT}"
    exit 0
  fi

  if cherry_pick_already_exists "${CANDIDATE}"; then
    echo "versions=[]" >> "${GITHUB_OUTPUT}"
    exit 0
  fi

  VERSIONS="${CANDIDATE}"
elif [[ "${EVENT_ACTION}" == "reconcile" ]]; then
  # -- 'reconcile' event: re-derive all current Hotfix labels, but still skip
  # any version that's already been cherry-picked (see OVERVIEW above).
  ALL_VERSIONS=$(echo "${LABELS}" | tr ',' '\n' \
    | sed -nE 's/^Hotfix ([0-9]+\.[0-9]+\.[0-9]+)$/\1/p')

  # No valid "Hotfix X.Y.Z" label at all is the same caller error as the
  # 'closed' event hitting the check below — fall through to it.
  if [[ -n "${ALL_VERSIONS}" ]]; then
    VERSIONS=""
    while IFS= read -r candidate; do
      [[ -z "${candidate}" ]] && continue
      if cherry_pick_already_exists "${candidate}"; then
        continue
      fi
      VERSIONS+="${candidate}"$'\n'
    done <<< "${ALL_VERSIONS}"
    VERSIONS="${VERSIONS%$'\n'}"

    # Every valid label already had a cherry-pick (all duplicates): this is a
    # legitimate no-op, unlike "no valid label found" below, so exit early
    # rather than falling into the shared error check.
    if [[ -z "${VERSIONS}" ]]; then
      echo "versions=[]" >> "${GITHUB_OUTPUT}"
      exit 0
    fi
  else
    VERSIONS=""
  fi
else
  # -- 'closed' event: process all hotfix labels on the PR --------------------
  # Split by comma, keep only labels matching "Hotfix X.Y.Z", extract the version.
  # Use sed -E for portable extended regex (works on both GNU and BSD sed).
  VERSIONS=$(echo "${LABELS}" | tr ',' '\n' \
    | sed -nE 's/^Hotfix ([0-9]+\.[0-9]+\.[0-9]+)$/\1/p')
fi

# -- Validate that at least one version was found ----------------------------
if [[ -z "${VERSIONS}" ]]; then
  echo "::error::No valid 'Hotfix X.Y.Z' label found. " \
       "Labels must match 'Hotfix <major>.<minor>.<patch>'."
  exit 1
fi

# -- Emit JSON array for the matrix strategy ----------------------------------
# Convert the newline-separated version list into a compact JSON array.
# e.g. "7.0.1\n8.0.0" → ["7.0.1","8.0.0"]
JSON=$(echo "${VERSIONS}" \
  | jq -R -s -c 'split("\n") | map(select(length > 0))')

echo "versions=${JSON}" >> "${GITHUB_OUTPUT}"
echo "Detected hotfix versions: ${JSON}"
