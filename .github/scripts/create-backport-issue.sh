#!/usr/bin/env bash
#################################################################################
# Licensed to the .NET Foundation under one or more agreements.                 #
# The .NET Foundation licenses this file to you under the MIT license.          #
# See the LICENSE file in the project root for more information.                #
#################################################################################
#
# create-backport-issue.sh
#
# Creates a child "backport issue" targeting a hotfix milestone when a
# "Hotfix X.Y.Z" label is added to a parent issue, and links it as a native
# GitHub sub-issue of the parent.
#
# OVERVIEW
# --------
# This mirrors, on the issue side, what extract-hotfix-versions.sh /
# cherry-pick-to-release.sh already do on the PR side:
#
#   1. Validate that the newly added label matches "Hotfix X.Y.Z". Any other
#      label is ignored.
#
#   2. Guard against duplicates: if the parent issue already has a sub-issue
#      titled with the "[X.Y.Z] " prefix, do nothing (handles labels being
#      removed/re-added, or the workflow re-running). Milestone is
#      deliberately not part of this match; see Step 2 below for why.
#
#   3. Look up the X.Y.Z milestone (best-effort, like cherry-pick-to-release.sh's
#      milestone lookup — if it doesn't exist yet, the child issue is created
#      without one and a note is appended to its body).
#
#   4. Create the child issue titled "[X.Y.Z] <parent title>", body
#      referencing the parent, carrying the parent's labels minus any
#      "Hotfix *" labels (those stay on the parent only, so this workflow
#      doesn't recurse on the child).
#
#   5. Link the child issue to the parent as a native GitHub sub-issue via the
#      REST "add sub-issue" endpoint (same relationship used for #4737 under
#      #4714).
#
# REQUIRED ENVIRONMENT VARIABLES
# ------------------------------
#   EVENT_LABEL        The label that was just added (e.g. "Hotfix 7.1.1").
#   PARENT_ISSUE_NUMBER  The parent issue's number.
#   GH_TOKEN            GitHub token for 'gh' CLI authentication.
#   GITHUB_REPOSITORY   Owner/repo (e.g. "dotnet/SqlClient"). Set by Actions.
#
# OUTPUTS
# -------
#   Creates a new issue and links it as a sub-issue of the parent, unless the
#   label doesn't match "Hotfix X.Y.Z" or a matching backport issue already
#   exists, in which case the script exits 0 having done nothing.
#
# USAGE
#   Called from the hotfix-label-issue.yml workflow. Can also be run locally:
#
#     export EVENT_LABEL="Hotfix 7.1.1"
#     export PARENT_ISSUE_NUMBER=4714
#     export GH_TOKEN="ghp_..."
#     export GITHUB_REPOSITORY="dotnet/SqlClient"
#     bash .github/scripts/create-backport-issue.sh
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
: "${EVENT_LABEL:?EVENT_LABEL environment variable is required}"
: "${PARENT_ISSUE_NUMBER:?PARENT_ISSUE_NUMBER environment variable is required}"
: "${GITHUB_REPOSITORY:?GITHUB_REPOSITORY environment variable is required}"

# -- Step 1: Validate the label matches "Hotfix X.Y.Z" ------------------------
if [[ "${EVENT_LABEL}" =~ ^Hotfix\ ([0-9]+\.[0-9]+\.[0-9]+)$ ]]; then
  VERSION="${BASH_REMATCH[1]}"
else
  echo "Label '${EVENT_LABEL}' is not a valid 'Hotfix X.Y.Z' label. Skipping."
  exit 0
fi

CHILD_TITLE_PREFIX="[${VERSION}]"

echo "Parent issue:  #${PARENT_ISSUE_NUMBER}"
echo "Hotfix label:  ${EVENT_LABEL}"
echo "Version:       ${VERSION}"

# -- Step 2: Guard against duplicates -----------------------------------------
# Look at the parent's existing sub-issues. If one already carries the child
# title prefix for VERSION, a backport issue for this version already
# exists — nothing to do. Matching is on title prefix only, never milestone
# alone (see the loop below for why).
#
# NOTE: the milestone column uses "NONE" rather than "" for issues without a
# milestone. Tab is IFS whitespace, so 'read' silently collapses/skips a
# leading empty field, which would otherwise shift every column left.
#
# A failed lookup (network/permission error) must NOT be treated the same as
# "no sub-issues exist" — that would defeat the duplicate guard and create a
# second backport issue on a rerun. Do not suppress stderr here so a real
# failure is both visible in the log and distinguishable (via exit code) from
# a legitimately empty (but successful) call.
if ! EXISTING_SUB_ISSUES=$(gh api "repos/${GITHUB_REPOSITORY}/issues/${PARENT_ISSUE_NUMBER}/sub_issues" \
  --paginate --jq '.[] | [(.milestone.title // "NONE"), .title, (.number|tostring)] | @tsv'); then
  echo "::error::Failed to look up existing sub-issues for #${PARENT_ISSUE_NUMBER};" \
       "cannot verify a backport issue doesn't already exist. Aborting to avoid creating a duplicate." >&2
  exit 1
