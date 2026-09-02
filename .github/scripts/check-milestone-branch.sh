#!/usr/bin/env bash
#################################################################################
# Licensed to the .NET Foundation under one or more agreements.                 #
# The .NET Foundation licenses this file to you under the MIT license.          #
# See the LICENSE file in the project root for more information.                #
#################################################################################
#
# check-milestone-branch.sh
#
# Validates that a pull request's milestone is consistent with the branch the
# pull request targets.
#
# OVERVIEW
# --------
# Milestones in this repository are named "<major>.<minor>.<patch>", optionally
# with a pre-release suffix (e.g. "7.0.3", "8.0.0-preview1"). Every milestone
# therefore maps to a candidate release branch:
#
#     <major>.<minor>.<patch>  ->  release/<major>.<minor>
#
# Release branches and open milestones determine where the work belongs:
#
#   * The branch EXISTS  -> that version has already forked off the default
#     branch and is in servicing. Changes for it go to release/<major>.<minor>.
#
#   * The branch DOES NOT exist, and this is the earliest open milestone series
#     without a release branch -> that version is in development on the default
#     branch. Changes for it go to the default branch.
#
#   * An earlier open milestone series has no release branch -> the requested
#     version is not active yet and cannot target the default branch.
#
# This rule is self-maintaining: no hard-coded version list needs updating when
# a new release branch is cut.
#
# VALIDATION MATRIX
# -----------------
#   Target branch            Release branch exists?   Result
#   -----------------------  -----------------------  ----------------------
#   release/<major>.<minor>  n/a (it is the target)   pass
#   another release/*        n/a                      fail (mismatch)
#   default branch           no, earliest open line   pass
#   default branch           no, later open line      fail (not active yet)
#   default branch           yes                      fail (needs servicing branch)
#   anything else            n/a                      skipped (integration branch)
#
# Pull requests into long-lived integration branches (e.g. "dev/paul/foo") are
# skipped, because the milestone is enforced when that branch is merged into
# the default branch or a release branch.
#
# Milestones that don't parse as "<major>.<minor>.<patch>" are skipped with a
# notice rather than failing the build.
#
# REQUIRED ENVIRONMENT VARIABLES
# ------------------------------
#   MILESTONE_TITLE    The PR's milestone title (e.g. "7.0.3").
#   BASE_REF           The branch the PR targets (e.g. "main", "release/7.0").
#   DEFAULT_BRANCH     The repository's default branch (e.g. "main").
#   GITHUB_REPOSITORY  Owner/repo (e.g. "dotnet/SqlClient"). Set automatically by Actions.
#   GH_TOKEN           GitHub token for API calls (gh CLI auth).
#
# OUTPUTS
# -------
#   Emits ::notice:: on success/skip and ::error:: on failure.
#   Exits 0 when the milestone and target branch agree (or the check is
#   skipped), and 1 when they conflict.
#
# USAGE
#   Called from the check-milestone.yml workflow. Can also be run locally:
#
#     export MILESTONE_TITLE="7.0.3"
#     export BASE_REF="main"
#     export DEFAULT_BRANCH="main"
#     export GITHUB_REPOSITORY="dotnet/SqlClient"
#     bash .github/scripts/check-milestone-branch.sh
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
: "${MILESTONE_TITLE:?MILESTONE_TITLE environment variable is required}"
: "${BASE_REF:?BASE_REF environment variable is required}"
: "${DEFAULT_BRANCH:?DEFAULT_BRANCH environment variable is required}"
: "${GITHUB_REPOSITORY:?GITHUB_REPOSITORY environment variable is required}"

# -- Derive the candidate release branch from the milestone -------------------
# Accepts "X.Y.Z" with an optional pre-release/build suffix, e.g. "8.0.0-preview1".
if [[ "${MILESTONE_TITLE}" =~ ^([0-9]+)\.([0-9]+)\.([0-9]+)([-+].*)?$ ]]; then
  MAJOR="${BASH_REMATCH[1]}"
  MINOR="${BASH_REMATCH[2]}"
  PATCH="${BASH_REMATCH[3]}"
else
  echo "::notice::Milestone '${MILESTONE_TITLE}' is not in 'major.minor.patch' form; skipping the target branch check."
  exit 0
fi

RELEASE_BRANCH="release/${MAJOR}.${MINOR}"

