"""Checks every Clans save: unpack, recompress with the same chunk layout, unpack again; reports header fields."""
import glob
import os
import struct
import subprocess
import sys
import zlib

TAG = 0x222222229E2A83C1
CHUNK = 0x20000


def unpack(data):
    assert data[:5] == b"!GZIP"
    p, out, blocks = 5, bytearray(), 0
    while p < len(data):
        tag, chunk = struct.unpack_from("<QQ", data, p); p += 16
        assert tag == TAG and chunk == CHUNK
        comp_id = data[p]; p += 1
        comp, uncomp = struct.unpack_from("<QQ", data, p); p += 16
        sizes = []
        for _ in range((uncomp + CHUNK - 1) // CHUNK):
            sizes.append(struct.unpack_from("<QQ", data, p)); p += 16
        for c, u in sizes:
            out += zlib.decompress(data[p:p + c], 31); p += c
        blocks += 1
    return bytes(out), comp_id, blocks


def pack(raw, comp_id=4):
    out = bytearray(b"!GZIP")
    for b0 in range(0, len(raw), CHUNK):
        part = raw[b0:b0 + CHUNK]
        co = zlib.compressobj(6, zlib.DEFLATED, 31)
        z = co.compress(part) + co.flush()
        out += struct.pack("<QQ", TAG, CHUNK) + bytes([comp_id])
        out += struct.pack("<QQ", len(z), len(part)) + struct.pack("<QQ", len(z), len(part)) + z
    return bytes(out)


src = os.path.expandvars(r"%LOCALAPPDATA%\MW5Clans\Saved\SaveGames")
ok = 0
for f in sorted(glob.glob(os.path.join(src, "*.sav"))):
    data = open(f, "rb").read()
    raw, cid, blocks = unpack(data)
    again, _, _ = unpack(pack(raw, cid))
    same = again == raw
    ok += same
    print(f"{os.path.basename(f)[:45]:46} comp={cid} blocks={blocks:3} raw={len(raw):8} roundtrip={same}")
print("ok", ok)
