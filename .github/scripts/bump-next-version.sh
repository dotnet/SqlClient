#!/usr/bin/env bash
#################################################################################
# Licensed to the .NET Foundation under one or more agreements.                 #
# The .NET Foundation licenses this file to you under the MIT license.          #
# See the LICENSE file in the project root for more information.                #
#################################################################################
#
# Propose the next SqlClient version when its milestone closes, or reconcile
# active branches weekly. Run this script outside the repository checkout being
# edited so release-branch contents cannot replace the running automation.

set -euo pipefail

: "${DEFAULT_BRANCH:?DEFAULT_BRANCH is required}"
: "${GITHUB_REPOSITORY:?GITHUB_REPOSITORY is required}"
: "${GH_TOKEN:?GH_TOKEN is required}"

if [[ "${1:-}" == "--sync" ]]; then
  if ! refs=$(git ls-remote origin 'refs/heads/release/*'); then
    echo "::error::Could not list release branches." >&2
    exit 1
  fi
  SYNC_BRANCH="${DEFAULT_BRANCH}" bash "$0" --reconcile
  while IFS=$'\t' read -r _ ref; do
    if [[ "${ref}" =~ ^refs/heads/release/[0-9]+\.[0-9]+$ ]]; then
      SYNC_BRANCH="${ref#refs/heads/}" bash "$0" --reconcile
    fi
  done <<< "${refs}"
  exit 0
fi

reconcile=false
if [[ "${1:-}" == "--reconcile" ]]; then
  reconcile=true
  : "${SYNC_BRANCH:?SYNC_BRANCH is required}"
