#!/usr/bin/env bats
#################################################################################
# Licensed to the .NET Foundation under one or more agreements.                 #
# The .NET Foundation licenses this file to you under the MIT license.          #
# See the LICENSE file in the project root for more information.                #
#################################################################################
#
# Tests for close-backport-issue.sh
#
# Run with:  bats .github/scripts/tests/close-backport-issue.bats
#
# Dependencies: bats-core (https://github.com/bats-core/bats-core)
#
#################################################################################

SCRIPT=".github/scripts/close-backport-issue.sh"

setup() {
  export PR_NUMBER="4750"
  export GH_TOKEN="fake-token"
  export GITHUB_REPOSITORY="dotnet/SqlClient"

  STUB_DIR="$(mktemp -d)"
  export PATH="${STUB_DIR}:${PATH}"
}

teardown() {
  rm -rf "${STUB_DIR}"
}

# Writes a 'gh' stub that dispatches based on argv.
#
#   $1: the merged PR's body (from 'pr view --json body'), possibly
#       containing a "<!-- backport-issue-numbers: N N ... -->" marker
#   $2: newline-separated "issue_number:STATE" pairs (STATE is OPEN or CLOSED)
stub_gh() {
  local pr_body="$1"
  local issue_state_map="$2"

  printf '%s' "${pr_body}" > "${STUB_DIR}/pr_body.txt"
  printf '%s' "${issue_state_map}" > "${STUB_DIR}/issue_states.txt"
  : > "${STUB_DIR}/close_calls.log"

  cat > "${STUB_DIR}/gh" <<STUB
#!/usr/bin/env bash
set -euo pipefail

# gh pr view <n> --json body --jq '.body'
if [[ "\$1" == "pr" && "\$2" == "view" && "\${*}" == *"--json body"* ]]; then
  cat "${STUB_DIR}/pr_body.txt"
  exit 0
fi

# gh issue view <n> --json state --jq '.state'
if [[ "\$1" == "issue" && "\$2" == "view" ]]; then
  issue_number="\$3"
  awk -F':' -v n="\${issue_number}" '\$1 == n { print \$2 }' "${STUB_DIR}/issue_states.txt"
  exit 0
fi

# gh issue close <n> --repo <repo> --reason completed --comment "..."
if [[ "\$1" == "issue" && "\$2" == "close" ]]; then
  echo "\$3" >> "${STUB_DIR}/close_calls.log"
  exit 0
fi

echo "unhandled gh invocation: \$*" >&2
exit 1
STUB
  chmod +x "${STUB_DIR}/gh"
}

closed_issues() {
  cat "${STUB_DIR}/close_calls.log"
}

# ── --help flag ──────────────────────────────────────────────────────────────

@test "prints help text with --help" {
  run bash "${SCRIPT}" --help
  [ "$status" -eq 0 ]
  [[ "$output" == *"REQUIRED ENVIRONMENT VARIABLES"* ]]
}

# ── Input validation ─────────────────────────────────────────────────────────

@test "fails when PR_NUMBER is unset" {
  unset PR_NUMBER
  run bash "${SCRIPT}"
  [ "$status" -ne 0 ]
  [[ "$output" == *"PR_NUMBER"* ]]
}

# ── No-op cases ───────────────────────────────────────────────────────────────

@test "no-ops when the PR body has no backport-issue-numbers marker" {
  stub_gh "Cherry-pick of #123 (abc123) into \`release/7.0\`." ""
  run bash "${SCRIPT}"
  [ "$status" -eq 0 ]
  [[ "$output" == *"Nothing to close"* ]]
  [ -z "$(closed_issues)" ]
}

@test "no-ops when the marker's referenced issue is already closed" {
  stub_gh "$(printf 'Cherry-pick.\n\n<!-- backport-issue-numbers: 4714 -->\n\nFixes #4714')" \
    "$(printf '4714:CLOSED')"
  run bash "${SCRIPT}"
  [ "$status" -eq 0 ]
  [[ "$output" == *"already closed"* ]]
  [ -z "$(closed_issues)" ]
}

@test "aborts instead of proceeding when an issue state lookup fails" {
  # A failed 'gh issue view' must not be swallowed and mistaken for "issue
  # doesn't need closing" — surface it loudly instead of silently skipping.
  printf 'Cherry-pick.\n\n<!-- backport-issue-numbers: 4714 -->\n\nFixes #4714' \
    > "${STUB_DIR}/pr_body.txt"
  cat > "${STUB_DIR}/gh" <<STUB
#!/usr/bin/env bash
set -euo pipefail
if [[ "\$1" == "pr" && "\$2" == "view" && "\${*}" == *"--json body"* ]]; then
  cat "${STUB_DIR}/pr_body.txt"
  exit 0
fi
if [[ "\$1" == "issue" && "\$2" == "view" ]]; then
  echo "gh: permission denied" >&2
  exit 1
fi
echo "unhandled gh invocation: \$*" >&2
exit 1
STUB
  chmod +x "${STUB_DIR}/gh"

  run bash "${SCRIPT}"
  [ "$status" -ne 0 ]
  [[ "$output" == *"Failed to look up state for issue #4714"* ]]
}

@test "ignores a 'Fixes #N' line in the body that has no marker (manual release PR)" {
  # A manually-authored release-branch PR containing "Fixes #<parent issue>"
  # must not have that issue closed: only issues named in our own
  # backport-issue-numbers marker are ever closed.
  stub_gh "Manual backport. Fixes #4715." ""
  run bash "${SCRIPT}"
  [ "$status" -eq 0 ]
  [[ "$output" == *"Nothing to close"* ]]
  [ -z "$(closed_issues)" ]
}

# ── Happy path ────────────────────────────────────────────────────────────────

@test "closes a single open backport issue named in the marker" {
  stub_gh "$(printf 'Cherry-pick.\n\n<!-- backport-issue-numbers: 4714 -->\n\nFixes #4714')" \
    "$(printf '4714:OPEN')"
  run bash "${SCRIPT}"
  [ "$status" -eq 0 ]
  [ "$(closed_issues)" = "4714" ]
}

@test "closes multiple open backport issues named in the marker" {
  stub_gh "$(printf 'Cherry-pick.\n\n<!-- backport-issue-numbers: 4714 4715 -->\n\nFixes #4714\n\nFixes #4715')" \
    "$(printf '4714:OPEN\n4715:OPEN')"
  run bash "${SCRIPT}"
  [ "$status" -eq 0 ]
  [ "$(closed_issues | sort)" = "$(printf '4714\n4715' | sort)" ]
}

@test "closes only the open issue among a mix of open and closed marker entries" {
  stub_gh "$(printf 'Cherry-pick.\n\n<!-- backport-issue-numbers: 4714 4715 -->\n\nFixes #4714\n\nFixes #4715')" \
    "$(printf '4714:OPEN\n4715:CLOSED')"
  run bash "${SCRIPT}"
  [ "$status" -eq 0 ]
  [ "$(closed_issues)" = "4714" ]
  [[ "$output" == *"Issue #4715 is already closed"* ]]
}
