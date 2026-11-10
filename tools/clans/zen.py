"""Zen package (UE 5.4) header: name map, export map, and export payloads in export-bundle order. Read-only."""
import struct
from classify_mia import names as name_map

def exports(d):
    hsize, = struct.unpack_from('<I', d, 4)
    emap, ebundle, depheaders = struct.unpack_from('<III', d, 32)
    ns = name_map(d)
    entries = []
    for o in range(emap, ebundle, 72):
        off, size = struct.unpack_from('<QQ', d, o); ni, nn = struct.unpack_from('<II', d, o + 16)
        cls, = struct.unpack_from('<Q', d, o + 32)
        entries.append([ns[ni & 0x3FFFFFFF] + (f'_{nn - 1}' if nn else ''), cls, size, None])
    pos = hsize
    for o in range(ebundle, depheaders, 8):
        idx, cmd = struct.unpack_from('<II', d, o)
        if cmd == 1:  # Serialize
            e = entries[idx]; e[3] = d[pos:pos + e[2]]; pos += e[2]
    return [(n, c, b or b'') for n, c, s, b in entries]
