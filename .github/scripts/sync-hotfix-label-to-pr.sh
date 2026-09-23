#!/usr/bin/env bash
#################################################################################
# Licensed to the .NET Foundation under one or more agreements.                 #
# The .NET Foundation licenses this file to you under the MIT license.          #
# See the LICENSE file in the project root for more information.                #
#################################################################################
#
# sync-hotfix-label-to-pr.sh
#
# Copies "Hotfix X.Y.Z" labels from an issue onto any pull request that will
# close it, so the existing cherry-pick-hotfix.yml automation (which reacts to
# "Hotfix X.Y.Z" labels on merged PRs) fires without the label having to be
# applied to the PR by hand.
#
# OVERVIEW
# --------
#   1. Ask the GitHub API which issues this PR's closing keywords (Fixes,
#      Closes, Resolves, etc.) will close when it's merged.
#
#   2. For each such issue, collect any "Hotfix X.Y.Z" labels it carries.
#
#   3. Add any of those labels to the PR that it doesn't already have.
#
# This is idempotent and safe to re-run on every 'opened'/'edited'/'reopened'/
# 'synchronize' event: labels already present are left alone, and a PR with no
# closing references or no Hotfix-labeled referenced issues is a silent no-op.
#
# REQUIRED ENVIRONMENT VARIABLES
# ------------------------------
#   PR_NUMBER          The pull request number.
#   GH_TOKEN            GitHub token for 'gh' CLI authentication.
#   GITHUB_REPOSITORY   Owner/repo (e.g. "dotnet/SqlClient"). Set by Actions.
#
# OUTPUTS
# -------
#   Adds zero or more "Hotfix X.Y.Z" labels to the pull request.
#
# USAGE
#   Called from the sync-hotfix-label-to-pr.yml workflow. Can also be run
#   locally:
#
#     export PR_NUMBER=4750
#     export GH_TOKEN="ghp_..."
#     export GITHUB_REPOSITORY="dotnet/SqlClient"
#     bash .github/scripts/sync-hotfix-label-to-pr.sh
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
: "${PR_NUMBER:?PR_NUMBER environment variable is required}"
: "${GITHUB_REPOSITORY:?GITHUB_REPOSITORY environment variable is required}"

echo "Pull request: #${PR_NUMBER}"

# -- Step 1: Find issues this PR will close -----------------------------------
CLOSING_ISSUES=$(gh pr view "${PR_NUMBER}" --repo "${GITHUB_REPOSITORY}" \
  --json closingIssuesReferences --jq '.closingIssuesReferences[].number')

if [[ -z "${CLOSING_ISSUES}" ]]; then
  echo "No closing issue references found on #${PR_NUMBER}. Nothing to sync."
  exit 0
fi

echo "Closing issue references: ${CLOSING_ISSUES}"

# -- Step 2: Collect "Hotfix X.Y.Z" labels from each referenced issue ---------
HOTFIX_LABELS=""
while IFS= read -r issue_number; do
  [[ -z "${issue_number}" ]] && continue

  issue_labels=$(gh issue view "${issue_number}" --repo "${GITHUB_REPOSITORY}" \
    --json labels --jq '.labels[].name | select(startswith("Hotfix "))')

  while IFS= read -r label; do
    [[ -z "${label}" ]] && continue
    if ! grep -qxF "${label}" <<< "${HOTFIX_LABELS}"; then
      HOTFIX_LABELS+="${label}"$'\n'
    fi
  done <<< "${issue_labels}"
done <<< "${CLOSING_ISSUES}"

if [[ -z "${HOTFIX_LABELS}" ]]; then
  echo "No 'Hotfix X.Y.Z' labels found on referenced issues. Nothing to sync."
  exit 0
fi

# -- Step 3: Add any missing labels to the PR ---------------------------------
EXISTING_PR_LABELS=$(gh pr view "${PR_NUMBER}" --repo "${GITHUB_REPOSITORY}" \
  --json labels --jq '.labels[].name')

ADDED_ANY=0
while IFS= read -r label; do
  [[ -z "${label}" ]] && continue
  if grep -qxF "${label}" <<< "${EXISTING_PR_LABELS}"; then
    echo "PR #${PR_NUMBER} already has label '${label}'. Skipping."
    continue
  fi

  echo "Adding label '${label}' to #${PR_NUMBER} (from issue Hotfix label)."
  gh pr edit "${PR_NUMBER}" --repo "${GITHUB_REPOSITORY}" --add-label "${label}"
  ADDED_ANY=1
done <<< "${HOTFIX_LABELS}"

if [[ "${ADDED_ANY}" -eq 0 ]]; then
  echo "All Hotfix labels from referenced issues are already present on #${PR_NUMBER}."
fi
