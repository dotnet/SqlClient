#!/usr/bin/env bash
#################################################################################
# Licensed to the .NET Foundation under one or more agreements.                 #
# The .NET Foundation licenses this file to you under the MIT license.          #
# See the LICENSE file in the project root for more information.                #
#################################################################################
#
# close-backport-issue.sh
#
# Explicitly closes the issue(s) referenced by a pull request's closing
# keywords (Fixes/Closes/Resolves #N) after that pull request merges into a
# non-default branch (e.g. a release/X.Y branch).
#
# WHY THIS EXISTS
# ----------------
# GitHub only auto-closes an issue referenced by closing keywords when the
# referencing PR is merged into the repository's *default* branch. The
# cherry-pick PRs created by cherry-pick-to-release.sh always target a
# release/X.Y branch, never the default branch, so their "Fixes #<backport
# issue>" line (added by lookup_backport_issue() in that script) creates the
# cross-reference link but never actually closes the backport issue on
# merge. This script closes the gap: it re-derives the same closing
# references GitHub computed for the PR and closes each one explicitly.
#
# OVERVIEW
# --------
#   1. Ask the GitHub API which issues this PR's closing keywords reference.
#      (This list is populated regardless of the PR's base branch — only the
#      *automatic* close behavior is restricted to the default branch.)
#
#   2. For each such issue that is still open, close it with a comment
#      linking back to the merged PR.
#
# This is a no-op (and not an error) when the PR has no closing references,
# or when every referenced issue is already closed.
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

# -- Step 1: Find issues this PR's closing keywords reference -----------------
# closingIssuesReferences can include issues from other repositories (e.g.
# "Fixes owner/other#123"). Only consider references in this repository — a
# bare '.number' would otherwise let a cross-repo reference collide with an
# unrelated local issue of the same number and close it by mistake. 'gh
# ... --jq' takes a single query string (no jq '--arg' passthrough), so the
# repo is escaped and interpolated directly.
REPO_ESCAPED=$(printf '%s' "${GITHUB_REPOSITORY}" | sed 's/["\\]/\\&/g')

CLOSING_ISSUES=$(gh pr view "${PR_NUMBER}" --repo "${GITHUB_REPOSITORY}" \
  --json closingIssuesReferences \
  --jq ".closingIssuesReferences[] | select((.repository.owner.login + \"/\" + .repository.name) == \"${REPO_ESCAPED}\") | .number")

if [[ -z "${CLOSING_ISSUES}" ]]; then
  echo "No closing issue references found on #${PR_NUMBER}. Nothing to close."
  exit 0
fi

echo "Closing issue references: ${CLOSING_ISSUES}"

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
