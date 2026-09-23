#!/usr/bin/env bats
#################################################################################
# Licensed to the .NET Foundation under one or more agreements.                 #
# The .NET Foundation licenses this file to you under the MIT license.          #
# See the LICENSE file in the project root for more information.                #
#################################################################################
#
# Tests for create-backport-issue.sh
#
# Run with:  bats .github/scripts/tests/create-backport-issue.bats
#
# Dependencies: bats-core (https://github.com/bats-core/bats-core)
#
#################################################################################

SCRIPT=".github/scripts/create-backport-issue.sh"

setup() {
  export EVENT_LABEL="Hotfix 7.1.1"
  export PARENT_ISSUE_NUMBER="4714"
  export GH_TOKEN="fake-token"
  export GITHUB_REPOSITORY="dotnet/SqlClient"

  STUB_DIR="$(mktemp -d)"
  export PATH="${STUB_DIR}:${PATH}"
}

teardown() {
  rm -rf "${STUB_DIR}"
}

# Writes a 'gh' stub into STUB_DIR that dispatches based on argv, so each test
# only needs to describe the handful of calls it cares about. The real script
# always reads these responses through a '--jq' filter, so the fixtures below
# are given already in the *post-jq* shape the script expects, not raw API JSON.
#
#   $1: sub_issues TSV rows (one per line: "<milestone>\t<title>\t<number>")
#   $2: milestone titles, one per line (as if from --jq '.[].title')
#   $3: parent issue view JSON ({title,labels})
#   $4: 'gh issue create' output (the created issue URL)
#   $5: issue GET response for the child (JSON with .id)
stub_gh() {
  local sub_issues_tsv="$1"
  local milestone_titles="$2"
  local parent_json="$3"
  local created_url="$4"
  local child_issue_json="$5"

  printf '%s' "${sub_issues_tsv}" > "${STUB_DIR}/sub_issues.tsv"
  printf '%s' "${milestone_titles}" > "${STUB_DIR}/milestones.txt"

  cat > "${STUB_DIR}/gh" <<STUB
#!/usr/bin/env bash
set -euo pipefail

if [[ "\$1" == "api" && "\$2" == repos/*/issues/*/sub_issues && "\${*}" != *"--method POST"* ]]; then
  cat "${STUB_DIR}/sub_issues.tsv"
  exit 0
fi

if [[ "\$1" == "api" && "\$2" == repos/*/milestones ]]; then
  cat "${STUB_DIR}/milestones.txt"
  exit 0
fi

if [[ "\$1" == "issue" && "\$2" == "view" ]]; then
  echo '${parent_json}'
  exit 0
fi

if [[ "\$1" == "issue" && "\$2" == "create" ]]; then
  echo '${created_url}'
  exit 0
fi

if [[ "\$1" == "api" && "\$2" == repos/*/issues/* && "\${*}" != *"sub_issues"* ]]; then
  echo '${child_issue_json}'
  exit 0
fi

if [[ "\$1" == "api" && "\$2" == repos/*/issues/*/sub_issues && "\${*}" == *"--method POST"* ]]; then
  echo "SUB_ISSUE_LINK_CALLED" >> "${STUB_DIR}/sub_issue_calls.log"
  exit 0
fi

echo "unhandled gh invocation: \$*" >&2
exit 1
STUB
  chmod +x "${STUB_DIR}/gh"
}

# ── --help flag ──────────────────────────────────────────────────────────────

@test "prints help text with --help" {
  run bash "${SCRIPT}" --help
  [ "$status" -eq 0 ]
  [[ "$output" == *"REQUIRED ENVIRONMENT VARIABLES"* ]]
}

# ── Input validation ─────────────────────────────────────────────────────────

@test "fails when EVENT_LABEL is unset" {
  unset EVENT_LABEL
  run bash "${SCRIPT}"
  [ "$status" -ne 0 ]
  [[ "$output" == *"EVENT_LABEL"* ]]
}

@test "fails when PARENT_ISSUE_NUMBER is unset" {
  unset PARENT_ISSUE_NUMBER
  run bash "${SCRIPT}"
  [ "$status" -ne 0 ]
  [[ "$output" == *"PARENT_ISSUE_NUMBER"* ]]
}

# ── Label validation ─────────────────────────────────────────────────────────

@test "skips non-hotfix labels" {
  export EVENT_LABEL="bug"
  run bash "${SCRIPT}"
  [ "$status" -eq 0 ]
  [[ "$output" == *"not a valid"* ]]
}

@test "skips malformed hotfix labels" {
  export EVENT_LABEL="Hotfix 7.1"
  run bash "${SCRIPT}"
  [ "$status" -eq 0 ]
  [[ "$output" == *"not a valid"* ]]
}

# ── Duplicate detection ──────────────────────────────────────────────────────

@test "skips when a sub-issue is already milestoned for this version" {
  stub_gh "$(printf '7.1.1\t[7.1.1] something\t4900')" "" "" "" ""
  run bash "${SCRIPT}"
  [ "$status" -eq 0 ]
  [[ "$output" == *"already exists"* ]]
}

@test "skips when a sub-issue already has the child title prefix" {
  stub_gh "$(printf 'NONE\t[7.1.1] something\t4900')" "" "" "" ""
  run bash "${SCRIPT}"
  [ "$status" -eq 0 ]
  [[ "$output" == *"already exists"* ]]
}

@test "does not skip when existing sub-issues are for a different version" {
  stub_gh "$(printf '7.0.5\t[7.0.5] other\t4800')" \
    "$(printf '7.1.1')" \
    '{"title":"Parent title","labels":[{"name":"bug"},{"name":"Hotfix 7.1.1"}]}' \
    "https://github.com/dotnet/SqlClient/issues/4901" \
    '{"id":123456789}'
  run bash "${SCRIPT}"
  [ "$status" -eq 0 ]
  [[ "$output" == *"Created backport issue #4901"* ]]
  [[ "$output" == *"Linked #4901 as a sub-issue of #4714"* ]]
  [ -f "${STUB_DIR}/sub_issue_calls.log" ]
}

# ── Happy path ────────────────────────────────────────────────────────────────

@test "creates a backport issue and links it as a sub-issue" {
  stub_gh "" \
    "$(printf '7.1.1')" \
    '{"title":"Parent title","labels":[{"name":"bug"},{"name":"Hotfix 7.1.1"}]}' \
    "https://github.com/dotnet/SqlClient/issues/4901" \
    '{"id":123456789}'

  run bash "${SCRIPT}"
  [ "$status" -eq 0 ]
  [[ "$output" == *"Milestone '7.1.1' found."* ]]
  [[ "$output" == *"Created backport issue #4901"* ]]
  [[ "$output" == *"Linked #4901 as a sub-issue of #4714"* ]]
  [ -f "${STUB_DIR}/sub_issue_calls.log" ]
}

@test "creates a backport issue without a milestone when it does not exist" {
  stub_gh "" \
    "" \
    '{"title":"Parent title","labels":[]}' \
    "https://github.com/dotnet/SqlClient/issues/4902" \
    '{"id":987654321}'

  run bash "${SCRIPT}"
  [ "$status" -eq 0 ]
  [[ "$output" == *"does not exist"* ]]
  [[ "$output" == *"Created backport issue #4902"* ]]
}
