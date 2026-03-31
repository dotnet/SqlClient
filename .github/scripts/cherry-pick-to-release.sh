#!/usr/bin/env bash
#################################################################################
# Licensed to the .NET Foundation under one or more agreements.                 #
# The .NET Foundation licenses this file to you under the MIT license.          #
# See the LICENSE file in the project root for more information.                #
#################################################################################
#
# cherry-pick-to-release.sh
#
# Cherry-picks a merge commit from the default branch onto a release branch
# and opens a pull request for the result. If the cherry-pick conflicts, an
# empty-commit placeholder PR is created with manual resolution instructions.
#
# OVERVIEW
# --------
# This script performs the following steps:
#
#   1. Derive the target release branch from the version's major.minor
#      (e.g. "7.0.1" → release/7.0).
#
#   2. Check whether the commit's patch is already present on the target
#      branch (via 'git cherry'). If so, exit cleanly — nothing to do.
#
#   3. Detect whether the merge commit is a true merge (2+ parents) or a
#      squash-merge (1 parent).  True merges require '--mainline 1'.
#
#   4. Attempt the cherry-pick:
#      - On success: push the branch, look up the milestone, create a PR.
#      - On conflict: abort, push an empty-commit placeholder, create a
#        "CONFLICTS" PR with manual resolution instructions.
#
#   5. Milestone lookup is best-effort. If the milestone doesn't exist yet
#      the PR is created without one and a warning note is added to the body.
#
# REQUIRED ENVIRONMENT VARIABLES
# ------------------------------
#   VERSION            Full hotfix version, e.g. "7.0.1".
#   MERGE_COMMIT_SHA   SHA of the merge commit on the default branch.
#   PR_NUMBER          Number of the original PR that was merged.
#   PR_TITLE           Title of the original PR (used in cherry-pick PR title).
#   GH_TOKEN           GitHub token for 'gh' CLI authentication.
#   GITHUB_REPOSITORY  Owner/repo (e.g. "dotnet/SqlClient"). Set by Actions.
#
# OUTPUTS
# -------
#   On success or conflict, a new PR is created on GitHub.
#   On already-applied, the script exits 0 with a notice.
#
# USAGE
#   Typically called from the cherry-pick-hotfix.yml workflow.
#   The git working directory must have full history (fetch-depth: 0) and
#   user.name / user.email must be configured before calling this script.
#
#   Local testing example (dry-run — comment out 'gh pr create' calls):
#
#     export VERSION="7.0.1"
#     export MERGE_COMMIT_SHA="abc123"
#     export PR_NUMBER=42
#     export PR_TITLE="Fix connection timeout"
#     export GH_TOKEN="ghp_..."
#     export GITHUB_REPOSITORY="dotnet/SqlClient"
#     bash .github/scripts/cherry-pick-to-release.sh
#
#################################################################################
set -euo pipefail

# -- Runtime help -------------------------------------------------------------
if [[ "${1:-}" == "--help" || "${1:-}" == "-h" ]]; then
  cat <<'EOF'
Usage: cherry-pick-to-release.sh

Cherry-picks a merge commit into a release branch and opens a PR.

Required environment variables:
  VERSION            Hotfix version (e.g. "7.0.1")
  MERGE_COMMIT_SHA   SHA of the merge commit
  PR_NUMBER          Original PR number
  PR_TITLE           Original PR title
  GH_TOKEN           GitHub token
  GITHUB_REPOSITORY  owner/repo (set by Actions)

Behavior:
  - Derives release branch from major.minor (7.0.1 → release/7.0)
  - Skips if patch is already applied on the target branch
  - Creates a clean PR on success, or a CONFLICTS PR on failure
  - Milestone is set if it exists; otherwise a warning is logged

Exit codes:
  0  Success (including "already applied" — no PR created)
  1  Error (e.g. version parse failure)
EOF
  exit 0
fi

# -- Input validation ---------------------------------------------------------
: "${VERSION:?VERSION environment variable is required}"
: "${MERGE_COMMIT_SHA:?MERGE_COMMIT_SHA environment variable is required}"
: "${PR_NUMBER:?PR_NUMBER environment variable is required}"
: "${PR_TITLE:?PR_TITLE environment variable is required}"
: "${GITHUB_REPOSITORY:?GITHUB_REPOSITORY environment variable is required}"

# -- Step 1: Derive target branch from major.minor ---------------------------
# "7.0.1" → "7.0", so target branch is "release/7.0".
# Use '|| true' to prevent set -e from aborting on a non-matching grep.
BRANCH_VERSION=$(echo "${VERSION}" | grep -oP '^\d+\.\d+' || true)
if [[ -z "${BRANCH_VERSION}" ]]; then
  echo "::error::Could not parse major.minor from version '${VERSION}'."
  exit 1
fi

TARGET_BRANCH="release/${BRANCH_VERSION}"
CHERRY_PICK_BRANCH="dev/automation/pr-${PR_NUMBER}-to-${VERSION}"

echo "Version:            ${VERSION}"
echo "Target branch:      ${TARGET_BRANCH}"
echo "Cherry-pick branch: ${CHERRY_PICK_BRANCH}"
echo "Merge commit:       ${MERGE_COMMIT_SHA}"

# Ensure the target branch ref is available locally.
git fetch origin "${TARGET_BRANCH}"

