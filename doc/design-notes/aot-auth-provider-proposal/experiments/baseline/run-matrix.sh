#!/bin/bash
# Full reproduction matrix (experiments.md A.1; backporting.md for net9.0/net8.0).
# usage: run-matrix.sh [tfm...]   (default: net10.0)
here=$(dirname "$0")
for tfm in "${@:-net10.0}"; do
  "$here/run.sh" "$tfm" jit true
  for spec in "trim true" "trim false" "aot true" "aot false" "trim true -p:RootAzure=true"; do
    "$here/run.sh" "$tfm" $spec
  done
done
