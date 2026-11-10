"""Lists every file path in MW5 Clans' IoStore containers from the .utoc directory indexes (read-only, no
decompression needed). Usage: python -I tools/clans/utoc_files.py "<MW5Clans/Content/Paks>" out.txt
M5SE-T46, 2026-11-10: 243,372 files; all 242 Clans catalog items exist in the game."""
import glob, os, struct, sys

def fstring(b, o):
    n, = struct.unpack_from('<i', b, o); o += 4
    if n == 0: return '', o
    if n < 0:
        s = b[o:o + -n * 2].decode('utf-16-le').rstrip('\0'); return s, o + -n * 2
    return b[o:o + n].decode('latin-1').rstrip('\0'), o + n

def files(path):
    b = open(path, 'rb').read()
    assert b[:16] == b'-==--==--==--==-'
    hs, ec, cbc, cbes, cmnc, cmnl, cbs, dis = struct.unpack_from('<8I', b, 20)
    flags = b[80]; seeds, = struct.unpack_from('<I', b, 84); nophash, = struct.unpack_from('<I', b, 96)
    if not flags & 8 or dis == 0: return []
    o = hs + ec * 12 + ec * 10 + seeds * 4 + nophash * 4 + cbc * cbes + cmnc * cmnl
    if flags & 4:  # signed: hash size + toc sig + block sig + block hashes
        sz, = struct.unpack_from('<i', b, o); o += 4 + sz * 2 + cbc * 20
    d = b[o:o + dis]; p = 0
    mount, p = fstring(d, p)
    n, = struct.unpack_from('<i', d, p); p += 4
    dirs = [struct.unpack_from('<4I', d, p + i * 16) for i in range(n)]; p += n * 16
    n, = struct.unpack_from('<i', d, p); p += 4
    fents = [struct.unpack_from('<3I', d, p + i * 12) for i in range(n)]; p += n * 12
    n, = struct.unpack_from('<i', d, p); p += 4
    strings = []
    for _ in range(n):
        s, p = fstring(d, p); strings.append(s)
    out = []
    NONE = 0xFFFFFFFF
    def walk(i, prefix):
        while i != NONE:
            name, child, sib, ffile = dirs[i]
            here = prefix + (strings[name] + '/' if name != NONE else '')
            f = ffile
            while f != NONE:
                fn, nxt, _ = fents[f]; out.append(mount + here + strings[fn]); f = nxt
            walk(child, here)
            i = sib
    walk(0, '')
    return out

if __name__ == '__main__':
    root = sys.argv[1]
    allf = []
    for t in sorted(glob.glob(os.path.join(root, '*.utoc'))):
        fs = files(t); allf += fs
        print(f'{os.path.basename(t):40} {len(fs)} files', file=sys.stderr)
    open(sys.argv[2], 'w', encoding='utf-8').write('\n'.join(allf))
    print(len(allf), 'total', file=sys.stderr)
