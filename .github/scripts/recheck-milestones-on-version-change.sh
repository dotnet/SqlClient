#!/usr/bin/env bash
#################################################################################
# Licensed to the .NET Foundation under one or more agreements.                 #
# The .NET Foundation licenses this file to you under the MIT license.          #
# See the LICENSE file in the project root for more information.                #
#################################################################################
#
# Re-evaluates open PR milestones after SqlClientNextVersion changes and creates
# fresh check runs on their head commits. Re-running an old workflow run would
# still use the old PR merge commit and version.
#
# Required environment variables:
#   BASE_BRANCH       The branch whose version changed.
#   GITHUB_REPOSITORY Owner/repo.
#   GH_TOKEN          GitHub token with pull-request read and checks write.
#
#################################################################################
set -euo pipefail

source "$(dirname "${BASH_SOURCE[0]}")/read-sqlclient-version.sh"

: "${BASE_BRANCH:?BASE_BRANCH environment variable is required}"
: "${GITHUB_REPOSITORY:?GITHUB_REPOSITORY environment variable is required}"
: "${GH_TOKEN:?GH_TOKEN environment variable is required}"

CHECK_NAME="Check milestone matches next SqlClient version"
versions_file="${SQLCLIENT_VERSIONS_FILE:-src/Microsoft.Data.SqlClient/Versions.props}"
version_property="${SQLCLIENT_VERSION_PROPERTY:-SqlClientNextVersion}"
if ! next_version=$(read_sqlclient_version "${versions_file}" "${version_property}"); then
  exit 1
fi

if ! pull_requests=$(gh pr list \
    --repo "${GITHUB_REPOSITORY}" \
    --base "${BASE_BRANCH}" \
    --state open \
    --limit 1000 \
    --json number,headRefOid,milestone); then
  echo "::error::Unable to list open pull requests targeting '${BASE_BRANCH}'."
  exit 1
fi

if ! eligible_pull_requests=$(jq -c '
  .[]
  | select(.milestone != null and (.milestone.title | test("^[0-9]+\\.[0-9]+\\.[0-9]+([-+].*)?$")))
' <<< "${pull_requests}"); then
  echo "::error::Unable to parse open pull requests targeting '${BASE_BRANCH}'."
  exit 1
fi

if [[ -z "${eligible_pull_requests}" ]]; then
  echo "::notice::No open semantic-version milestone PRs target '${BASE_BRANCH}'."
  exit 0
fi

while IFS= read -r pull_request; do
  number=$(jq -r '.number' <<< "${pull_request}")
  head_sha=$(jq -r '.headRefOid' <<< "${pull_request}")
  milestone=$(jq -r '.milestone.title' <<< "${pull_request}")
  if [[ "${milestone}" == "${next_version}" ]]; then
    conclusion="success"
    title="Milestone version matches next SqlClient version"
    summary="Milestone version \`${milestone}\` matches next SqlClient version \`${next_version}\`."
  else
    conclusion="failure"
    title="Milestone version does not match next SqlClient version"
    summary="Milestone version \`${milestone}\` does not match next SqlClient version \`${next_version}\`."
  fi

  if ! check_run=$(jq -n \
      --arg name "${CHECK_NAME}" \
      --arg head_sha "${head_sha}" \
      --arg conclusion "${conclusion}" \
      --arg title "${title}" \
      --arg summary "${summary}" \
      '{name:$name, head_sha:$head_sha, status:"completed", conclusion:$conclusion, output:{title:$title,summary:$summary}}'); then
    echo "::error::Unable to prepare a fresh version check for PR #${number}."
    exit 1
  fi

  if ! gh api --method POST "repos/${GITHUB_REPOSITORY}/check-runs" --input - <<< "${check_run}" >/dev/null; then
    echo "::error::Unable to create a fresh version check for PR #${number}."
    exit 1
  fi
  echo "::notice::Created a fresh '${CHECK_NAME}' check for PR #${number}: ${conclusion}."
done <<< "${eligible_pull_requests}"
