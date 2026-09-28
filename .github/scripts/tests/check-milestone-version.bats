#!/usr/bin/env bats
#################################################################################
# Licensed to the .NET Foundation under one or more agreements.                 #
# The .NET Foundation licenses this file to you under the MIT license.          #
# See the LICENSE file in the project root for more information.                #
#################################################################################
#
# Tests for check-milestone-version.sh
#
# Run with:  bats .github/scripts/tests/check-milestone-version.bats
#
#################################################################################

SCRIPT=".github/scripts/check-milestone-version.sh"

setup() {
  STUB_DIR="$(mktemp -d)"
  export MILESTONE_TITLE="8.0.0-preview1"
  export BASE_REF="main"
  export DEFAULT_BRANCH="main"
  export SQLCLIENT_VERSIONS_FILE="${STUB_DIR}/Versions.props"
  mock_next_version "8.0.0-preview1"
}

teardown() {
  rm -rf "${STUB_DIR}"
}

mock_next_version() {
  printf '<Project>\n  <PropertyGroup>\n    <SqlClientNextVersion>%s</SqlClientNextVersion>\n  </PropertyGroup>\n</Project>\n' "$1" > "${SQLCLIENT_VERSIONS_FILE}"
}

@test "passes and logs success when a preview milestone matches" {
  run bash "${SCRIPT}"
  [ "$status" -eq 0 ]
  [[ "$output" == *"::notice::Milestone '8.0.0-preview1' matches SqlClientNextVersion."* ]]
}

@test "fails when a stale preview milestone no longer matches" {
  mock_next_version "8.0.0-preview2"
  run bash "${SCRIPT}"
  [ "$status" -eq 1 ]
  [[ "$output" == *"does not match SqlClientNextVersion"* ]]
}

@test "passes when a stable milestone matches" {
  export MILESTONE_TITLE="7.1.1"
  mock_next_version "7.1.1"
  run bash "${SCRIPT}"
  [ "$status" -eq 0 ]
  [[ "$output" == *"::notice::Milestone '7.1.1' matches SqlClientNextVersion."* ]]
}

@test "fails closed when SqlClientNextVersion is missing" {
  printf '<Project />\n' > "${SQLCLIENT_VERSIONS_FILE}"
  run bash "${SCRIPT}"
  [ "$status" -eq 1 ]
  [[ "$output" == *"Expected exactly one SqlClientNextVersion"* ]]
}

@test "fails closed when SqlClientNextVersion is duplicated" {
  printf '<SqlClientNextVersion>8.0.0-preview1</SqlClientNextVersion>\n<SqlClientNextVersion>8.0.0-preview1</SqlClientNextVersion>\n' > "${SQLCLIENT_VERSIONS_FILE}"
  run bash "${SCRIPT}"
  [ "$status" -eq 1 ]
  [[ "$output" == *"Expected exactly one SqlClientNextVersion"* ]]
}

@test "fails closed when the version file is missing" {
  rm "${SQLCLIENT_VERSIONS_FILE}"
  run bash "${SCRIPT}"
  [ "$status" -eq 1 ]
  [[ "$output" == *"Unable to read SqlClient version file"* ]]
}

@test "skips milestones without semantic versions" {
  export MILESTONE_TITLE="vNext"
  run bash "${SCRIPT}"
  [ "$status" -eq 0 ]
  [[ "$output" == *"not a semantic version; skipping"* ]]
}

@test "skips integration branch targets" {
  export BASE_REF="dev/feature"
  run bash "${SCRIPT}"
  [ "$status" -eq 0 ]
  [[ "$output" == *"integration branch 'dev/feature'; skipping"* ]]
}
