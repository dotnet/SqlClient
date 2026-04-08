#!/usr/bin/env bats
#################################################################################
# Licensed to the .NET Foundation under one or more agreements.                 #
# The .NET Foundation licenses this file to you under the MIT license.          #
# See the LICENSE file in the project root for more information.                #
#################################################################################
#
# Tests for cherry-pick-to-release.sh
#
# Run with:  bats .github/scripts/tests/cherry-pick-to-release.bats
#
# NOTE: These tests mock git and gh commands to validate the script's logic
# without requiring a real repository or GitHub API access.
#
# Dependencies: bats-core (https://github.com/bats-core/bats-core)
#
#################################################################################

# Path to the script under test (relative to repo root).
SCRIPT=".github/scripts/cherry-pick-to-release.sh"

# ── Helpers ──────────────────────────────────────────────────────────────────

setup() {
  # Create a directory for mock binaries that override real git/gh.
  STUB_DIR="$(mktemp -d)"
  export PATH="${STUB_DIR}:${PATH}"

  # Defaults — individual tests override as needed.
  export VERSION="7.0.1"
  export MERGE_COMMIT_SHA="abc123def456"
  export PR_NUMBER="42"
  export PR_TITLE="Fix connection timeout"
  export GH_TOKEN="fake-token"
  export GITHUB_REPOSITORY="dotnet/SqlClient"
  export GITHUB_OUTPUT="$(mktemp)"
}

teardown() {
  rm -rf "${STUB_DIR}"
  rm -f "${GITHUB_OUTPUT}"
}

# Write a mock 'git' script.  Each call to the mock appends a log line so
# tests can verify which git subcommands were executed and with what args.
write_git_mock() {
  local body="$1"
  cat > "${STUB_DIR}/git" <<STUB
#!/usr/bin/env bash
echo "GIT: \$*" >> "${STUB_DIR}/git.log"
${body}
STUB
  chmod +x "${STUB_DIR}/git"
}

# Write a mock 'gh' script.
write_gh_mock() {
  local body="$1"
  cat > "${STUB_DIR}/gh" <<STUB
#!/usr/bin/env bash
echo "GH: \$*" >> "${STUB_DIR}/gh.log"
${body}
STUB
  chmod +x "${STUB_DIR}/gh"
}

# ── --help flag ──────────────────────────────────────────────────────────────

@test "prints help text with --help" {
  run bash "${SCRIPT}" --help
  [ "$status" -eq 0 ]
  [[ "$output" == *"Cherry-picks a merge commit"* ]]
  [[ "$output" == *"REQUIRED ENVIRONMENT VARIABLES"* ]]
}

@test "prints help text with -h" {
  run bash "${SCRIPT}" -h
  [ "$status" -eq 0 ]
  [[ "$output" == *"Cherry-picks"* ]]
}

# ── Input validation ─────────────────────────────────────────────────────────

@test "fails when VERSION is unset" {
  unset VERSION
  run bash "${SCRIPT}"
  [ "$status" -ne 0 ]
  [[ "$output" == *"VERSION"* ]]
}

@test "fails when MERGE_COMMIT_SHA is unset" {
  unset MERGE_COMMIT_SHA
  run bash "${SCRIPT}"
  [ "$status" -ne 0 ]
  [[ "$output" == *"MERGE_COMMIT_SHA"* ]]
}

@test "fails when PR_NUMBER is unset" {
  unset PR_NUMBER
  run bash "${SCRIPT}"
  [ "$status" -ne 0 ]
  [[ "$output" == *"PR_NUMBER"* ]]
}

@test "fails when PR_TITLE is unset" {
  unset PR_TITLE
  run bash "${SCRIPT}"
  [ "$status" -ne 0 ]
  [[ "$output" == *"PR_TITLE"* ]]
}

@test "fails when GITHUB_REPOSITORY is unset" {
  unset GITHUB_REPOSITORY
  run bash "${SCRIPT}"
  [ "$status" -ne 0 ]
  [[ "$output" == *"GITHUB_REPOSITORY"* ]]
}

# ── Version parsing ─────────────────────────────────────────────────────────

@test "derives release/7.0 from version 7.0.1" {
  write_git_mock '
    if [[ "$1" == "fetch" ]]; then exit 0; fi
    if [[ "$1" == "cherry" ]]; then echo "+ abc123"; exit 0; fi
    if [[ "$1" == "checkout" ]]; then exit 0; fi
    if [[ "$1" == "rev-list" ]]; then echo "abc123 parent1"; exit 0; fi
    if [[ "$1" == "cherry-pick" ]]; then exit 0; fi
    if [[ "$1" == "push" ]]; then exit 0; fi
    exit 0
  '
  write_gh_mock '
    if [[ "$1" == "api" ]]; then echo "7.0.1"; exit 0; fi
    if [[ "$1" == "pr" ]]; then exit 0; fi
    exit 0
  '

  run bash "${SCRIPT}"
  [ "$status" -eq 0 ]
  # Verify the git fetch targeted release/7.0.
  grep -q "GIT: fetch origin release/7.0" "${STUB_DIR}/git.log"
  # Milestone lookup must use GET to avoid accidentally POSTing to the create endpoint.
  grep "GH: api repos/dotnet/SqlClient/milestones" "${STUB_DIR}/gh.log" | grep -q "\-\-method GET"
}

