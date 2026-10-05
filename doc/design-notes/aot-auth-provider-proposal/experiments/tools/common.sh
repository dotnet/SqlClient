#!/bin/bash
# Shared helpers. Each harness is copied to a work directory outside the repository before it is
# built, so the repository's NuGet.config, central package management and Directory.* files do
# not apply. Override the location with AOT_EXPERIMENTS_WORK.

EXPERIMENTS_ROOT=$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)
TOOLS="$EXPERIMENTS_ROOT/tools"
WORK_ROOT=${AOT_EXPERIMENTS_WORK:-${TMPDIR:-/tmp}/aot-auth-experiments}

# stage <harness>: copy experiments/<harness> to $WORK_ROOT/<harness>, preserving any out/ folder.
stage() {
  local name=$1 dst="$WORK_ROOT/$1"
  mkdir -p "$dst"
  find "$dst" -mindepth 1 -maxdepth 1 ! -name out -exec rm -rf {} +
  cp -r "$EXPERIMENTS_ROOT/$name/." "$dst/"
  mkdir -p "$dst/out"
  echo "$dst"
}

# set_tfm <csproj> <tfm>: rewrite the single <TargetFramework> element.
set_tfm() {
  sed -i -E "s|<TargetFramework>[^<]+</TargetFramework>|<TargetFramework>$2</TargetFramework>|" "$1"
}
