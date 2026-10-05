#!/bin/bash
# usage: aotsyms.sh <native-binary> <Lib> <shape letters>  Checks nm for <Lib>_Shapes__Reflect<X>.
for x in $(echo $3 | grep -o .); do
  if nm "$1" 2>/dev/null | grep -q "${2}_Shapes__Reflect${x}\b"; then echo -n "$x=PRESENT "; else echo -n "$x=TRIMMED "; fi
done; echo
