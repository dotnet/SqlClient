#!/usr/bin/env bash
#################################################################################
# Licensed to the .NET Foundation under one or more agreements.                 #
# The .NET Foundation licenses this file to you under the MIT license.          #
# See the LICENSE file in the project root for more information.                #
#################################################################################
#
# close-backport-issue.sh
#
# Explicitly closes the backport issue(s) that a cherry-pick PR (created by
# cherry-pick-to-release.sh) is meant to close, after that PR merges into a
# non-default branch (e.g. a release/X.Y branch).
#
# WHY THIS EXISTS
# ----------------
# GitHub only auto-closes an issue referenced by closing keywords (Fixes,
# Closes, Resolves) when the referencing PR merges into the repository's
# *default* branch — and for PRs targeting any other branch, it doesn't even
# populate 'closingIssuesReferences' for keyword references (only issues
# manually linked via the PR's "Development" sidebar show up there). The
# cherry-pick PRs created by cherry-pick-to-release.sh always target a
# release/X.Y branch, so their "Fixes #<backport issue>" text is purely
# cosmetic and gives the GitHub API nothing to report. This script closes the
# gap by reading the "<!-- backport-issue-numbers: ... -->" marker that
# lookup_backport_issue() (in cherry-pick-to-release.sh) embeds in the PR
# body, and closing each issue named there explicitly. Using an explicit
# marker (rather than any closing keyword found in the PR body) also means
# this only ever closes issues our own automation created and recorded, never
# an issue a human happens to reference with "Fixes #N" in an unrelated,
# manually-authored release-branch PR.
#
# OVERVIEW
# --------
#   1. Read the merged PR's body and extract the issue numbers recorded in
#      its "<!-- backport-issue-numbers: ... -->" marker, if any.
#
#   2. For each such issue that is still open, close it with a comment
#      linking back to the merged PR.
#
# This is a no-op (and not an error) when the PR has no marker, or when every
# referenced issue is already closed.
#
# REQUIRED ENVIRONMENT VARIABLES
# ------------------------------
#   PR_NUMBER          The pull request number that just merged.
#   GH_TOKEN            GitHub token for 'gh' CLI authentication.
#   GITHUB_REPOSITORY   Owner/repo (e.g. "dotnet/SqlClient"). Set by Actions.
#
# OUTPUTS
# -------
#   Closes zero or more issues in this repository.
#
# USAGE
#   Called from the close-backport-issue.yml workflow. Can also be run
#   locally:
#
#     export PR_NUMBER=4750
#     export GH_TOKEN="ghp_..."
#     export GITHUB_REPOSITORY="dotnet/SqlClient"
#     bash .github/scripts/close-backport-issue.sh
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

# -- Step 1: Extract backport issue numbers from the PR body's marker --------
PR_BODY=$(gh pr view "${PR_NUMBER}" --repo "${GITHUB_REPOSITORY}" \
  --json body --jq '.body')

# The marker line looks like: <!-- backport-issue-numbers: 123 456 -->
MARKER_LINE=$(grep -o '<!-- *backport-issue-numbers:[0-9 ]*-->' <<< "${PR_BODY}" || true)

if [[ -z "${MARKER_LINE}" ]]; then
  echo "No backport-issue-numbers marker found on #${PR_NUMBER}. Nothing to close."
  exit 0
fi

CLOSING_ISSUES=$(grep -o '[0-9]\+' <<< "${MARKER_LINE}")

echo "Backport issue numbers: ${CLOSING_ISSUES}"

# -- Step 2: Close each referenced issue that is still open -------------------
CLOSED_ANY=0
while IFS= read -r issue_number; do
  [[ -z "${issue_number}" ]] && continue

  # A real API/permission failure here must not be swallowed and mistaken for
  # "issue doesn't exist" — surface it loudly instead of silently skipping.
  if ! issue_state=$(gh issue view "${issue_number}" --repo "${GITHUB_REPOSITORY}" \
    --json state --jq '.state'); then
    echo "::error::Failed to look up state for issue #${issue_number}." >&2
    exit 1
  fi

  if [[ "${issue_state}" != "OPEN" ]]; then
    echo "Issue #${issue_number} is already $(tr '[:upper:]' '[:lower:]' <<< "${issue_state}"). Skipping."
    continue
  fi

  echo "Closing issue #${issue_number} (referenced by merged PR #${PR_NUMBER})."
  gh issue close "${issue_number}" --repo "${GITHUB_REPOSITORY}" \
    --reason completed \
    --comment "Closed by #${PR_NUMBER}, which merged into a non-default branch. GitHub only auto-closes issues for PRs merged into the default branch, so this was closed explicitly."
  CLOSED_ANY=1
done <<< "${CLOSING_ISSUES}"

if [[ "${CLOSED_ANY}" -eq 0 ]]; then
  echo "All referenced issues were already closed."
fi
