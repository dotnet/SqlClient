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
# This script handles two distinct trigger scenarios:
#
#   1. 'closed' event  — The PR was just merged. ALL "Hotfix X.Y.Z" labels on
#      the PR are processed, emitting one version per valid label.
#
#   2. 'labeled' event — A label was added to an already-merged PR. Only the
#      NEWLY ADDED label is considered, and only if a cherry-pick for that
#      version hasn't already been created (branch or PR exists).
#
# Label names must match the exact pattern "Hotfix <major>.<minor>.<patch>"
# (e.g. "Hotfix 7.0.1"). All other labels are silently ignored.
#
# REQUIRED ENVIRONMENT VARIABLES
# ------------------------------
#   LABELS        Comma-separated list of all label names on the PR.
#   EVENT_ACTION  The GitHub event action: "closed" or "labeled".
#   EVENT_LABEL   For 'labeled' events, the name of the label that was added.
#                 Empty or unset for 'closed' events.
#   PR_NUMBER     The pull request number (used to derive cherry-pick branch names).
#   GH_TOKEN      GitHub token for API calls (gh CLI auth).
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
  cat <<'EOF'
Usage: extract-hotfix-versions.sh

Parses "Hotfix X.Y.Z" labels from a GitHub PR and emits a JSON array for
matrix fan-out.

Required environment variables:
  LABELS         Comma-separated label names on the PR
  EVENT_ACTION   "closed" or "labeled"
  EVENT_LABEL    The label just added (labeled events only)
  PR_NUMBER      PR number
  GH_TOKEN       GitHub token for gh CLI

Output (written to $GITHUB_OUTPUT):
  versions=["7.0.1","8.0.0"]

Exit codes:
  0  Success (including "nothing to do" — versions=[])
  1  Error (e.g. no valid Hotfix labels on a 'closed' event)
EOF
  exit 0
fi

# -- Input validation ---------------------------------------------------------
: "${LABELS:?LABELS environment variable is required}"
: "${EVENT_ACTION:?EVENT_ACTION environment variable is required}"
: "${PR_NUMBER:?PR_NUMBER environment variable is required}"

# -- 'labeled' event: process only the newly added label ----------------------
if [[ "${EVENT_ACTION}" == "labeled" ]]; then
  # Extract version from the new label. If it doesn't match "Hotfix X.Y.Z",
  # this is a non-hotfix label — emit empty matrix and exit cleanly.
  CANDIDATE=$(echo "${EVENT_LABEL:-}" \
    | sed -n 's/^Hotfix \([0-9]\+\.[0-9]\+\.[0-9]\+\)$/\1/p')

  if [[ -z "${CANDIDATE}" ]]; then
    echo "Label '${EVENT_LABEL:-}' is not a valid 'Hotfix X.Y.Z' label. Skipping."
    echo "versions=[]" >> "${GITHUB_OUTPUT}"
    exit 0
  fi

  # Guard against duplicate cherry-picks.  If the cherry-pick branch already
  # exists on the remote, or a PR (open, closed, or merged) was already created
  # from it, there is nothing left to do.
  #
  # NOTE: We use the GitHub API rather than 'git ls-remote' because the
  # detect-versions job does not check out the repository (no .git directory).
  CHERRY_PICK_BRANCH="dev/automation/pr-${PR_NUMBER}-to-${CANDIDATE}"

  if gh api "repos/${GITHUB_REPOSITORY}/git/ref/heads/${CHERRY_PICK_BRANCH}" \
      --silent 2>/dev/null; then
    echo "Cherry-pick branch '${CHERRY_PICK_BRANCH}' already exists. Skipping."
    echo "versions=[]" >> "${GITHUB_OUTPUT}"
    exit 0
  fi

  EXISTING_PR=$(gh pr list --head "${CHERRY_PICK_BRANCH}" --state all \
    --json number --jq 'length')
  if [[ "${EXISTING_PR}" -gt 0 ]]; then
    echo "A cherry-pick PR from '${CHERRY_PICK_BRANCH}' already exists. Skipping."
    echo "versions=[]" >> "${GITHUB_OUTPUT}"
    exit 0
  fi

  VERSIONS="${CANDIDATE}"
else
  # -- 'closed' event: process all hotfix labels on the PR --------------------
  # Split by comma, keep only labels matching "Hotfix X.Y.Z", extract the version.
  VERSIONS=$(echo "${LABELS}" | tr ',' '\n' \
    | sed -n 's/^Hotfix \([0-9]\+\.[0-9]\+\.[0-9]\+\)$/\1/p')
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