@test "derives release/8.0 from version 8.0.0" {
  export VERSION="8.0.0"
  write_git_mock '
    if [[ "$1" == "fetch" ]]; then exit 0; fi
    if [[ "$1" == "cherry" ]]; then echo "+ abc123"; exit 0; fi
    if [[ "$1" == "checkout" ]]; then exit 0; fi
    if [[ "$1" == "rev-list" ]]; then echo "abc123 parent1"; exit 0; fi
    if [[ "$1" == "cherry-pick" ]]; then exit 0; fi
    if [[ "$1" == "push" ]]; then exit 0; fi
    exit 0
  '
  write_gh_mock '
    if [[ "$1" == "api" ]]; then echo "8.0.0"; exit 0; fi
    if [[ "$1" == "pr" ]]; then exit 0; fi
    exit 0
  '

  run bash "${SCRIPT}"
  [ "$status" -eq 0 ]
  grep -q "GIT: fetch origin release/8.0" "${STUB_DIR}/git.log"
}

@test "fails on unparseable version" {
  export VERSION="bad"
  run bash "${SCRIPT}"
  [ "$status" -eq 1 ]
  [[ "$output" == *"Could not parse"* ]]
}

# ── Already-applied detection ───────────────────────────────────────────────

@test "exits cleanly when commit is already applied" {
  write_git_mock '
    if [[ "$1" == "fetch" ]]; then exit 0; fi
    # git cherry: "-" prefix means patch is already applied.
    if [[ "$1" == "cherry" ]]; then echo "- abc123def456"; exit 0; fi
    exit 0
  '

  run bash "${SCRIPT}"
  [ "$status" -eq 0 ]
  [[ "$output" == *"already applied"* ]]
  # Should NOT have attempted a cherry-pick.
  ! grep -q "GIT: cherry-pick" "${STUB_DIR}/git.log"
}

# ── Squash-merge detection (single parent) ──────────────────────────────────

@test "does not use --mainline for squash merges" {
  write_git_mock '
    if [[ "$1" == "fetch" ]]; then exit 0; fi
    if [[ "$1" == "cherry" ]]; then echo "+ abc123"; exit 0; fi
    if [[ "$1" == "checkout" ]]; then exit 0; fi
    # Single parent: rev-list outputs "sha parent1" (2 fields → 1 parent).
    if [[ "$1" == "rev-list" ]]; then echo "abc123def456 parent1"; exit 0; fi
    if [[ "$1" == "cherry-pick" ]]; then exit 0; fi
    if [[ "$1" == "push" ]]; then exit 0; fi
    exit 0
  '
  write_gh_mock '
    if [[ "$1" == "api" ]]; then echo "7.0.1"; exit 0; fi
    if [[ "$1" == "pr" ]]; then exit 0; fi
    exit 0
  '

  run bash "${SCRIPT}"
  [ "$status" -eq 0 ]
  [[ "$output" == *"single parent"* ]]
  # cherry-pick should NOT include --mainline.
  grep "GIT: cherry-pick" "${STUB_DIR}/git.log" | grep -qv "\-\-mainline"
}

# ── True merge detection (multiple parents) ─────────────────────────────────

@test "uses --mainline 1 for true merge commits" {
  write_git_mock '
    if [[ "$1" == "fetch" ]]; then exit 0; fi
    if [[ "$1" == "cherry" ]]; then echo "+ abc123"; exit 0; fi
    if [[ "$1" == "checkout" ]]; then exit 0; fi
    # Two parents: rev-list outputs "sha parent1 parent2" (3 fields → 2 parents).
    if [[ "$1" == "rev-list" ]]; then echo "abc123def456 parent1 parent2"; exit 0; fi
    if [[ "$1" == "cherry-pick" ]]; then exit 0; fi
    if [[ "$1" == "push" ]]; then exit 0; fi
    exit 0
  '
  write_gh_mock '
    if [[ "$1" == "api" ]]; then echo "7.0.1"; exit 0; fi
    if [[ "$1" == "pr" ]]; then exit 0; fi
    exit 0
  '

  run bash "${SCRIPT}"
  [ "$status" -eq 0 ]
  [[ "$output" == *"--mainline 1"* ]]
}

# ── Milestone lookup ────────────────────────────────────────────────────────