# -- Skip integration branches ------------------------------------------------
# Only the default branch and release branches carry milestone semantics.
if [[ "${BASE_REF}" != "${DEFAULT_BRANCH}" && "${BASE_REF}" != release/* ]]; then
  echo "::notice::PR targets integration branch '${BASE_REF}'; skipping the milestone/branch check."
  exit 0
fi

# -- Validate a release branch target -----------------------------------------
# The target branch itself proves which version is being serviced, so no branch
# listing is needed here.
if [[ "${BASE_REF}" == release/* ]]; then
  if [[ "${BASE_REF}" != "${RELEASE_BRANCH}" ]]; then
    echo "::error::Milestone '${MILESTONE_TITLE}' belongs to '${RELEASE_BRANCH}', but this PR targets '${BASE_REF}'. Retarget the PR or assign the milestone that matches '${BASE_REF}'."
    exit 1
  fi

  echo "::notice::Milestone '${MILESTONE_TITLE}' matches target branch '${BASE_REF}'."
  exit 0
fi

# -- Validate a default branch target -----------------------------------------
# 'matching-refs' returns only refs under the given prefix, so this is a single
# cheap call regardless of how many topic branches the repository has.
if ! RELEASE_REFS=$(gh api "repos/${GITHUB_REPOSITORY}/git/matching-refs/heads/release/" \
    --jq '.[].ref' 2>&1); then
  echo "::error::Unable to list release branches for '${GITHUB_REPOSITORY}': ${RELEASE_REFS}"
  exit 1
fi

if grep -qxF "refs/heads/${RELEASE_BRANCH}" <<< "${RELEASE_REFS}"; then
  echo "::error::Milestone '${MILESTONE_TITLE}' is a servicing release owned by '${RELEASE_BRANCH}', but this PR targets '${DEFAULT_BRANCH}'. Either retarget the PR to '${RELEASE_BRANCH}', or assign an in-development milestone and add the 'Hotfix ${MAJOR}.${MINOR}.${PATCH}' label so the change is cherry-picked after merge."
  exit 1
fi

if ! OPEN_MILESTONES=$(gh api --paginate "repos/${GITHUB_REPOSITORY}/milestones?state=open&per_page=100" \
    --jq '.[].title' 2>&1); then
  echo "::error::Unable to list open milestones for '${GITHUB_REPOSITORY}': ${OPEN_MILESTONES}"
  exit 1
fi

ACTIVE_MAJOR=""
ACTIVE_MINOR=""
ACTIVE_MILESTONE=""
while IFS= read -r milestone; do
  if [[ ! "${milestone}" =~ ^([0-9]+)\.([0-9]+)\.([0-9]+)([-+].*)?$ ]]; then
    continue
  fi

  candidate_major="${BASH_REMATCH[1]}"
  candidate_minor="${BASH_REMATCH[2]}"
  candidate_branch="release/${candidate_major}.${candidate_minor}"
  if grep -qxF "refs/heads/${candidate_branch}" <<< "${RELEASE_REFS}"; then
    continue
  fi

  if [[ -z "${ACTIVE_MAJOR}" ]] ||
      (( 10#${candidate_major} < 10#${ACTIVE_MAJOR} )) ||
      (( 10#${candidate_major} == 10#${ACTIVE_MAJOR} && 10#${candidate_minor} < 10#${ACTIVE_MINOR} )); then
    ACTIVE_MAJOR="${candidate_major}"
    ACTIVE_MINOR="${candidate_minor}"
    ACTIVE_MILESTONE="${milestone}"
  fi
done <<< "${OPEN_MILESTONES}"

if [[ -n "${ACTIVE_MAJOR}" ]] &&
    { (( 10#${MAJOR} > 10#${ACTIVE_MAJOR} )) ||
      (( 10#${MAJOR} == 10#${ACTIVE_MAJOR} && 10#${MINOR} > 10#${ACTIVE_MINOR} )); }; then
  echo "::error::Milestone '${MILESTONE_TITLE}' is for a later development line, but '${ACTIVE_MILESTONE}' remains active on '${DEFAULT_BRANCH}' until 'release/${ACTIVE_MAJOR}.${ACTIVE_MINOR}' is cut. Assign a milestone from the active ${ACTIVE_MAJOR}.${ACTIVE_MINOR} line."
  exit 1
fi

echo "::notice::Milestone '${MILESTONE_TITLE}' is still in development (no '${RELEASE_BRANCH}' branch); targeting '${DEFAULT_BRANCH}' is correct."
