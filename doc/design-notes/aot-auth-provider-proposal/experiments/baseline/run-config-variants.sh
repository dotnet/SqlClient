#!/bin/bash
# app.config variants against SqlClient 7.1.1's real section handler (experiments.md A.3, A.4).
# usage: run-config-variants.sh
source "$(dirname "$0")/../tools/common.sh"
"$(dirname "$0")/run.sh" net10.0 jit true -p:CfgProbe=true > /dev/null || exit 1
W=$WORK_ROOT/baseline; out=$W/out/net10.0-jit-true--p_CfgProbe_true
for cfg in "$W"/cfgs/*.config; do
  echo "===== $(basename "$cfg" .config)"
  cp "$cfg" "$out/app.dll.config"
  "$out/app" 2>&1 | grep -E "^PROBE|^GetProvider|^SetProvider|^Initializer|invocation failed|Unable to load|Received|Added user-defined|Failed to add" | cut -c1-300
done
rm -f "$out/app.dll.config"
