#!/bin/bash
# Untyped read of a section whose handler lives in another assembly, and the CS0122 forwarding
# check (experiments.md A.3).
source "$(dirname "$0")/../tools/common.sh"
W=$(stage forwarding)
echo "=== untyped read across assemblies"
dotnet run --project "$W/app" 2>&1 | tail -1
echo "=== forwarding an internal type (expected to fail with CS0122)"
dotnet build "$W/Owner" -p:ForwardCheck=true 2>&1 | grep -oE "error CS0122[^[]*" | sort -u
