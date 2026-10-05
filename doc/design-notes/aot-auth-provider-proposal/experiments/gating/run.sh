#!/bin/bash
# Gating shapes under trimming and NativeAOT (experiments.md A.2; backporting.md for 9 and 8).
# usage: run.sh <8|9|10> <trim|aot> <switches off: 0|1>
source "$(dirname "$0")/../tools/common.sh"
v=$1; mode=$2; off=$3
W=$(stage gating); app=$W/app$v; out=$W/out/$v-$mode-$off; log=$out.log
rm -rf "$out"
props="-p:Mode=$mode -p:SwitchOff=$([ "$off" = 1 ] && echo true || echo false)"
dotnet publish "$app" -c Release -r linux-x64 -o "$out" $props > "$log" 2>&1 \
  || { echo "net$v $mode off=$off PUBLISH FAILED (log: $log)"; tail -5 "$log"; exit 1; }
warn=$(grep -oE "IL[0-9]{4}" "$log" | sort | uniq -c | tr -s ' ' | tr '\n' ' ')
bin=$out/app$v
echo "net$v $mode off=$off | warnings: ${warn:-none}"
echo "   run:   $("$bin" 2>&1 | tr '\n' ' ')"
if [ "$mode" = aot ]; then
  echo "   poly:  $("$TOOLS/aotsyms.sh" "$bin" PolyLib ABCE)"
  echo "   multi: $("$TOOLS/aotsyms.sh" "$bin" MultiLib ABCD)"
else
  echo "   poly:  $(python3 "$TOOLS/u16grep.py" "$out" POLY_MARKER_A POLY_MARKER_B POLY_MARKER_C POLY_MARKER_E)"
  echo "   multi: $(python3 "$TOOLS/u16grep.py" "$out" MULTI_MARKER_A MULTI_MARKER_B MULTI_MARKER_C MULTI_MARKER_D)"
fi
