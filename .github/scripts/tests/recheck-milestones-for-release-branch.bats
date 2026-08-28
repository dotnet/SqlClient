#!/usr/bin/env bats
#################################################################################
# Licensed to the .NET Foundation under one or more agreements.                 #
# The .NET Foundation licenses this file to you under the MIT license.          #
# See the LICENSE file in the project root for more information.                #
#################################################################################
#
# Tests for recheck-milestones-for-release-branch.sh
#
# Run with:  bats .github/scripts/tests/recheck-milestones-for-release-branch.bats
#
# Dependencies: bats-core (https://github.com/bats-core/bats-core)
#
#################################################################################

# Path to the script under test (relative to repo root).
SCRIPT=".github/scripts/recheck-milestones-for-release-branch.sh"

# ── Helpers ──────────────────────────────────────────────────────────────────

setup() {
  STUB_DIR="$(mktemp -d)"
  export PATH="${STUB_DIR}:${PATH}"

  # Defaults — individual tests override as needed.
  export RELEASE_BRANCH="release/7.1"
  export DEFAULT_BRANCH="main"
  export WORKFLOW_FILE="check-milestone.yml"
  export GITHUB_REPOSITORY="dotnet/SqlClient"
  export GH_TOKEN="fake-token"

  # Open PRs as "<number> <headSha> <milestone>", one per line.
  mock_gh "100 aaa111 7.1.0
101 bbb222 8.0.0-preview1
102 ccc333 7.1.0-preview3"
}

teardown() {
  rm -rf "${STUB_DIR}"
}

# Install a 'gh' mock. $1 is the 'pr list' output; the run lookup returns
# "run-<sha>" and 'run rerun' succeeds unless RERUN_FAILS is set.
mock_gh() {
  cat > "${STUB_DIR}/gh" <<MOCK
#!/usr/bin/env bash
echo "GH: \$*" >> "${STUB_DIR}/gh.log"
case "\$1" in
  pr)
    printf '%s\n' '${1}'
    ;;
  api)
    if [[ -n "\${NO_RUN_FOUND:-}" ]]; then
      exit 0
    fi
    sha="\$(sed -n 's/.*head_sha=\([^&]*\).*/\1/p' <<< "\$2")"
    echo "run-\${sha}"
    ;;
  run)
    [[ -z "\${RERUN_FAILS:-}" ]] || exit 1
    ;;
esac
MOCK
  chmod +x "${STUB_DIR}/gh"
}

# Install a 'gh' mock whose 'pr list' call fails.
mock_pr_list_failure() {
  cat > "${STUB_DIR}/gh" <<'MOCK'
#!/usr/bin/env bash
echo "HTTP 403: rate limit exceeded" >&2
exit 1
MOCK
  chmod +x "${STUB_DIR}/gh"
}

# ── --help flag ──────────────────────────────────────────────────────────────

@test "prints help text with --help" {
  run bash "${SCRIPT}" --help
  [ "$status" -eq 0 ]
  [[ "$output" == *"REQUIRED ENVIRONMENT VARIABLES"* ]]
}

# ── Input validation ─────────────────────────────────────────────────────────

@test "fails when RELEASE_BRANCH is unset" {
  unset RELEASE_BRANCH
  run bash "${SCRIPT}"
  [ "$status" -ne 0 ]
  [[ "$output" == *"RELEASE_BRANCH"* ]]
}

@test "fails when DEFAULT_BRANCH is unset" {
  unset DEFAULT_BRANCH
  run bash "${SCRIPT}"
  [ "$status" -ne 0 ]
  [[ "$output" == *"DEFAULT_BRANCH"* ]]
}

@test "fails when WORKFLOW_FILE is unset" {
  unset WORKFLOW_FILE
  run bash "${SCRIPT}"
  [ "$status" -ne 0 ]
  [[ "$output" == *"WORKFLOW_FILE"* ]]
}

@test "fails when GITHUB_REPOSITORY is unset" {
  unset GITHUB_REPOSITORY
  run bash "${SCRIPT}"
  [ "$status" -ne 0 ]
  [[ "$output" == *"GITHUB_REPOSITORY"* ]]
}

# ── Branch name parsing ──────────────────────────────────────────────────────

@test "skips branches that are not release/<major>.<minor>" {
  export RELEASE_BRANCH="dev/paul/some-feature"
  run bash "${SCRIPT}"
  [ "$status" -eq 0 ]
  [[ "$output" == *"nothing to reconcile"* ]]
  [ ! -f "${STUB_DIR}/gh.log" ]
}

@test "skips a release branch with a patch component" {
  export RELEASE_BRANCH="release/7.1.0"
  run bash "${SCRIPT}"
  [ "$status" -eq 0 ]
  [[ "$output" == *"nothing to reconcile"* ]]
}

# ── Matching and re-running ──────────────────────────────────────────────────

@test "re-runs only the PRs whose milestone matches the new branch" {
  run bash "${SCRIPT}"
  [ "$status" -eq 0 ]
  [[ "$output" == *"PR #100"* ]]
  [[ "$output" == *"PR #102"* ]]
  [[ "$output" != *"PR #101"* ]]
  [[ "$output" == *"Re-checked 2 pull request(s)"* ]]
}

@test "queries open PRs against the default branch" {
  run bash "${SCRIPT}"
  [ "$status" -eq 0 ]
  grep -qF "GH: pr list --repo dotnet/SqlClient --base main --state open" "${STUB_DIR}/gh.log"
}

@test "looks up the run by the PR head sha and re-runs it" {
  run bash "${SCRIPT}"
  [ "$status" -eq 0 ]
  grep -qF "head_sha=aaa111" "${STUB_DIR}/gh.log"
  grep -qF "GH: run rerun run-aaa111 --repo dotnet/SqlClient" "${STUB_DIR}/gh.log"
}

@test "reports when no open PR carries a matching milestone" {
  export RELEASE_BRANCH="release/6.1"
  run bash "${SCRIPT}"
  [ "$status" -eq 0 ]
  [[ "$output" == *"No open PR targeting 'main' carries a 6.1.* milestone"* ]]
}

@test "ignores PRs whose milestone is not major.minor.patch" {
  mock_gh "200 ddd444 vNext"
  run bash "${SCRIPT}"
  [ "$status" -eq 0 ]
  [[ "$output" == *"No open PR"* ]]
}

# ── Failure handling ─────────────────────────────────────────────────────────

@test "warns and fails when a PR has no run to re-run" {
  export NO_RUN_FOUND=1
  run bash "${SCRIPT}"
  [ "$status" -eq 1 ]
  [[ "$output" == *"has no milestone check run to re-run"* ]]
  [[ "$output" == *"2 of 2 affected pull requests"* ]]
}

@test "warns and fails when a re-run cannot be started" {
  export RERUN_FAILS=1
  run bash "${SCRIPT}"
  [ "$status" -eq 1 ]
  [[ "$output" == *"Could not re-run the milestone check for PR #100"* ]]
}

@test "fails when the PR listing API call fails" {
  mock_pr_list_failure
  run bash "${SCRIPT}"
  [ "$status" -eq 1 ]
  [[ "$output" == *"Unable to list open pull requests"* ]]
}
