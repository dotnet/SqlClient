#!/usr/bin/env bats
#################################################################################
# Licensed to the .NET Foundation under one or more agreements.                 #
# The .NET Foundation licenses this file to you under the MIT license.          #
# See the LICENSE file in the project root for more information.                #
#################################################################################
#
# Tests for extract-hotfix-versions.sh
#
# Run with:  bats .github/scripts/tests/extract-hotfix-versions.bats
#
# Dependencies: bats-core (https://github.com/bats-core/bats-core)
#
#################################################################################

# Path to the script under test (relative to repo root).
SCRIPT=".github/scripts/extract-hotfix-versions.sh"

# ── Helpers ──────────────────────────────────────────────────────────────────

setup() {
  # Create a temporary GITHUB_OUTPUT file for each test.
  export GITHUB_OUTPUT
  GITHUB_OUTPUT="$(mktemp)"

  # Defaults — individual tests override as needed.
  export EVENT_ACTION="closed"
  export EVENT_LABEL=""
  export PR_NUMBER="42"
  export GH_TOKEN="fake-token"
}

teardown() {
  rm -f "${GITHUB_OUTPUT}"
}

# Read the 'versions' output written to GITHUB_OUTPUT.
get_versions() {
  grep '^versions=' "${GITHUB_OUTPUT}" | head -1 | cut -d= -f2-
}

# ── --help flag ──────────────────────────────────────────────────────────────

@test "prints help text with --help" {
  run bash "${SCRIPT}" --help
  [ "$status" -eq 0 ]
  [[ "$output" == *"Parses"* ]]
  [[ "$output" == *"Required environment variables"* ]]
}

@test "prints help text with -h" {
  run bash "${SCRIPT}" -h
  [ "$status" -eq 0 ]
  [[ "$output" == *"Parses"* ]]
}

# ── Input validation ─────────────────────────────────────────────────────────

@test "fails when LABELS is unset" {
  unset LABELS
  run bash "${SCRIPT}"
  [ "$status" -ne 0 ]
  [[ "$output" == *"LABELS"* ]]
}

@test "fails when EVENT_ACTION is unset" {
  export LABELS="Hotfix 7.0.1"
  unset EVENT_ACTION
  run bash "${SCRIPT}"
  [ "$status" -ne 0 ]
  [[ "$output" == *"EVENT_ACTION"* ]]
}

@test "fails when PR_NUMBER is unset" {
  export LABELS="Hotfix 7.0.1"
  unset PR_NUMBER
  run bash "${SCRIPT}"
  [ "$status" -ne 0 ]
  [[ "$output" == *"PR_NUMBER"* ]]
}

# ── Closed event: single label ──────────────────────────────────────────────

@test "closed event: extracts single Hotfix label" {
  export LABELS="Hotfix 7.0.1"
  run bash "${SCRIPT}"
  [ "$status" -eq 0 ]
  [ "$(get_versions)" = '["7.0.1"]' ]
}

@test "closed event: ignores non-hotfix labels" {
  export LABELS="bug,Hotfix 7.0.1,enhancement"
  run bash "${SCRIPT}"
  [ "$status" -eq 0 ]
  [ "$(get_versions)" = '["7.0.1"]' ]
}

# ── Closed event: multiple labels ───────────────────────────────────────────

@test "closed event: extracts multiple Hotfix labels" {
  export LABELS="Hotfix 7.0.1,Hotfix 8.0.0"
  run bash "${SCRIPT}"
  [ "$status" -eq 0 ]
  [ "$(get_versions)" = '["7.0.1","8.0.0"]' ]
}

@test "closed event: extracts hotfix labels mixed with other labels" {
  export LABELS="bug,Hotfix 7.0.1,enhancement,Hotfix 8.0.0,docs"
  run bash "${SCRIPT}"
  [ "$status" -eq 0 ]
  [ "$(get_versions)" = '["7.0.1","8.0.0"]' ]
}

# ── Closed event: malformed labels ──────────────────────────────────────────

@test "closed event: rejects malformed Hotfix labels (no patch)" {
  export LABELS="Hotfix 7.0"
  run bash "${SCRIPT}"
  [ "$status" -eq 1 ]
  [[ "$output" == *"No valid"* ]]
}

