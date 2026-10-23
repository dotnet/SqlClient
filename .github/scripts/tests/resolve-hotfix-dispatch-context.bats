#!/usr/bin/env bats
#################################################################################
# Licensed to the .NET Foundation under one or more agreements.                 #
# The .NET Foundation licenses this file to you under the MIT license.          #
# See the LICENSE file in the project root for more information.                #
#################################################################################
#
# Tests for resolve-hotfix-dispatch-context.sh
#
# Run with:  bats .github/scripts/tests/resolve-hotfix-dispatch-context.bats
#
# Dependencies: bats-core (https://github.com/bats-core/bats-core)
#
#################################################################################

SCRIPT=".github/scripts/resolve-hotfix-dispatch-context.sh"

setup() {
  export PR_NUMBER="4721"
  export GITHUB_REPOSITORY="dotnet/SqlClient"
  export GITHUB_OUTPUT
  GITHUB_OUTPUT="$(mktemp)"

  STUB_DIR="$(mktemp -d)"
  export STUB_DIR
  export PATH="${STUB_DIR}:${PATH}"

  cat > "${STUB_DIR}/gh" <<'STUB'
#!/usr/bin/env bash
set -euo pipefail

printf '%s\n' "$*" > "${STUB_DIR}/gh_args.txt"
for ((i = 1; i <= $#; i++)); do
  if [[ "${!i}" == "--jq" ]]; then
    j=$((i + 1))
    jq -r "${!j}" "${STUB_DIR}/pr.json"
    exit 0
  fi
done

echo "missing --jq argument" >&2
exit 1
STUB
  chmod +x "${STUB_DIR}/gh"
}

teardown() {
  rm -rf "${STUB_DIR}"
  rm -f "${GITHUB_OUTPUT}"
}

stub_pr_state() {
  jq -n --arg state "$1" '{
    number: 4721,
    state: $state,
    baseRefName: "main",
    labels: [{name: "Hotfix 7.1.1"}],
    mergeCommit: {oid: "0123456789abcdef"},
    title: "Fix issue"
  }' > "${STUB_DIR}/pr.json"
}

@test "workflow_dispatch context marks a merged PR for reconciliation" {
  stub_pr_state "MERGED"

  run bash "${SCRIPT}"

  [ "$status" -eq 0 ]
  grep -Fxq "merged=true" "${GITHUB_OUTPUT}"
  grep -Fxq "event_action=reconcile" "${GITHUB_OUTPUT}"
  grep -Fxq "pr_number=4721" "${GITHUB_OUTPUT}"
  grep -Fxq "base_ref=main" "${GITHUB_OUTPUT}"
  grep -Fxq "labels=Hotfix 7.1.1" "${GITHUB_OUTPUT}"
  grep -Fxq "merge_commit_sha=0123456789abcdef" "${GITHUB_OUTPUT}"
  grep -Fq 'pr view 4721 --repo dotnet/SqlClient --json number,state,baseRefName,labels,mergeCommit,title --jq' "${STUB_DIR}/gh_args.txt"
}

@test "workflow_dispatch context does not mark an unmerged closed PR as merged" {
  stub_pr_state "CLOSED"

  run bash "${SCRIPT}"

  [ "$status" -eq 0 ]
  grep -Fxq "merged=false" "${GITHUB_OUTPUT}"
}

@test "workflow_dispatch context does not mark an open PR as merged" {
  stub_pr_state "OPEN"

  run bash "${SCRIPT}"

  [ "$status" -eq 0 ]
  grep -Fxq "merged=false" "${GITHUB_OUTPUT}"
}