@test "warns when milestone does not exist" {
  write_git_mock '
    if [[ "$1" == "fetch" ]]; then exit 0; fi
    if [[ "$1" == "cherry" ]]; then echo "+ abc123"; exit 0; fi
    if [[ "$1" == "checkout" ]]; then exit 0; fi
    if [[ "$1" == "rev-list" ]]; then echo "abc123def456 parent1"; exit 0; fi
    if [[ "$1" == "cherry-pick" ]]; then exit 0; fi
    if [[ "$1" == "push" ]]; then exit 0; fi
    if [[ "$1" == "config" ]]; then exit 0; fi
    exit 0
  '
  # gh api returns a milestone that does NOT match VERSION (7.0.1).
  write_gh_mock '
    if [[ "$1" == "api" ]]; then echo "6.0.0"; exit 0; fi
    if [[ "$1" == "pr" ]]; then exit 0; fi
    exit 0
  '

  run bash "${SCRIPT}"
  [ "$status" -eq 0 ]
  [[ "$output" == *"does not exist"* ]]
  # Milestone lookup must use GET to avoid accidentally POSTing to the create endpoint.
  grep "GH: api repos/dotnet/SqlClient/milestones" "${STUB_DIR}/gh.log" | grep -q "\-\-method GET"
}

# ── Conflict handling ───────────────────────────────────────────────────────

@test "creates CONFLICTS PR when cherry-pick fails" {
  write_git_mock '
    if [[ "$1" == "fetch" ]]; then exit 0; fi
    if [[ "$1" == "cherry" ]]; then echo "+ abc123"; exit 0; fi
    if [[ "$1" == "checkout" ]]; then exit 0; fi
    if [[ "$1" == "rev-list" ]]; then echo "abc123def456 parent1"; exit 0; fi
    # cherry-pick fails with conflicts.
    if [[ "$1" == "cherry-pick" ]]; then
      if [[ "$2" == "--abort" ]]; then exit 0; fi
      exit 1
    fi
    if [[ "$1" == "commit" ]]; then exit 0; fi
    if [[ "$1" == "push" ]]; then exit 0; fi
    exit 0
  '
  write_gh_mock '
    if [[ "$1" == "api" ]]; then echo "7.0.1"; exit 0; fi
    if [[ "$1" == "pr" ]]; then exit 0; fi
    exit 0
  '

  run bash "${SCRIPT}"
  [ "$status" -eq 0 ]
  [[ "$output" == *"failed due to conflicts"* ]]
  # Should have called cherry-pick --abort.
  grep -q "GIT: cherry-pick --abort" "${STUB_DIR}/git.log"
  # Should have created an empty commit.
  grep -q "GIT: commit --allow-empty" "${STUB_DIR}/git.log"
}

@test "conflict PR body contains real newlines, not literal backslash-n" {
  write_git_mock '
    if [[ "$1" == "fetch" ]]; then exit 0; fi
    if [[ "$1" == "cherry" ]]; then echo "+ abc123"; exit 0; fi
    if [[ "$1" == "checkout" ]]; then exit 0; fi
    if [[ "$1" == "rev-list" ]]; then echo "abc123def456 parent1"; exit 0; fi
    if [[ "$1" == "cherry-pick" ]]; then
      if [[ "$2" == "--abort" ]]; then exit 0; fi
      exit 1
    fi
    if [[ "$1" == "commit" ]]; then exit 0; fi
    if [[ "$1" == "push" ]]; then exit 0; fi
    exit 0
  '
  # Capture the full --body argument to a file for inspection.
  write_gh_mock '
    if [[ "$1" == "api" ]]; then echo "7.0.1"; exit 0; fi
    if [[ "$1" == "pr" && "$2" == "create" ]]; then
      while [[ $# -gt 0 ]]; do
        if [[ "$1" == "--body" ]]; then
          printf "%s" "$2" > "'"${STUB_DIR}"'/pr-body.txt"
          break
        fi
        shift
      done
      exit 0
    fi
    exit 0
  '

  run bash "${SCRIPT}"
  [ "$status" -eq 0 ]

  # The body file must exist (gh pr create was called with --body).
  [ -f "${STUB_DIR}/pr-body.txt" ]

  local body
  body="$(cat "${STUB_DIR}/pr-body.txt")"

  # Must NOT contain literal two-character sequence '\n'.
  [[ "$body" != *'\\n'* ]]
  # Each command in the code block must be on its own line.
  [[ "$body" == *$'\ngit fetch origin\n'* ]]
  [[ "$body" == *$'\ngit checkout dev/automation/pr-42-to-7.0.1\n'* ]]
  [[ "$body" == *$'\ngit cherry-pick abc123def456\n'* ]]
  [[ "$body" == *$'\n# resolve conflicts\n'* ]]
  [[ "$body" == *$'\ngit push origin dev/automation/pr-42-to-7.0.1 --force\n'* ]]
}
