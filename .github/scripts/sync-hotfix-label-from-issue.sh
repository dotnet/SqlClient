#!/usr/bin/env bash
#################################################################################
# Licensed to the .NET Foundation under one or more agreements.                 #
# The .NET Foundation licenses this file to you under the MIT license.          #
# See the LICENSE file in the project root for more information.                #
#################################################################################
#
# sync-hotfix-label-from-issue.sh
#
# Companion to sync-hotfix-label-to-pr.sh, covering the timing gap it misses:
# sync-hotfix-label-to-pr.sh only runs on PR events (opened/reopened/edited/
# synchronize), so if an issue is labeled "Hotfix X.Y.Z" *after* its closing
# PR is already open, and that PR never receives another qualifying event,
# the label never gets copied over and cherry-pick-hotfix.yml never fires on
# merge. This script runs the sync from the other direction: given an issue
# that was just labeled, find the open PR(s) that will close it and re-run
# sync-hotfix-label-to-pr.sh for each.
#
# OVERVIEW
# --------
#   1. Ask the GitHub API which open pull request(s) will close this issue
#      (via the 'closedByPullRequestsReferences' connection).
#
#   2. For each such PR, invoke sync-hotfix-label-to-pr.sh with PR_NUMBER set,
#      which does the actual label-copying work.
#
# This is idempotent and safe to re-run on every issue 'labeled' event: a PR
# that already has the label is left alone (handled by the delegated script),
# and an issue with no closing PR is a silent no-op.
#
# REQUIRED ENVIRONMENT VARIABLES
# ------------------------------
#   ISSUE_NUMBER        The issue number that was just labeled.
#   GH_TOKEN             GitHub token for 'gh' CLI authentication.
#   GITHUB_REPOSITORY    Owner/repo (e.g. "dotnet/SqlClient"). Set by Actions.
#
# OUTPUTS
# -------
#   Adds zero or more "Hotfix X.Y.Z" labels to pull request(s) that close
#   this issue.
#
# USAGE
#   Called from the sync-hotfix-label-to-pr.yml workflow. Can also be run
#   locally:
#
#     export ISSUE_NUMBER=4715
#     export GH_TOKEN="ghp_..."
#     export GITHUB_REPOSITORY="dotnet/SqlClient"
#     bash .github/scripts/sync-hotfix-label-from-issue.sh
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
: "${ISSUE_NUMBER:?ISSUE_NUMBER environment variable is required}"
: "${GITHUB_REPOSITORY:?GITHUB_REPOSITORY environment variable is required}"

echo "Issue: #${ISSUE_NUMBER}"

REPO_OWNER="${GITHUB_REPOSITORY%%/*}"
REPO_NAME="${GITHUB_REPOSITORY#*/}"

# -- Step 1: Find open PR(s) that will close this issue -----------------------
# 'closedByPullRequestsReferences' is the reverse of 'closingIssuesReferences'
# on a PR: it lists the PR(s) whose closing keywords reference this issue.
# Unlike closingIssuesReferences on a *release-branch* PR, this is reliable
# here because the referencing PR (the one fixing the original issue) always
# targets the default branch.
CLOSING_PRS=$(gh api graphql \
  -f query='
    query($owner: String!, $repo: String!, $issueNumber: Int!) {
      repository(owner: $owner, name: $repo) {
        issue(number: $issueNumber) {
          closedByPullRequestsReferences(first: 50) {
            nodes { number state }
          }
        }
      }
    }' \
  -F owner="${REPO_OWNER}" \
  -F repo="${REPO_NAME}" \
  -F issueNumber="${ISSUE_NUMBER}" \
  --jq '.data.repository.issue.closedByPullRequestsReferences.nodes[] | select(.state == "OPEN") | .number')

if [[ -z "${CLOSING_PRS}" ]]; then
  echo "No open pull requests found that close #${ISSUE_NUMBER}. Nothing to sync."
  exit 0
fi

echo "Pull requests referencing #${ISSUE_NUMBER}: ${CLOSING_PRS}"

# -- Step 2: Re-run the PR-side sync for each referencing PR ------------------
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

while IFS= read -r pr_number; do
  [[ -z "${pr_number}" ]] && continue
  echo "Syncing Hotfix labels onto PR #${pr_number} (closes #${ISSUE_NUMBER})."
  PR_NUMBER="${pr_number}" bash "${SCRIPT_DIR}/sync-hotfix-label-to-pr.sh"
done <<< "${CLOSING_PRS}"
