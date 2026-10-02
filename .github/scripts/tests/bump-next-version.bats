#!/usr/bin/env bats
#################################################################################
# Licensed to the .NET Foundation under one or more agreements.                 #
# The .NET Foundation licenses this file to you under the MIT license.          #
# See the LICENSE file in the project root for more information.                #
#################################################################################

SCRIPT="${BATS_TEST_DIRNAME}/../bump-next-version.sh"

setup() {
  TEST_DIR="$(mktemp -d)"
  export PATH="${TEST_DIR}/bin:${PATH}"
  mkdir -p "${TEST_DIR}/bin" "${TEST_DIR}/work/src/Microsoft.Data.SqlClient"
  git init -q --bare "${TEST_DIR}/remote.git"
  git init -q "${TEST_DIR}/work"
  git -C "${TEST_DIR}/work" config user.name "Test"
  git -C "${TEST_DIR}/work" config user.email "test@example.invalid"
  git -C "${TEST_DIR}/work" remote add origin "${TEST_DIR}/remote.git"
  echo '<Project><SqlClientNextVersion>8.0.0-preview1</SqlClientNextVersion></Project>' > "${TEST_DIR}/work/src/Microsoft.Data.SqlClient/Versions.props"
  git -C "${TEST_DIR}/work" add .
  git -C "${TEST_DIR}/work" commit -qm initial
  git -C "${TEST_DIR}/work" branch -M main
  git -C "${TEST_DIR}/work" push -q origin main

  printf '8.0.0-preview2\topen\n8.0.0\topen\n' > "${TEST_DIR}/milestones"
  export TEST_DIR GITHUB_REPOSITORY="dotnet/SqlClient" DEFAULT_BRANCH="main"
  export CLOSED_MILESTONE="8.0.0-preview1" GH_TOKEN="test-token"
  cat > "${TEST_DIR}/bin/gh" <<'MOCK'
#!/usr/bin/env bash
echo "$*" >> "${TEST_DIR}/gh.log"
if [[ "$1" == "api" && "$2" == "--paginate" ]]; then
  [[ -z "${FAIL_MILESTONES:-}" ]] || exit 1
  cat "${TEST_DIR}/milestones"
elif [[ "$1" == "api" && "$2" == "-X" ]]; then
  [[ -z "${FAIL_CREATE:-}" ]] || exit 1
  printf '%s\n' "${@: -1}" >> "${TEST_DIR}/created"
elif [[ "$1" == "pr" && "$2" == "list" ]]; then
  [[ -z "${FAIL_LIST:-}" ]] || exit 1
  printf '%s' "${EXISTING_PR:-}"
elif [[ "$1" == "pr" && "$2" == "create" ]]; then
  [[ -z "${FAIL_PR:-}" ]] || exit 1
  echo 'https://github.com/dotnet/SqlClient/pull/123'
elif [[ "$1" == "pr" && "$2" == "edit" ]]; then
  exit 0
else
  exit 1
fi
MOCK
  chmod +x "${TEST_DIR}/bin/gh"
}

teardown() {
  rm -rf "${TEST_DIR}"
}

run_bump() {
  run bash -c 'cd "$TEST_DIR/work" && bash "$1" "${@:2}"' -- "${SCRIPT}" "$@"
}

set_version() {
  printf '<Project><SqlClientNextVersion>%s</SqlClientNextVersion></Project>\n' "$1" > "${TEST_DIR}/work/src/Microsoft.Data.SqlClient/Versions.props"
  git -C "${TEST_DIR}/work" add .
  git -C "${TEST_DIR}/work" commit -qm update
  git -C "${TEST_DIR}/work" push -q origin main
}

@test "advances preview to the next open preview regardless of API order" {
  printf '8.0.0\topen\n8.0.0-preview3\topen\n8.0.0-preview2\topen\n' > "${TEST_DIR}/milestones"
  run_bump
  [ "$status" -eq 0 ]
  [[ "$output" == *"8.0.0-preview2"* ]]
  grep -qF -- '--base main --head dev/automation/next-version-main-8.0.0-preview1 --milestone 8.0.0-preview2' "${TEST_DIR}/gh.log"
  [ "$(git -C "${TEST_DIR}/work" show 'origin/dev/automation/next-version-main-8.0.0-preview1:src/Microsoft.Data.SqlClient/Versions.props' | grep -c '<SqlClientNextVersion>8.0.0-preview2</SqlClientNextVersion>')" -eq 1 ]
}

@test "advances from last preview to stable in the same series" {
  export CLOSED_MILESTONE="8.0.0-preview3"
  set_version "${CLOSED_MILESTONE}"
  printf '8.0.0\topen\n' > "${TEST_DIR}/milestones"
  run_bump
  [ "$status" -eq 0 ]
  [[ "$output" == *"8.0.0"* ]]
  [ ! -f "${TEST_DIR}/created" ]
}

