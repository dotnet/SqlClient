#!/usr/bin/env bash
#################################################################################
# Licensed to the .NET Foundation under one or more agreements.                 #
# The .NET Foundation licenses this file to you under the MIT license.          #
# See the LICENSE file in the project root for more information.                #
#################################################################################
#
# Resolves the pull request context for a cherry-pick workflow_dispatch run.
#
#################################################################################

set -euo pipefail

: "${PR_NUMBER:?PR_NUMBER is required}"
: "${GITHUB_REPOSITORY:?GITHUB_REPOSITORY is required}"
: "${GITHUB_OUTPUT:?GITHUB_OUTPUT is required}"

gh pr view "${PR_NUMBER}" --repo "${GITHUB_REPOSITORY}" \
  --json number,state,baseRefName,labels,mergeCommit,title \
  --jq '
    "pr_number=\(.number)",
    "event_action=reconcile",
    "event_label=",
    "merged=\(.state == "MERGED")",
    "base_ref=\(.baseRefName)",
    "labels=\([.labels[].name] | join(","))",
    "merge_commit_sha=\(.mergeCommit.oid // "")",
    "pr_title=\(.title)"
  ' >> "${GITHUB_OUTPUT}"