fi

if [[ -n "${EXISTING_SUB_ISSUES}" ]]; then
  while IFS=$'\t' read -r sub_milestone sub_title sub_number; do
    # Match on the deterministic "[VERSION] " title prefix only — matching on
    # milestone alone would also catch an unrelated sub-issue that just
    # happens to be milestoned to the same release (e.g. a separate follow-up
    # task), causing this guard to wrongly skip creating the real backport
    # issue. The title prefix alone is sufficient: it's set unconditionally
    # in Step 5 below regardless of whether the milestone was found yet, so
    # it still detects a previously-created (possibly milestone-less)
    # backport issue on a rerun.
    if [[ "${sub_title}" == "${CHILD_TITLE_PREFIX}"* ]]; then
      echo "::notice::Backport issue #${sub_number} for '${VERSION}' already exists under parent #${PARENT_ISSUE_NUMBER}" \
           "(milestone: ${sub_milestone}). Skipping."
      exit 0
    fi
  done <<< "${EXISTING_SUB_ISSUES}"
fi



# -- Step 3: Look up the parent issue's title/labels --------------------------
PARENT_JSON=$(gh issue view "${PARENT_ISSUE_NUMBER}" --repo "${GITHUB_REPOSITORY}" \
  --json title,labels)
PARENT_TITLE=$(jq -r '.title' <<< "${PARENT_JSON}")

# Copy the parent's labels onto the child, excluding any "Hotfix *" labels —
# those are only meaningful (and only drive automation) on the parent issue.
CHILD_LABELS=$(jq -r '[.labels[].name | select(startswith("Hotfix ") | not)] | join(",")' \
  <<< "${PARENT_JSON}")

# -- Step 4: Look up the milestone (best-effort) ------------------------------
MILESTONE_FOUND=""
MILESTONE_NOTE=""
if gh api "repos/${GITHUB_REPOSITORY}/milestones" --method GET --paginate \
    --field state=open --jq '.[].title' | grep -qx "${VERSION}"; then
  MILESTONE_FOUND="${VERSION}"
  echo "Milestone '${VERSION}' found."
else
  echo "::warning::Milestone '${VERSION}' does not exist." \
       "Backport issue will be created without a milestone."
  MILESTONE_NOTE=$'\n\n> **Note:** Milestone `'"${VERSION}"'` does not exist yet. Please create it and assign this issue manually.'
fi

# -- Step 5: Create the child issue -------------------------------------------
CHILD_BODY="Backport of #${PARENT_ISSUE_NUMBER} for the \`${VERSION}\` hotfix.${MILESTONE_NOTE}"

# Built as an array (not an unquoted string) because label names in this repo
# can contain spaces (e.g. "Regression :boom:"), which would otherwise be
# word-split incorrectly. The "${ARGS[@]+...}" guard keeps this portable to
# bash 3.2 (macOS default), where referencing an empty array under 'set -u'
# is treated as an unbound variable.
ARGS=()
if [[ -n "${CHILD_LABELS}" ]]; then
  ARGS+=(--label "${CHILD_LABELS}")
fi
if [[ -n "${MILESTONE_FOUND}" ]]; then
  ARGS+=(--milestone "${MILESTONE_FOUND}")
fi

CHILD_URL=$(gh issue create \
  --repo "${GITHUB_REPOSITORY}" \
  --title "${CHILD_TITLE_PREFIX} ${PARENT_TITLE}" \
  --body "${CHILD_BODY}" \
  "${ARGS[@]+"${ARGS[@]}"}")

CHILD_NUMBER="${CHILD_URL##*/}"
echo "Created backport issue #${CHILD_NUMBER}: ${CHILD_URL}"

# -- Step 6: Link the child as a native GitHub sub-issue of the parent --------
# The sub-issues REST API takes the child issue's numeric database *id* (not
# its user-facing number, and not its node_id), so resolve it first.
CHILD_ID=$(gh api "repos/${GITHUB_REPOSITORY}/issues/${CHILD_NUMBER}" --jq '.id')

gh api "repos/${GITHUB_REPOSITORY}/issues/${PARENT_ISSUE_NUMBER}/sub_issues" \
  --method POST \
  --header "Accept: application/vnd.github+json" \
  -F "sub_issue_id=${CHILD_ID}" \
  >/dev/null

echo "Linked #${CHILD_NUMBER} as a sub-issue of #${PARENT_ISSUE_NUMBER}."