@test "falls back to a new patch milestone after stable closes" {
  export CLOSED_MILESTONE="8.0.0"
  set_version "${CLOSED_MILESTONE}"
  printf '8.0.1\tclosed\n' > "${TEST_DIR}/milestones"
  run_bump
  [ "$status" -eq 0 ]
  grep -qF 'title=8.0.2' "${TEST_DIR}/gh.log"
  grep -qF -- '--milestone 8.0.2' "${TEST_DIR}/gh.log"
}

@test "falls back to a patch when no later preview or stable milestone is open" {
  printf '8.0.0-preview2\tclosed\n8.1.0-preview1\topen\n' > "${TEST_DIR}/milestones"
  run_bump
  [ "$status" -eq 0 ]
  grep -qF 'title=8.0.1' "${TEST_DIR}/gh.log"
  grep -qF -- '--milestone 8.0.1' "${TEST_DIR}/gh.log"
}

@test "updates release branch rather than main for servicing milestone" {
  git -C "${TEST_DIR}/work" switch -qc release/7.1
  echo '<Project><SqlClientNextVersion>7.1.0</SqlClientNextVersion></Project>' > "${TEST_DIR}/work/src/Microsoft.Data.SqlClient/Versions.props"
  git -C "${TEST_DIR}/work" add .
  git -C "${TEST_DIR}/work" commit -qm release
  git -C "${TEST_DIR}/work" push -q origin release/7.1
  export CLOSED_MILESTONE="7.1.0"
  printf '7.1.1\topen\n8.0.0\topen\n' > "${TEST_DIR}/milestones"
  run_bump
  [ "$status" -eq 0 ]
  grep -qF -- '--base release/7.1 --head dev/automation/next-version-release-7.1-7.1.0 --milestone 7.1.1' "${TEST_DIR}/gh.log"
}

@test "skips stale closure when branch has already advanced" {
  set_version "8.0.0-preview2"
  run_bump
  [ "$status" -eq 0 ]
  [[ "$output" == *"already targets"* ]]
  [ ! -f "${TEST_DIR}/gh.log" ]
}

@test "skips non-versioned and legacy milestones" {
  export CLOSED_MILESTONE="7.0 Hotfix 2"
  run_bump
  [ "$status" -eq 0 ]
  [[ "$output" == *"Skipping"* ]]
  [ ! -f "${TEST_DIR}/gh.log" ]
}

@test "does not open a duplicate version-bump PR" {
  export EXISTING_PR="123"
  run_bump
  [ "$status" -eq 0 ]
  [[ "$output" == *"already exists"* ]]
  ! grep -qF 'pr create' "${TEST_DIR}/gh.log"
}

@test "fails visibly if the milestone listing fails" {
  export FAIL_MILESTONES=1
  run_bump
  [ "$status" -ne 0 ]
  [[ "$output" == *"milestones"* ]]
}

@test "skips a legacy release branch without unified Versions.props" {
  git -C "${TEST_DIR}/work" switch -qc release/7.0
  git -C "${TEST_DIR}/work" rm -q src/Microsoft.Data.SqlClient/Versions.props
  git -C "${TEST_DIR}/work" commit -qm legacy
  git -C "${TEST_DIR}/work" push -q origin release/7.0
  export CLOSED_MILESTONE="7.0.3"
  run_bump
  [ "$status" -eq 0 ]
  [[ "$output" == *"does not use the unified"* ]]
  [ ! -f "${TEST_DIR}/gh.log" ]
}

@test "fails without pushing if patch milestone creation fails" {
  export CLOSED_MILESTONE="8.0.0"
  set_version "${CLOSED_MILESTONE}"
  printf '8.0.0\tclosed\n' > "${TEST_DIR}/milestones"
  export FAIL_CREATE=1
  run_bump
  [ "$status" -ne 0 ]
  [[ "$output" == *"Could not create milestone"* ]]
  ! git -C "${TEST_DIR}/work" ls-remote origin 'refs/heads/dev/automation/*' | grep -q .
}

@test "retries PR creation from an already pushed matching branch" {
  export FAIL_PR=1
  run_bump
  [ "$status" -ne 0 ]
  [ "$(git -C "${TEST_DIR}/work" ls-remote origin 'refs/heads/dev/automation/next-version-main-8.0.0-preview1' | wc -l | tr -d ' ')" -eq 1 ]

  unset FAIL_PR
  run_bump
  [ "$status" -eq 0 ]
  [[ "$output" == *"Reusing"* ]]
  grep -qF 'https://github.com/dotnet/SqlClient/pull/123' <<< "$output"
}