# -- Step 2: Check if the patch is already applied ----------------------------
# 'git cherry' compares patches between two points. A '-' prefix means the
# patch is already present (equivalent commit exists on the target branch).
# A '+' prefix means it has not been applied yet.
if git cherry "origin/${TARGET_BRANCH}" "${MERGE_COMMIT_SHA}^" "${MERGE_COMMIT_SHA}" \
    | grep -q '^-'; then
  echo "::notice::Commit ${MERGE_COMMIT_SHA} is already applied on" \
       "${TARGET_BRANCH}. Skipping cherry-pick."
  exit 0
fi

# Create the cherry-pick working branch from the target release branch.
git checkout -b "${CHERRY_PICK_BRANCH}" "origin/${TARGET_BRANCH}"

# -- Step 3: Detect merge commit type -----------------------------------------
# True merge commits have 2+ parents and require '--mainline 1' to tell git
# which parent's tree to diff against (the first parent = the target branch).
# Squash-merge commits have exactly 1 parent and must NOT use --mainline.
#
# 'git rev-list --parents -n1 <sha>' outputs: <sha> <parent1> [<parent2> ...]
# awk counts fields and subtracts 1 (the SHA itself) to get the parent count.
PARENT_COUNT=$(git rev-list --parents -n1 "${MERGE_COMMIT_SHA}" \
  | awk '{print NF - 1}')
MAINLINE_FLAG=""
if [[ "${PARENT_COUNT}" -gt 1 ]]; then
  MAINLINE_FLAG="--mainline 1"
  echo "Merge commit has ${PARENT_COUNT} parents — using --mainline 1."
else
  echo "Squash-merge commit (single parent) — no --mainline flag needed."
fi

# -- Helper: look up milestone ------------------------------------------------
# Milestone assignment is best-effort. If the milestone doesn't exist yet, the
# PR is created without one and a note is appended to the PR body.
lookup_milestone() {
  local version="$1"
  MILESTONE_ARG=""
  MILESTONE_NOTE=""

  if gh api "repos/${GITHUB_REPOSITORY}/milestones" \
      --jq '.[].title' | grep -qx "${version}"; then
    MILESTONE_ARG="--milestone ${version}"
    echo "Milestone '${version}' found."
  else
    echo "::warning::Milestone '${version}' does not exist." \
         "PR will be created without a milestone."
    MILESTONE_NOTE=$'\n\n> **Note:** Milestone `'"${version}"'` does not exist yet. Please create it and assign this PR manually.'
  fi
}

# -- Step 4: Attempt the cherry-pick ------------------------------------------
if git cherry-pick "${MERGE_COMMIT_SHA}" ${MAINLINE_FLAG}; then
  # --- Success path ---
  echo "Cherry-pick succeeded. Pushing branch and creating PR."
  git push origin "${CHERRY_PICK_BRANCH}"

  lookup_milestone "${VERSION}"

  gh pr create \
    --base "${TARGET_BRANCH}" \
    --head "${CHERRY_PICK_BRANCH}" \
    --title "[${VERSION} Cherry-pick] ${PR_TITLE}" \
    --body "Cherry-pick of #${PR_NUMBER} (${MERGE_COMMIT_SHA}) into \`${TARGET_BRANCH}\`.${MILESTONE_NOTE}" \
    ${MILESTONE_ARG}
else
  # --- Conflict path ---
  echo "::error::Cherry-pick of ${MERGE_COMMIT_SHA} failed due to conflicts."
  git cherry-pick --abort

  # Build the cherry-pick command for inclusion in the conflict-resolution
  # instructions. Only include --mainline 1 when the commit is a true merge.
  CHERRY_PICK_CMD="git cherry-pick ${MERGE_COMMIT_SHA}"
  if [[ -n "${MAINLINE_FLAG}" ]]; then
    CHERRY_PICK_CMD="${CHERRY_PICK_CMD} ${MAINLINE_FLAG}"
  fi

  # Create a branch with an empty commit so a PR can be opened. The PR body
  # contains step-by-step instructions for manual conflict resolution.
  git checkout "origin/${TARGET_BRANCH}"
  git checkout -B "${CHERRY_PICK_BRANCH}"
  git commit --allow-empty \
    -m "Cherry-pick of #${PR_NUMBER} requires manual resolution" \
    -m "To resolve, run:  ${CHERRY_PICK_CMD}"
  git push origin "${CHERRY_PICK_BRANCH}"

  lookup_milestone "${VERSION}"

  gh pr create \
    --base "${TARGET_BRANCH}" \
    --head "${CHERRY_PICK_BRANCH}" \
    --title "[${VERSION} Cherry-pick - CONFLICTS] ${PR_TITLE}" \
    ${MILESTONE_ARG} \
    --body $'Cherry-pick of #'"${PR_NUMBER}"' ('"${MERGE_COMMIT_SHA}"') into `'"${TARGET_BRANCH}"'` **failed due to merge conflicts**.'"${MILESTONE_NOTE}"$'\n\nPlease resolve manually:\n```bash\ngit fetch origin\ngit checkout '"${CHERRY_PICK_BRANCH}"'\n'"${CHERRY_PICK_CMD}"$'\n# resolve conflicts\ngit push origin '"${CHERRY_PICK_BRANCH}"' --force\n```'
fi
