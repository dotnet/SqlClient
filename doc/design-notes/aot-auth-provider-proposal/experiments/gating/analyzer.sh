#!/bin/bash
# Library analyzer output for both libraries (experiments.md B).
source "$(dirname "$0")/../tools/common.sh"
W=$(stage gating)
for lib in PolyLib MultiLib; do
  echo "=== $lib"
  dotnet build "$W/$lib" -c Release -p:EnableTrimAnalyzer=true -p:EnableAotAnalyzer=true 2>&1 \
    | grep -E "warning (IL|NETSDK)" | sed -E "s|$W/||; s| \[.*||" | sort -u | cut -c1-200
done