@test "weekly reconciliation leaves an open current milestone unchanged" {
  printf '8.0.0-preview1\topen\n8.0.0-preview2\topen\n' > "${TEST_DIR}/milestones"
  run_bump --reconcile
  [ "$status" -ne 0 ]
  [[ "$output" == *"SYNC_BRANCH"* ]]
  export SYNC_BRANCH=main
  run_bump --reconcile
  [ "$status" -eq 0 ]
  [[ "$output" == *"already targets open milestone"* ]]
  ! grep -qF 'pr create' "${TEST_DIR}/gh.log"
}

@test "weekly reconciliation advances a discarded preview" {
  export SYNC_BRANCH=main
  printf '8.0.0-preview2\topen\n8.0.0\topen\n' > "${TEST_DIR}/milestones"
  run_bump --reconcile
  [ "$status" -eq 0 ]
  grep -qF -- '--milestone 8.0.0-preview2' "${TEST_DIR}/gh.log"
}

@test "weekly reconciliation selects next unbranched series for main" {
  git -C "${TEST_DIR}/work" branch release/8.0
  git -C "${TEST_DIR}/work" push -q origin release/8.0
  printf '8.0.0-preview1\topen\n8.0.0-preview2\topen\n8.1.0-preview1\topen\n' > "${TEST_DIR}/milestones"
  export SYNC_BRANCH=main
  run_bump --reconcile
  [ "$status" -eq 0 ]
  grep -qF -- '--milestone 8.1.0-preview1' "${TEST_DIR}/gh.log"
}

@test "weekly reconciliation does not invent a patch when no open milestone remains" {
  export SYNC_BRANCH=main
  printf '8.0.0-preview1\tclosed\n' > "${TEST_DIR}/milestones"
  run_bump --reconcile
  [ "$status" -eq 0 ]
  [[ "$output" == *"No later open milestone"* ]]
  ! grep -qF 'title=' "${TEST_DIR}/gh.log"
}

@test "weekly sync checks main and release branches with unified version files" {
  git -C "${TEST_DIR}/work" branch release/8.0
  git -C "${TEST_DIR}/work" push -q origin release/8.0
  printf '8.0.0-preview2\topen\n8.1.0-preview1\topen\n' > "${TEST_DIR}/milestones"
  run_bump --sync
  [ "$status" -eq 0 ]
  grep -qF -- '--base main --head dev/automation/next-version-main-8.0.0-preview1 --milestone 8.1.0-preview1' "${TEST_DIR}/gh.log"
  grep -qF -- '--base release/8.0 --head dev/automation/next-version-release-8.0-8.0.0-preview1 --milestone 8.0.0-preview2' "${TEST_DIR}/gh.log"
}

@test "weekly reconciliation updates an existing open PR after another preview is discarded" {
  export SYNC_BRANCH=main
  export EXISTING_PR=$'123\tOPEN'
  printf '8.0.0-preview2\topen\n8.0.0-preview3\topen\n' > "${TEST_DIR}/milestones"
  unset EXISTING_PR
  run_bump --reconcile
  [ "$status" -eq 0 ]

  export EXISTING_PR=$'123\tOPEN'
  printf '8.0.0-preview2\tclosed\n8.0.0-preview3\topen\n' > "${TEST_DIR}/milestones"
  run_bump --reconcile
  [ "$status" -eq 0 ]
  grep -qF 'pr edit 123 --repo dotnet/SqlClient --milestone 8.0.0-preview3' "${TEST_DIR}/gh.log"
  git -C "${TEST_DIR}/work" show 'origin/dev/automation/next-version-main-8.0.0-preview1:src/Microsoft.Data.SqlClient/Versions.props' |
    grep -qF '<SqlClientNextVersion>8.0.0-preview3</SqlClientNextVersion>'
}

@test "weekly reconciliation refuses to overwrite unrelated PR edits" {
  export SYNC_BRANCH=main
  printf '8.0.0-preview2\topen\n8.0.0-preview3\topen\n' > "${TEST_DIR}/milestones"
  run_bump --reconcile
  [ "$status" -eq 0 ]

  echo '<!-- Maintainer edit -->' >> "${TEST_DIR}/work/src/Microsoft.Data.SqlClient/Versions.props"
  git -C "${TEST_DIR}/work" add .
  git -C "${TEST_DIR}/work" commit -qm 'maintainer edit'
  git -C "${TEST_DIR}/work" push -q origin HEAD:refs/heads/dev/automation/next-version-main-8.0.0-preview1
  export EXISTING_PR=$'123\tOPEN'
  printf '8.0.0-preview3\topen\n' > "${TEST_DIR}/milestones"
  run_bump --reconcile
  [ "$status" -ne 0 ]
  [[ "$output" == *"resolve it manually"* ]]
  ! grep -qF 'pr edit' "${TEST_DIR}/gh.log"
}
