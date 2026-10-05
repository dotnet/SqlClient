#!/usr/bin/env python3
# usage: u16grep.py <dir> <marker>...  Reports whether each marker appears as UTF-16 in <dir>.
import sys, os
d = sys.argv[1]; data = b''
for root, _, files in os.walk(d):
    for f in files:
        if f.endswith(('.dll', '')) and not f.endswith(('.json', '.pdb', '.dbg')):
            data += open(os.path.join(root, f), 'rb').read()
for m in sys.argv[2:]:
    print(f"{m.split('_')[-1]}={'PRESENT' if m.encode('utf-16-le') in data else 'TRIMMED'}", end=' ')
print()
