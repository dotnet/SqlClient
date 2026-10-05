#!/bin/bash
# Reproduction of #4193 against the published 7.1.1 packages (experiments.md A.1).
# usage: run.sh <tfm> <jit|trim|aot> <TrimmerSingleWarn: true|false> [extra msbuild args...]
#   e.g. run.sh net10.0 aot false
#        run.sh net10.0 trim true -p:RootAzure=true
source "$(dirname "$0")/../tools/common.sh"
tfm=$1; mode=$2; sw=$3; shift 3; extra="$*"
W=$(stage baseline); app=$W/app
set_tfm "$app/app.csproj" "$tfm"
tag=$(echo "$tfm-$mode-$sw-$extra" | tr ' =:/' '____'); out=$W/out/$tag; log=$W/out/$tag.log
rm -rf "$out"
args="-c Release -r linux-x64 -o $out -p:SingleWarn=$sw $extra"
[ "$mode" != jit ] && args="$args -p:Mode=$mode"
dotnet publish "$app" $args > "$log" 2>&1 || { echo "### $tag PUBLISH FAILED (log: $log)"; tail -5 "$log"; exit 1; }
echo "### $tfm $mode TrimmerSingleWarn=$sw $extra"
echo "warnings: $(grep -oE 'warning IL[0-9]{4}' "$log" | sort | uniq -c | tr -s ' ' | tr '\n' ',')"
echo "assembly-level: $(grep -oE "IL(2104|3053): Assembly '[^']+'" "$log" | sort -u | tr '\n' ';')"
echo "auth-specific warnings: $(grep -E 'warning IL' "$log" | grep -c SqlAuthenticationProvider)"
echo "Azure dll in publish dir: $(ls "$out" | grep -c 'Extensions.Azure.dll')"
"$out/app" 2>&1 | sed 's/^/  run> /' | cut -c1-240