@test "closed event: rejects Hotfix label with text suffix" {
  export LABELS="Hotfix 7.0.1-beta"
  run bash "${SCRIPT}"
  [ "$status" -eq 1 ]
  [[ "$output" == *"No valid"* ]]
}

@test "closed event: rejects Hotfix label with non-numeric version" {
  export LABELS="Hotfix abc"
  run bash "${SCRIPT}"
  [ "$status" -eq 1 ]
  [[ "$output" == *"No valid"* ]]
}

@test "closed event: fails when no labels present" {
  export LABELS=""
  run bash "${SCRIPT}"
  [ "$status" -eq 1 ]
}

# ── Labeled event: basic behavior ───────────────────────────────────────────

@test "labeled event: processes valid newly added label" {
  export EVENT_ACTION="labeled"
  export EVENT_LABEL="Hotfix 7.0.1"
  export LABELS="Hotfix 7.0.1,Hotfix 8.0.0"

  # Mock git and gh to report no existing branch/PR.
  # Create stub 'git' and 'gh' in PATH that return empty/zero.
  local stub_dir
  stub_dir="$(mktemp -d)"
  cat > "${stub_dir}/git" <<'STUB'
#!/usr/bin/env bash
# ls-remote --heads: return nothing (no existing branch)
exit 0
STUB
  cat > "${stub_dir}/gh" <<'STUB'
#!/usr/bin/env bash
# pr list: return empty array ("0" PRs)
echo "0"
STUB
  chmod +x "${stub_dir}/git" "${stub_dir}/gh"

  export PATH="${stub_dir}:${PATH}"
  run bash "${SCRIPT}"
  rm -rf "${stub_dir}"

  [ "$status" -eq 0 ]
  [ "$(get_versions)" = '["7.0.1"]' ]
}

@test "labeled event: skips non-hotfix label" {
  export EVENT_ACTION="labeled"
  export EVENT_LABEL="bug"
  export LABELS="bug,Hotfix 7.0.1"
  run bash "${SCRIPT}"
  [ "$status" -eq 0 ]
  [ "$(get_versions)" = '[]' ]
}

@test "labeled event: skips malformed hotfix label" {
  export EVENT_ACTION="labeled"
  export EVENT_LABEL="Hotfix 7.0"
  export LABELS="Hotfix 7.0"
  run bash "${SCRIPT}"
  [ "$status" -eq 0 ]
  [ "$(get_versions)" = '[]' ]
}

# ── Labeled event: duplicate detection ──────────────────────────────────────

@test "labeled event: skips when cherry-pick branch already exists" {
  export EVENT_ACTION="labeled"
  export EVENT_LABEL="Hotfix 7.0.1"
  export LABELS="Hotfix 7.0.1"

  local stub_dir
  stub_dir="$(mktemp -d)"
  # git ls-remote returns a matching ref (branch exists).
  cat > "${stub_dir}/git" <<'STUB'
#!/usr/bin/env bash
echo "abc123	refs/heads/dev/automation/pr-42-to-7.0.1"
STUB
  chmod +x "${stub_dir}/git"
  export PATH="${stub_dir}:${PATH}"

  run bash "${SCRIPT}"
  rm -rf "${stub_dir}"

  [ "$status" -eq 0 ]
  [ "$(get_versions)" = '[]' ]
  [[ "$output" == *"already exists"* ]]
}

@test "labeled event: skips when cherry-pick PR already exists" {
  export EVENT_ACTION="labeled"
  export EVENT_LABEL="Hotfix 7.0.1"
  export LABELS="Hotfix 7.0.1"

  local stub_dir
  stub_dir="$(mktemp -d)"
  # git ls-remote returns nothing (no branch), but gh reports an existing PR.
  cat > "${stub_dir}/git" <<'STUB'
#!/usr/bin/env bash
exit 0
STUB
  cat > "${stub_dir}/gh" <<'STUB'
#!/usr/bin/env bash
echo "1"
STUB
  chmod +x "${stub_dir}/git" "${stub_dir}/gh"
  export PATH="${stub_dir}:${PATH}"

  run bash "${SCRIPT}"
  rm -rf "${stub_dir}"

  [ "$status" -eq 0 ]
  [ "$(get_versions)" = '[]' ]
  [[ "$output" == *"already exists"* ]]
}
