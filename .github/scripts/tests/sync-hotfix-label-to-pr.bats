#!/usr/bin/env bats
#################################################################################
# Licensed to the .NET Foundation under one or more agreements.                 #
# The .NET Foundation licenses this file to you under the MIT license.          #
# See the LICENSE file in the project root for more information.                #
#################################################################################
#
# Tests for sync-hotfix-label-to-pr.sh
#
# Run with:  bats .github/scripts/tests/sync-hotfix-label-to-pr.bats
#
# Dependencies: bats-core (https://github.com/bats-core/bats-core)
#
#################################################################################

SCRIPT=".github/scripts/sync-hotfix-label-to-pr.sh"

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
#   $1: closing issue numbers, one per line (from 'pr view --json closingIssuesReferences')
#   $2: newline-separated "issue_number:label1,label2" pairs describing each
#       referenced issue's Hotfix labels (label list may be empty)
#   $3: existing PR labels, one per line
stub_gh() {
  local closing_issues="$1"
  local issue_label_map="$2"
  local existing_pr_labels="$3"

  printf '%s' "${closing_issues}" > "${STUB_DIR}/closing_issues.txt"
  printf '%s' "${issue_label_map}" > "${STUB_DIR}/issue_labels.txt"
  printf '%s' "${existing_pr_labels}" > "${STUB_DIR}/pr_labels.txt"
  : > "${STUB_DIR}/add_label_calls.log"

  cat > "${STUB_DIR}/gh" <<STUB
#!/usr/bin/env bash
set -euo pipefail

# gh pr view <n> --json closingIssuesReferences ...
if [[ "\$1" == "pr" && "\$2" == "view" && "\${*}" == *"closingIssuesReferences"* ]]; then
  cat "${STUB_DIR}/closing_issues.txt"
  exit 0
fi

# gh pr view <n> --json labels ...
if [[ "\$1" == "pr" && "\$2" == "view" && "\${*}" == *"--json labels"* ]]; then
  cat "${STUB_DIR}/pr_labels.txt"
  exit 0
fi

# gh issue view <n> --json labels ... (already filtered to "Hotfix " labels,
# matching the real script's '--jq select(startswith("Hotfix "))' filter)
if [[ "\$1" == "issue" && "\$2" == "view" ]]; then
  issue_number="\$3"
  awk -F':' -v n="\${issue_number}" '\$1 == n { print \$2 }' "${STUB_DIR}/issue_labels.txt" \
    | tr ',' '\n' | grep '^Hotfix ' || true
  exit 0
fi

# gh pr edit <n> --repo <repo> --add-label <label>
if [[ "\$1" == "pr" && "\$2" == "edit" ]]; then
  args=("\$@")
  for i in "\${!args[@]}"; do
    if [[ "\${args[\$i]}" == "--add-label" ]]; then
      next=\$((i + 1))
      echo "\${args[\$next]}" >> "${STUB_DIR}/add_label_calls.log"
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
  cat "${STUB_DIR}/add_label_calls.log"
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

@test "no-ops when the PR has no closing issue references" {
  stub_gh "" "" ""
  run bash "${SCRIPT}"
  [ "$status" -eq 0 ]
  [[ "$output" == *"Nothing to sync"* ]]
  [ -z "$(added_labels)" ]
}

@test "no-ops when referenced issues have no Hotfix labels" {
  stub_gh "$(printf '4714')" "$(printf '4714:bug')" ""
  run bash "${SCRIPT}"
  [ "$status" -eq 0 ]
  [[ "$output" == *"No 'Hotfix X.Y.Z' labels found"* ]]
  [ -z "$(added_labels)" ]
}

@test "aborts instead of proceeding when an issue label lookup fails" {
  # A failed 'gh issue view' must not be swallowed by the grep '|| true'
  # tolerance and treated the same as "issue has no Hotfix labels" — that
  # would silently skip labeling the PR after a transient API/permission
  # error instead of surfacing a retryable failure.
  printf '4714' > "${STUB_DIR}/closing_issues.txt"
  cat > "${STUB_DIR}/gh" <<STUB
#!/usr/bin/env bash
set -euo pipefail
if [[ "\$1" == "pr" && "\$2" == "view" && "\${*}" == *"closingIssuesReferences"* ]]; then
  cat "${STUB_DIR}/closing_issues.txt"
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
  [[ "$output" == *"Failed to look up labels for issue #4714"* ]]
}

# ── Happy path ────────────────────────────────────────────────────────────────

@test "adds a Hotfix label from a single referenced issue" {
  stub_gh "$(printf '4714')" "$(printf '4714:bug,Hotfix 7.1.1')" ""
  run bash "${SCRIPT}"
  [ "$status" -eq 0 ]
  [ "$(added_labels)" = "Hotfix 7.1.1" ]
}

@test "adds Hotfix labels from multiple referenced issues without duplicates" {
  stub_gh "$(printf '4714\n4715')" \
    "$(printf '4714:Hotfix 7.1.1\n4715:Hotfix 7.1.1,Hotfix 8.0.0')" \
    ""
  run bash "${SCRIPT}"
  [ "$status" -eq 0 ]
  [ "$(added_labels | sort)" = "$(printf 'Hotfix 7.1.1\nHotfix 8.0.0' | sort)" ]
}

@test "does not re-add a Hotfix label the PR already has" {
  stub_gh "$(printf '4714')" "$(printf '4714:Hotfix 7.1.1')" "$(printf 'Hotfix 7.1.1')"
  run bash "${SCRIPT}"
  [ "$status" -eq 0 ]
  [[ "$output" == *"already has label 'Hotfix 7.1.1'"* ]]
  [ -z "$(added_labels)" ]
}
