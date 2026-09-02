#!/usr/bin/env bats
#################################################################################
# Licensed to the .NET Foundation under one or more agreements.                 #
# The .NET Foundation licenses this file to you under the MIT license.          #
# See the LICENSE file in the project root for more information.                #
#################################################################################
#
# Tests for check-milestone-branch.sh
#
# Run with:  bats .github/scripts/tests/check-milestone-branch.bats
#
# Dependencies: bats-core (https://github.com/bats-core/bats-core)
#
#################################################################################

# Path to the script under test (relative to repo root).
SCRIPT=".github/scripts/check-milestone-branch.sh"

# ── Helpers ──────────────────────────────────────────────────────────────────

setup() {
  STUB_DIR="$(mktemp -d)"
  export PATH="${STUB_DIR}:${PATH}"

  # Defaults — individual tests override as needed.
  export MILESTONE_TITLE="7.1.0"
  export BASE_REF="main"
  export DEFAULT_BRANCH="main"
  export GITHUB_REPOSITORY="dotnet/SqlClient"
  export GH_TOKEN="fake-token"
  export MOCK_OPEN_MILESTONES=$'7.1.0\n8.0.0-preview1\n8.0.0-preview2\n8.0.0'

  mock_release_branches "release/6.1" "release/7.0"
}

teardown() {
  rm -rf "${STUB_DIR}"
}

# Install a 'gh' mock that reports the given release branches.
mock_release_branches() {
  local refs=""
  local branch
  for branch in "$@"; do
    refs+="refs/heads/${branch}"$'\n'
  done

  cat > "${STUB_DIR}/gh" <<MOCK
#!/usr/bin/env bash
echo "GH: \$*" >> "${STUB_DIR}/gh.log"
if [[ "\$*" == *"/milestones"* ]]; then
  printf '%s' "\${MOCK_OPEN_MILESTONES}"
else
  printf '%s' '${refs}'
fi
MOCK
  chmod +x "${STUB_DIR}/gh"
}

# Install a 'gh' mock that fails, simulating an API error.
mock_gh_failure() {
  cat > "${STUB_DIR}/gh" <<MOCK
#!/usr/bin/env bash
echo "GH: \$*" >> "${STUB_DIR}/gh.log"
echo "HTTP 403: rate limit exceeded" >&2
exit 1
MOCK
  chmod +x "${STUB_DIR}/gh"
}

# Install a 'gh' mock that lists branches but fails when milestones are queried.
mock_milestone_failure() {
  cat > "${STUB_DIR}/gh" <<MOCK
#!/usr/bin/env bash
echo "GH: \$*" >> "${STUB_DIR}/gh.log"
if [[ "\$*" == *"/milestones"* ]]; then
  echo "HTTP 403: resource not accessible by integration" >&2
  exit 1
fi
printf '%s' 'refs/heads/release/6.1
refs/heads/release/7.0
'
MOCK
  chmod +x "${STUB_DIR}/gh"
}

# ── --help flag ──────────────────────────────────────────────────────────────

@test "prints help text with --help" {
  run bash "${SCRIPT}" --help
  [ "$status" -eq 0 ]
  [[ "$output" == *"VALIDATION MATRIX"* ]]
  [[ "$output" == *"REQUIRED ENVIRONMENT VARIABLES"* ]]
}

@test "prints help text with -h" {
  run bash "${SCRIPT}" -h
  [ "$status" -eq 0 ]
  [[ "$output" == *"VALIDATION MATRIX"* ]]
}

# ── Input validation ─────────────────────────────────────────────────────────

@test "fails when MILESTONE_TITLE is unset" {
  unset MILESTONE_TITLE
  run bash "${SCRIPT}"
  [ "$status" -ne 0 ]
  [[ "$output" == *"MILESTONE_TITLE"* ]]
}

@test "fails when BASE_REF is unset" {
  unset BASE_REF
  run bash "${SCRIPT}"
  [ "$status" -ne 0 ]
  [[ "$output" == *"BASE_REF"* ]]
}

@test "fails when DEFAULT_BRANCH is unset" {
  unset DEFAULT_BRANCH
  run bash "${SCRIPT}"
  [ "$status" -ne 0 ]
  [[ "$output" == *"DEFAULT_BRANCH"* ]]
}

@test "fails when GITHUB_REPOSITORY is unset" {
  unset GITHUB_REPOSITORY
  run bash "${SCRIPT}"
  [ "$status" -ne 0 ]
  [[ "$output" == *"GITHUB_REPOSITORY"* ]]
}

# ── Default branch targets ───────────────────────────────────────────────────

@test "passes when in-development milestone targets the default branch" {
  export MILESTONE_TITLE="7.1.0"
  export BASE_REF="main"
  run bash "${SCRIPT}"
  [ "$status" -eq 0 ]
  [[ "$output" == *"::notice::"* ]]
  [[ "$output" == *"still in development"* ]]
}

@test "fails when a later milestone targets the default branch before the active release branch is cut" {
  export MILESTONE_TITLE="8.0.0-preview1"
  export BASE_REF="main"
  run bash "${SCRIPT}"
  [ "$status" -eq 1 ]
  [[ "$output" == *"7.1.0"* ]]
  [[ "$output" == *"release/7.1"* ]]
}