elif [[ -z "${1:-}" ]]; then
  : "${CLOSED_MILESTONE:?CLOSED_MILESTONE is required}"
  if [[ ! "${CLOSED_MILESTONE}" =~ ^([0-9]+)\.([0-9]+)\.([0-9]+)(-preview([0-9]+))?$ ]]; then
    echo "::notice::Skipping non-SqlClient milestone '${CLOSED_MILESTONE}'."
    exit 0
  fi
  major=$((10#${BASH_REMATCH[1]}))
  minor=$((10#${BASH_REMATCH[2]}))
else
  echo "::error::Unknown operation: $1" >&2
  exit 1
fi

version_file="src/Microsoft.Data.SqlClient/Versions.props"
if [[ "${reconcile}" == "true" ]]; then
  target_branch="${SYNC_BRANCH}"
else
  release_branch="release/${major}.${minor}"
  if ! release_ref=$(git ls-remote origin "refs/heads/${release_branch}"); then
    echo "::error::Could not look up ${release_branch}." >&2
    exit 1
  fi
  target_branch="${DEFAULT_BRANCH}"
  if [[ -n "${release_ref}" ]]; then
    target_branch="${release_branch}"
  fi
fi

if ! git fetch origin "${target_branch}"; then
  echo "::error::Could not fetch ${target_branch}." >&2
  exit 1
fi
target_sha=$(git rev-parse FETCH_HEAD)
if ! git cat-file -e "FETCH_HEAD:${version_file}" 2>/dev/null; then
  echo "::notice::Skipping ${target_branch}: it does not use the unified SqlClient Versions.props."
  exit 0
fi

current_version=$(git show "FETCH_HEAD:${version_file}" |
  sed -n 's/.*<SqlClientNextVersion>\([^<]*\)<\/SqlClientNextVersion>.*/\1/p')
if [[ -z "${current_version}" ]]; then
  echo "::error::Cannot read SqlClientNextVersion on ${target_branch}." >&2
  exit 1
fi
if [[ "${reconcile}" == "true" ]]; then
  if [[ ! "${current_version}" =~ ^([0-9]+)\.([0-9]+)\.([0-9]+)(-preview([0-9]+))?$ ]]; then
    echo "::notice::Skipping ${target_branch}: current version '${current_version}' is not a supported milestone."
    exit 0
  fi
  major=$((10#${BASH_REMATCH[1]}))
  minor=$((10#${BASH_REMATCH[2]}))
else
  if [[ "${current_version}" != "${CLOSED_MILESTONE}" ]]; then
    echo "::notice::${target_branch} already targets ${current_version}, not ${CLOSED_MILESTONE}; skipping stale closure."
    exit 0
  fi
fi

if [[ ! "${current_version}" =~ ^([0-9]+)\.([0-9]+)\.([0-9]+)(-preview([0-9]+))?$ ]]; then
  echo "::error::Unsupported current version '${current_version}' on ${target_branch}." >&2
  exit 1
fi
patch=$((10#${BASH_REMATCH[3]}))
closed_stage=1
closed_preview=0
if [[ -n "${BASH_REMATCH[5]}" ]]; then
  closed_stage=0
  closed_preview=$((10#${BASH_REMATCH[5]}))
fi
if [[ "${reconcile}" == "true" && "${target_branch}" =~ ^release/([0-9]+)\.([0-9]+)$ ]]; then
  if (( 10#${BASH_REMATCH[1]} != major || 10#${BASH_REMATCH[2]} != minor )); then
    echo "::error::${target_branch} has SqlClientNextVersion=${current_version} from a different series." >&2
    exit 1
  fi
fi

if ! milestones=$(gh api --paginate "repos/${GITHUB_REPOSITORY}/milestones?state=all&per_page=100" \
    --jq '.[] | [.title, .state] | @tsv'); then
  echo "::error::Could not list milestones." >&2
  exit 1
fi

next_version=""
create_milestone=false
current_open=false
current_owned=true
best_patch=0
best_major=0
best_minor=0
best_stage=0
best_preview=0
while IFS=$'\t' read -r title state; do
  if [[ ! "${title}" =~ ^([0-9]+)\.([0-9]+)\.([0-9]+)(-preview([0-9]+))?$ ]]; then
    continue
  fi
  candidate_major=$((10#${BASH_REMATCH[1]}))
  candidate_minor=$((10#${BASH_REMATCH[2]}))
  candidate_patch=$((10#${BASH_REMATCH[3]}))
  candidate_stage=1
  candidate_preview=0
  if [[ -n "${BASH_REMATCH[5]}" ]]; then
    candidate_stage=0
    candidate_preview=$((10#${BASH_REMATCH[5]}))
  fi
  if [[ "${title}" == "${current_version}" && "${state}" == "open" ]]; then
    current_open=true
  fi
  if [[ "${reconcile}" == "true" && "${target_branch}" == "${DEFAULT_BRANCH}" &&
        "${state}" == "open" &&
        ( "${candidate_major}" -gt "${major}" ||
          ( "${candidate_major}" -eq "${major}" && "${candidate_minor}" -ge "${minor}" ) ) ]]; then
    if ! candidate_release_ref=$(git ls-remote origin "refs/heads/release/${candidate_major}.${candidate_minor}"); then
      echo "::error::Could not check release branch for ${title}." >&2
      exit 1
    fi
    if [[ -n "${candidate_release_ref}" ]]; then
      if [[ "${candidate_major}" -eq "${major}" && "${candidate_minor}" -eq "${minor}" ]]; then
        current_owned=false
      fi
      continue
    fi
  fi
  if [[ "${state}" != "open" ]] ||
      (( candidate_major < major ||
         (candidate_major == major && candidate_minor < minor) )) ||
      { [[ "${reconcile}" == "false" || "${target_branch}" != "${DEFAULT_BRANCH}" ]] &&
        (( candidate_major != major || candidate_minor != minor )); } ||
      (( candidate_major == major && candidate_minor == minor &&
         (candidate_patch < patch ||
          (candidate_patch == patch && candidate_stage < closed_stage) ||
          (candidate_patch == patch && candidate_stage == closed_stage && candidate_preview <= closed_preview)) )); then
    continue
  fi
  if [[ -z "${next_version}" ]] ||
      (( candidate_major < best_major ||
         (candidate_major == best_major && candidate_minor < best_minor) ||
         (candidate_major == best_major && candidate_minor == best_minor && candidate_patch < best_patch) ||
         (candidate_major == best_major && candidate_minor == best_minor && candidate_patch == best_patch && candidate_stage < best_stage) ||
         (candidate_major == best_major && candidate_minor == best_minor && candidate_patch == best_patch && candidate_stage == best_stage && candidate_preview < best_preview) )); then
    next_version="${title}"
    best_major="${candidate_major}"
    best_minor="${candidate_minor}"
    best_patch="${candidate_patch}"
    best_stage="${candidate_stage}"
    best_preview="${candidate_preview}"
  fi
done <<< "${milestones}"

if [[ "${reconcile}" == "true" && "${current_open}" == "true" && "${current_owned}" == "true" ]]; then
  echo "::notice::${target_branch} already targets open milestone ${current_version}."
  exit 0
fi

if [[ -z "${next_version}" ]]; then
  if [[ "${reconcile}" == "true" ]]; then
    echo "::notice::No later open milestone for ${target_branch}; leaving ${current_version} unchanged."
    exit 0
  fi
  next_patch=$((patch + 1))
  while grep -Eq "^${major}\.${minor}\.${next_patch}(-preview[0-9]+)?[[:space:]]" <<< "${milestones}"; do
    next_patch=$((next_patch + 1))
  done
  next_version="${major}.${minor}.${next_patch}"
  create_milestone=true
fi

head_branch="dev/automation/next-version-${target_branch//\//-}-${current_version}"
if ! existing_pr=$(gh pr list --repo "${GITHUB_REPOSITORY}" --base "${target_branch}" \
    --head "${head_branch}" --state all --json number,state \
    --jq '.[0] | if . then [.number, .state] | @tsv else empty end'); then
  echo "::error::Could not look up existing version-bump PRs." >&2
  exit 1
fi
IFS=$'\t' read -r pr_number pr_state <<< "${existing_pr}"
if [[ -n "${pr_number}" && ( "${reconcile}" == "false" || "${pr_state}" != "OPEN" ) ]]; then
  echo "::notice::Version-bump PR #${pr_number} already exists; skipping."
  exit 0
fi
if ! existing_ref=$(git ls-remote origin "refs/heads/${head_branch}"); then
  echo "::error::Could not look up ${head_branch}." >&2
  exit 1
fi

ensure_milestone() {
  if [[ "${create_milestone}" == "true" ]]; then
    echo "No later open milestone in ${major}.${minor}; creating ${next_version}."
    if ! gh api -X POST "repos/${GITHUB_REPOSITORY}/milestones" -f "title=${next_version}"; then
      echo "::error::Could not create milestone ${next_version}." >&2
      exit 1
    fi
  fi
}

create_pr() {
  local reason="The ${current_version} milestone closed."
  if [[ "${reconcile}" == "true" ]]; then
    reason="Weekly milestone reconciliation: ${current_version} is no longer the next open milestone for ${target_branch}."
  fi
  gh pr create --repo "${GITHUB_REPOSITORY}" --base "${target_branch}" \
    --head "${head_branch}" --milestone "${next_version}" \
    --title "Set next SqlClient version to ${next_version}" \
    --body "${reason} Proposes ${next_version} for the SqlClient family on \`${target_branch}\`.

**Validation:** This PR was created with \`GITHUB_TOKEN\`, which does not trigger \`pull_request\` workflows. Push a follow-up commit using a non-default token to trigger the checks before merging."
}

edit_version() {
  python3 - "${version_file}" "$1" "${next_version}" <<'PY'
import pathlib
import re
import sys

path = pathlib.Path(sys.argv[1])
old, new = sys.argv[2:]
contents = path.read_text()
updated, count = re.subn(
    rf"(<SqlClientNextVersion>){re.escape(old)}(</SqlClientNextVersion>)",
    lambda match: match.group(1) + new + match.group(2),
    contents,
)
if count != 1:
    raise SystemExit(f"Expected exactly one SqlClientNextVersion={old} in {path}; found {count}")
path.write_text(updated)
PY
}

if [[ -n "${existing_ref}" ]]; then
  git fetch origin "${head_branch}"
  branch_version=$(git show "FETCH_HEAD:${version_file}" |
    sed -n 's/.*<SqlClientNextVersion>\([^<]*\)<\/SqlClientNextVersion>.*/\1/p')
  if [[ -z "${branch_version}" ]] ||
      ! git merge-base --is-ancestor "${target_sha}" FETCH_HEAD ||
      [[ "$(git diff --name-only "${target_sha}" FETCH_HEAD)" != "${version_file}" ]] ||
      ! git show "${target_sha}:${version_file}" |
        sed "s|<SqlClientNextVersion>${current_version}</SqlClientNextVersion>|<SqlClientNextVersion>${branch_version}</SqlClientNextVersion>|" |
        cmp -s - <(git show "FETCH_HEAD:${version_file}"); then
    echo "::error::${head_branch} exists but does not contain only the expected version bump; resolve it manually." >&2
    exit 1
  fi
  if [[ -n "${pr_number}" && "${reconcile}" == "true" ]]; then
    if [[ "${branch_version}" == "${next_version}" ]]; then
      echo "::notice::Version-bump PR #${pr_number} already proposes ${next_version}."
      exit 0
    fi
    echo "Updating version-bump PR #${pr_number} from ${branch_version} to ${next_version}."
    ensure_milestone
    git switch --detach FETCH_HEAD
    edit_version "${branch_version}"
    git add -- "${version_file}"
    git -c user.name="github-actions[bot]" \
        -c user.email="41898282+github-actions[bot]@users.noreply.github.com" \
        commit -m "Update next SqlClient version to ${next_version}"
    git push origin "HEAD:refs/heads/${head_branch}"
    gh pr edit "${pr_number}" --repo "${GITHUB_REPOSITORY}" \
      --milestone "${next_version}" --title "Set next SqlClient version to ${next_version}"
    exit 0
  fi
  if [[ "${branch_version}" != "${next_version}" ]]; then
    echo "::error::${head_branch} proposes ${branch_version}, not ${next_version}; resolve it manually." >&2
    exit 1
  fi
  echo "::notice::Reusing ${head_branch} to retry PR creation."
  ensure_milestone
  create_pr
  exit 0
fi

ensure_milestone
git switch -c "${head_branch}" "${target_sha}"
edit_version "${current_version}"
git add -- "${version_file}"
git -c user.name="github-actions[bot]" \
    -c user.email="41898282+github-actions[bot]@users.noreply.github.com" \
    commit -m "Set next SqlClient version to ${next_version}"
git push origin "HEAD:refs/heads/${head_branch}"
create_pr
