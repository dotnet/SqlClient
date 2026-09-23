#!/usr/bin/env bats
#################################################################################
# Licensed to the .NET Foundation under one or more agreements.                 #
# The .NET Foundation licenses this file to you under the MIT license.          #
# See the LICENSE file in the project root for more information.                #
#################################################################################
#
# Tests for sync-hotfix-label-from-issue.sh
#
# Run with:  bats .github/scripts/tests/sync-hotfix-label-from-issue.bats
#
# Dependencies: bats-core (https://github.com/bats-core/bats-core)
#
#################################################################################

SCRIPT=".github/scripts/sync-hotfix-label-from-issue.sh"

setup() {
  export ISSUE_NUMBER="4715"
  export GH_TOKEN="fake-token"
  export GITHUB_REPOSITORY="dotnet/SqlClient"

  STUB_DIR="$(mktemp -d)"
  export PATH="${STUB_DIR}:${PATH}"
}

teardown() {
  rm -rf "${STUB_DIR}"
}

# Writes a 'gh' stub that dispatches based on argv, covering both this
# script's own GraphQL lookup and the calls its delegate,
# sync-hotfix-label-to-pr.sh, makes for each PR it's re-run against.
#
#   $1: newline-separated "pr_number:STATE" pairs returned by the
#       closedByPullRequestsReferences GraphQL query (STATE is OPEN or CLOSED)
#   $2: newline-separated "pr_number:closing_issue_numbers,..." describing
#       each PR's own closing issue references (delegate's Step 1)
#   $3: newline-separated "issue_number:label1,label2" pairs describing each
#       referenced issue's Hotfix labels (delegate's Step 2)
#   $4: newline-separated "pr_number:label1,label2" pairs describing each PR's
#       existing labels (delegate's Step 3)
stub_gh() {
  local closing_prs="$1"
  local pr_closing_issues="$2"
  local issue_label_map="$3"
  local pr_label_map="$4"

  printf '%s' "${closing_prs}" > "${STUB_DIR}/closing_prs.txt"
  printf '%s' "${pr_closing_issues}" > "${STUB_DIR}/pr_closing_issues.txt"
  printf '%s' "${issue_label_map}" > "${STUB_DIR}/issue_labels.txt"
  printf '%s' "${pr_label_map}" > "${STUB_DIR}/pr_labels.txt"
  : > "${STUB_DIR}/add_label_calls.log"

  cat > "${STUB_DIR}/gh" <<STUB
#!/usr/bin/env bash
set -euo pipefail

# gh api graphql (closedByPullRequestsReferences lookup)
if [[ "\$1" == "api" && "\$2" == "graphql" ]]; then
  awk -F':' '\$2 == "OPEN" { print \$1 }' "${STUB_DIR}/closing_prs.txt"
  exit 0
fi

# gh pr view <n> --json closingIssuesReferences ... (delegate Step 1)
if [[ "\$1" == "pr" && "\$2" == "view" && "\${*}" == *"closingIssuesReferences"* ]]; then
  pr_number="\$3"
  awk -F':' -v n="\${pr_number}" '\$1 == n { gsub(",", "\\n", \$2); print \$2 }' "${STUB_DIR}/pr_closing_issues.txt"
  exit 0
fi

# gh issue view <n> --json labels ... (delegate Step 2)
if [[ "\$1" == "issue" && "\$2" == "view" ]]; then
  issue_number="\$3"
  awk -F':' -v n="\${issue_number}" '\$1 == n { gsub(",", "\\n", \$2); print \$2 }' "${STUB_DIR}/issue_labels.txt"
  exit 0
fi

# gh pr view <n> --json labels ... (delegate Step 3, existing PR labels)
if [[ "\$1" == "pr" && "\$2" == "view" && "\${*}" == *"--json labels"* ]]; then
  pr_number="\$3"
  awk -F':' -v n="\${pr_number}" '\$1 == n { gsub(",", "\\n", \$2); print \$2 }' "${STUB_DIR}/pr_labels.txt"
  exit 0
fi

# gh pr edit <n> --repo <repo> --add-label <label> (delegate Step 3)
if [[ "\$1" == "pr" && "\$2" == "edit" ]]; then
  echo "\$3 \$*" | awk '{ print \$1 }' >> "${STUB_DIR}/add_label_calls.log"
  for ((i = 1; i <= \$#; i++)); do
    if [[ "\${!i}" == "--add-label" ]]; then
      j=\$((i + 1))
      echo "\${!j}" >> "${STUB_DIR}/add_label_calls_labels.log"
    fi
  done
  exit 0
fi

echo "unhandled gh invocation: \$*" >&2
exit 1
STUB
  chmod +x "${STUB_DIR}/gh"
}

added_labels() {
  cat "${STUB_DIR}/add_label_calls_labels.log" 2>/dev/null
}

added_prs() {
  cat "${STUB_DIR}/add_label_calls.log" 2>/dev/null
}

# ── --help flag ──────────────────────────────────────────────────────────────

@test "prints help text with --help" {
  run bash "${SCRIPT}" --help
  [ "$status" -eq 0 ]
  [[ "$output" == *"REQUIRED ENVIRONMENT VARIABLES"* ]]
}

# ── Input validation ─────────────────────────────────────────────────────────

@test "fails when ISSUE_NUMBER is unset" {
  unset ISSUE_NUMBER
  run bash "${SCRIPT}"
  [ "$status" -ne 0 ]
  [[ "$output" == *"ISSUE_NUMBER"* ]]
}

# ── No-op cases ───────────────────────────────────────────────────────────────

@test "no-ops when no open PR closes the issue" {
  stub_gh "" "" "" ""
  run bash "${SCRIPT}"
  [ "$status" -eq 0 ]
  [[ "$output" == *"Nothing to sync"* ]]
  [ -z "$(added_labels)" ]
}

@test "ignores a closed PR that references the issue" {
  stub_gh "$(printf '4721:CLOSED')" "" "" ""
  run bash "${SCRIPT}"
  [ "$status" -eq 0 ]
  [[ "$output" == *"Nothing to sync"* ]]
  [ -z "$(added_labels)" ]
}

# ── Happy path ────────────────────────────────────────────────────────────────

@test "syncs the Hotfix label onto the open PR that closes the issue" {
  stub_gh \
    "$(printf '4721:OPEN')" \
    "$(printf '4721:4715')" \
    "$(printf '4715:Hotfix 7.1.1')" \
    "$(printf '4721:')"
  run bash "${SCRIPT}"
  [ "$status" -eq 0 ]
  [[ "$output" == *"Syncing Hotfix labels onto PR #4721"* ]]
  [ "$(added_labels)" = "Hotfix 7.1.1" ]
  [ "$(added_prs)" = "4721" ]
}

@test "syncs onto every open PR when more than one references the issue" {
  stub_gh \
    "$(printf '4721:OPEN\n4722:OPEN')" \
    "$(printf '4721:4715\n4722:4715')" \
    "$(printf '4715:Hotfix 7.1.1')" \
    "$(printf '4721:\n4722:')"
  run bash "${SCRIPT}"
  [ "$status" -eq 0 ]
  [ "$(added_prs | sort)" = "$(printf '4721\n4722' | sort)" ]
}

@test "is a no-op via the delegate when the PR already has the label" {
  stub_gh \
    "$(printf '4721:OPEN')" \
    "$(printf '4721:4715')" \
    "$(printf '4715:Hotfix 7.1.1')" \
    "$(printf '4721:Hotfix 7.1.1')"
  run bash "${SCRIPT}"
  [ "$status" -eq 0 ]
  [[ "$output" == *"already has label 'Hotfix 7.1.1'"* ]]
  [ -z "$(added_labels)" ]
}
