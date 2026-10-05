#!/bin/bash
# usage: run-matrix.sh [8|9|10 ...]   (default: 10)
here=$(dirname "$0")
for v in "${@:-10}"; do for mode in trim aot; do for off in 0 1; do "$here/run.sh" "$v" "$mode" "$off"; done; done; done
