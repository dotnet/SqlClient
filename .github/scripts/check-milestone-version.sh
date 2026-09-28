#!/usr/bin/env bash
#################################################################################
# Licensed to the .NET Foundation under one or more agreements.                 #
# The .NET Foundation licenses this file to you under the MIT license.          #
# See the LICENSE file in the project root for more information.                #
#################################################################################
#
# Validates that a semantic-version PR milestone exactly matches
# SqlClientNextVersion.
#
# Integration branches and non-semver milestones are skipped, matching the
# scope of check-milestone-branch.sh.
#
# Required environment variables:
#   MILESTONE_TITLE  The PR milestone title.
#   BASE_REF         The branch the PR targets.
#   DEFAULT_BRANCH   The repository's default branch.
#
# SQLCLIENT_VERSIONS_FILE optionally overrides the canonical Versions.props
# path, primarily for tests.
#
#################################################################################
set -euo pipefail

: "${MILESTONE_TITLE:?MILESTONE_TITLE environment variable is required}"
: "${BASE_REF:?BASE_REF environment variable is required}"
: "${DEFAULT_BRANCH:?DEFAULT_BRANCH environment variable is required}"

if [[ ! "${MILESTONE_TITLE}" =~ ^[0-9]+\.[0-9]+\.[0-9]+([-+].*)?$ ]]; then
  echo "::notice::Milestone '${MILESTONE_TITLE}' is not a semantic version; skipping the next-version check."
  exit 0
fi

if [[ "${BASE_REF}" != "${DEFAULT_BRANCH}" && "${BASE_REF}" != release/* ]]; then
  echo "::notice::PR targets integration branch '${BASE_REF}'; skipping the next-version check."
  exit 0
fi

versions_file="${SQLCLIENT_VERSIONS_FILE:-src/Microsoft.Data.SqlClient/Versions.props}"
if [[ ! -r "${versions_file}" ]]; then
  echo "::error::Unable to read SqlClient version file '${versions_file}'."
  exit 1
fi

if ! next_version=$(sed -nE 's/.*<SqlClientNextVersion>([^<]+)<\/SqlClientNextVersion>.*/\1/p' "${versions_file}"); then
  echo "::error::Unable to read SqlClientNextVersion from '${versions_file}'."
  exit 1
fi
if [[ -z "${next_version}" || "${next_version}" == *$'\n'* ]]; then
  echo "::error::Expected exactly one SqlClientNextVersion in '${versions_file}'."
  exit 1
fi
if [[ "${MILESTONE_TITLE}" != "${next_version}" ]]; then
  echo "::error::The PR milestone does not match SqlClientNextVersion in '${versions_file}'. Update the version or assign the matching milestone."
  exit 1
fi

echo "::notice::Milestone '${MILESTONE_TITLE}' matches SqlClientNextVersion."