@test "passes when a later milestone targets the default branch after the active release branch is cut" {
  mock_release_branches "release/6.1" "release/7.0" "release/7.1"
  export MILESTONE_TITLE="8.0.0-preview1"
  export BASE_REF="main"
  run bash "${SCRIPT}"
  [ "$status" -eq 0 ]
  [[ "$output" == *"still in development"* ]]
}

@test "fails when a serviced milestone targets the default branch" {
  export MILESTONE_TITLE="7.0.3"
  export BASE_REF="main"
  run bash "${SCRIPT}"
  [ "$status" -eq 1 ]
  [[ "$output" == *"::error::"* ]]
  [[ "$output" == *"release/7.0"* ]]
  [[ "$output" == *"Hotfix 7.0.3"* ]]
}

@test "honours a non-'main' default branch" {
  export MILESTONE_TITLE="7.1.0"
  export BASE_REF="master"
  export DEFAULT_BRANCH="master"
  run bash "${SCRIPT}"
  [ "$status" -eq 0 ]
  [[ "$output" == *"targeting 'master' is correct"* ]]
}

# ── API invocation ───────────────────────────────────────────────────────────

@test "queries the release refs endpoint with the expected arguments" {
  export MILESTONE_TITLE="7.1.0"
  export BASE_REF="main"
  run bash "${SCRIPT}"
  [ "$status" -eq 0 ]
  grep -qF "GH: api repos/dotnet/SqlClient/git/matching-refs/heads/release/ --jq .[].ref" "${STUB_DIR}/gh.log"
  grep -qF "GH: api --paginate repos/dotnet/SqlClient/milestones?state=open&per_page=100 --jq .[].title" "${STUB_DIR}/gh.log"
}

@test "does not call the API when the PR targets a release branch" {
  export MILESTONE_TITLE="7.0.3"
  export BASE_REF="release/7.0"
  run bash "${SCRIPT}"
  [ "$status" -eq 0 ]
  [ ! -f "${STUB_DIR}/gh.log" ]
}

# ── Release branch targets ───────────────────────────────────────────────────

@test "passes when the milestone matches the target release branch" {
  export MILESTONE_TITLE="7.0.3"
  export BASE_REF="release/7.0"
  run bash "${SCRIPT}"
  [ "$status" -eq 0 ]
  [[ "$output" == *"matches target branch 'release/7.0'"* ]]
}

@test "fails when the milestone belongs to a different release branch" {
  export MILESTONE_TITLE="7.0.3"
  export BASE_REF="release/6.1"
  run bash "${SCRIPT}"
  [ "$status" -eq 1 ]
  [[ "$output" == *"::error::"* ]]
  [[ "$output" == *"belongs to 'release/7.0'"* ]]
}

@test "fails when an in-development milestone targets a release branch" {
  export MILESTONE_TITLE="7.1.0"
  export BASE_REF="release/7.0"
  run bash "${SCRIPT}"
  [ "$status" -eq 1 ]
  [[ "$output" == *"belongs to 'release/7.1'"* ]]
}

@test "passes when a newly cut release branch matches the milestone" {
  mock_release_branches "release/6.1" "release/7.0" "release/7.1"
  export MILESTONE_TITLE="7.1.0"
  export BASE_REF="release/7.1"
  run bash "${SCRIPT}"
  [ "$status" -eq 0 ]
  [[ "$output" == *"matches target branch 'release/7.1'"* ]]
}

@test "fails on the default branch once the release branch is cut" {
  mock_release_branches "release/6.1" "release/7.0" "release/7.1"
  export MILESTONE_TITLE="7.1.0"
  export BASE_REF="main"
  run bash "${SCRIPT}"
  [ "$status" -eq 1 ]
  [[ "$output" == *"release/7.1"* ]]
}

# ── Skipped cases ────────────────────────────────────────────────────────────

@test "skips integration branch targets" {
  export MILESTONE_TITLE="7.0.3"
  export BASE_REF="dev/paul/some-feature"
  run bash "${SCRIPT}"
  [ "$status" -eq 0 ]
  [[ "$output" == *"integration branch"* ]]
}

@test "skips milestones that are not major.minor.patch" {
  export MILESTONE_TITLE="vNext"
  export BASE_REF="main"
  run bash "${SCRIPT}"
  [ "$status" -eq 0 ]
  [[ "$output" == *"not in 'major.minor.patch' form"* ]]
}

@test "skips two-part milestone titles" {
  export MILESTONE_TITLE="1.0 Hotfix 2"
  export BASE_REF="main"
  run bash "${SCRIPT}"
  [ "$status" -eq 0 ]
  [[ "$output" == *"not in 'major.minor.patch' form"* ]]
}

# ── API failures ─────────────────────────────────────────────────────────────

@test "fails when the branch listing API call fails" {
  mock_gh_failure
  export MILESTONE_TITLE="7.0.3"
  export BASE_REF="main"
  run bash "${SCRIPT}"
  [ "$status" -eq 1 ]
  [[ "$output" == *"Unable to list release branches"* ]]
}

@test "fails when the milestone listing API call fails" {
  mock_milestone_failure
  export MILESTONE_TITLE="7.1.0"
  export BASE_REF="main"
  run bash "${SCRIPT}"
  [ "$status" -eq 1 ]
  [[ "$output" == *"Unable to list open milestones"* ]]
}
