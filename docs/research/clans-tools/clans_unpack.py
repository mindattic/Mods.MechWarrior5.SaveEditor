"""Unpacks a MW5 Clans .sav: '!GZIP' magic + UE FArchive compressed-chunk stream (gzip chunks)."""
import re
import struct
import sys
import zlib

data = open(sys.argv[1], "rb").read()
assert data[:5] == b"!GZIP", data[:8]
p = 5
out = bytearray()
chunks = 0
while p < len(data):
    tag, chunk_size = struct.unpack_from("<QQ", data, p)
    # Some UE versions put a 1-byte compressor id after the tag/size pair; detect by tag check.
    if tag & 0xFFFFFFFF != 0x9E2A83C1:
        raise SystemExit(f"bad tag at {p:#x}: {tag:#x}")
    p += 16
    p += 1  # compressor id byte (4 = gzip in this build)
    comp, uncomp = struct.unpack_from("<QQ", data, p)
    p += 16
    n = (uncomp + chunk_size - 1) // chunk_size
    sizes = []
    for _ in range(n):
        sizes.append(struct.unpack_from("<QQ", data, p))
        p += 16
    for c, u in sizes:
        block = zlib.decompress(data[p:p + c], 31)
        assert len(block) == u
        out += block
        p += c
    chunks += 1
print(f"{sys.argv[1]}: {chunks} archive block(s), {len(data)} -> {len(out)} bytes, header byte at 5..: {data[5:0x36].hex()}")
open(sys.argv[2], "wb").write(out)
names = sorted(set(m.group().decode() for m in re.finditer(rb"[A-Z][A-Za-z0-9_]{5,60}Property", out)))
print("property types:", names[:40])
print("first 200 bytes:", out[:200])
