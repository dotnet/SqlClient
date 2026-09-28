#!/usr/bin/env bats
#################################################################################
# Licensed to the .NET Foundation under one or more agreements.                 #
# The .NET Foundation licenses this file to you under the MIT license.          #
# See the LICENSE file in the project root for more information.                #
#################################################################################
#
# Tests for recheck-milestones-on-version-change.sh
#
# Run with: bats .github/scripts/tests/recheck-milestones-on-version-change.bats
#
#################################################################################

SCRIPT=".github/scripts/recheck-milestones-on-version-change.sh"

setup() {
  STUB_DIR="$(mktemp -d)"
  export STUB_DIR
  export PATH="${STUB_DIR}:${PATH}"
  export BASE_BRANCH="main"
  export GITHUB_REPOSITORY="dotnet/SqlClient"
  export GH_TOKEN="fake-token"
  export SQLCLIENT_VERSIONS_FILE="${STUB_DIR}/Versions.props"
  export MOCK_CHECK_RUNS_FAIL="false"
  export MOCK_PULL_REQUESTS='[{"number":1,"headRefOid":"abc123","milestone":{"title":"8.0.0-preview2"}},{"number":2,"headRefOid":"def456","milestone":{"title":"8.0.0-preview1"}},{"number":3,"headRefOid":"ghi789","milestone":{"title":"vNext"}},{"number":4,"headRefOid":"jkl012","milestone":null}]'
  printf '<SqlClientNextVersion>8.0.0-preview2</SqlClientNextVersion>\n' > "${SQLCLIENT_VERSIONS_FILE}"

  cat > "${STUB_DIR}/gh" <<'MOCK'
#!/usr/bin/env bash
if [[ "$*" == *"pr list"* ]]; then
  printf '%s' "${MOCK_PULL_REQUESTS}"
elif [[ "$*" == *"/check-runs"* ]]; then
  if [[ "${MOCK_CHECK_RUNS_FAIL}" == "true" ]]; then
    echo "HTTP 403: insufficient permissions" >&2
    exit 1
  fi
  cat >> "${STUB_DIR}/check-runs.jsonl"
  printf '\n' >> "${STUB_DIR}/check-runs.jsonl"
else
  echo "Unexpected gh call: $*" >&2
  exit 1
fi
MOCK
  chmod +x "${STUB_DIR}/gh"
}

teardown() {
  rm -rf "${STUB_DIR}"
}

@test "creates fresh success and failure checks on PR head commits" {
  run bash "${SCRIPT}"
  [ "$status" -eq 0 ]
  [[ "$output" == *"PR #1: success"* ]]
  [[ "$output" == *"PR #2: failure"* ]]
  [ "$(jq -s 'length' "${STUB_DIR}/check-runs.jsonl")" -eq 2 ]
  [ "$(jq -s '[.[] | select(.head_sha == "abc123" and .conclusion == "success")] | length' "${STUB_DIR}/check-runs.jsonl")" -eq 1 ]
  [ "$(jq -s '[.[] | select(.head_sha == "def456" and .conclusion == "failure" and (.output.summary | contains("8.0.0-preview1") and contains("8.0.0-preview2")))] | length' "${STUB_DIR}/check-runs.jsonl")" -eq 1 ]
}

@test "skips PRs without semantic version milestones" {
  export MOCK_PULL_REQUESTS='[{"number":3,"headRefOid":"ghi789","milestone":{"title":"vNext"}},{"number":4,"headRefOid":"jkl012","milestone":null}]'
  run bash "${SCRIPT}"
  [ "$status" -eq 0 ]
  [[ "$output" == *"No open semantic-version milestone PRs target 'main'"* ]]
  [ ! -f "${STUB_DIR}/check-runs.jsonl" ]
}

@test "fails when GitHub rejects fresh check creation" {
  export MOCK_CHECK_RUNS_FAIL="true"
  run bash "${SCRIPT}"
  [ "$status" -eq 1 ]
  [[ "$output" == *"Unable to create a fresh version check for PR #1"* ]]
}
