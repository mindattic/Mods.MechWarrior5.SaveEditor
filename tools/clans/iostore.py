"""Minimal read-only IoStore reader for MW5 Clans (M5SE-T46): TOC, compressed blocks, Oodle, directory index.

Oodle comes from the Unreal Engine install on this machine (its licensed oo2core.dll); it is loaded, never shipped.
Set MW5_OODLE to another oo2core.dll path if UE lives elsewhere. Clans cooks unversioned properties, so values
such as tonnage need a class schema; names, tags and export layout (zen.py, classify_mia.py) don't."""
import ctypes, glob, os, struct, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from utoc_files import fstring

OODLE = os.environ.get("MW5_OODLE") or r"D:\Games\UE_5.8\Engine\Binaries\DotNET\AutomationTool\AutomationScripts\BuildGraph\net10.0\runtimes\win-x64\native\oo2core.dll"
_oo = ctypes.WinDLL(OODLE)
_dec = _oo.OodleLZ_Decompress
_dec.restype = ctypes.c_int64
_dec.argtypes = [ctypes.c_void_p, ctypes.c_int64, ctypes.c_void_p, ctypes.c_int64] + [ctypes.c_int] * 3 + [ctypes.c_void_p, ctypes.c_int64, ctypes.c_void_p, ctypes.c_void_p, ctypes.c_void_p, ctypes.c_int64, ctypes.c_int]

def oodle(src, size):
    out = ctypes.create_string_buffer(size)
    n = _dec(src, len(src), out, size, 1, 0, 0, None, 0, None, None, None, 0, 3)
    if n != size: raise ValueError(f'oodle returned {n}, wanted {size}')
    return out.raw

def be40(b, o): return int.from_bytes(b[o:o + 5], 'big')

class Container:
    def __init__(self, utoc):
        b = open(utoc, 'rb').read(); self.path = utoc
        hs, ec, cbc, cbes, cmnc, cmnl, self.bs, dis, pc = struct.unpack_from('<9I', b, 20)
        flags = b[80]; seeds, = struct.unpack_from('<I', b, 84); self.psize, = struct.unpack_from('<Q', b, 88); nophash, = struct.unpack_from('<I', b, 96)
        o = hs; self.ids = b[o:o + ec * 12]; o += ec * 12
        self.offlen = b[o:o + ec * 10]; o += ec * 10
        o += seeds * 4 + nophash * 4
        self.blocks = b[o:o + cbc * cbes]; self.cbes = cbes; o += cbc * cbes
        self.methods = [b[o + i * cmnl:o + (i + 1) * cmnl].rstrip(b'\0').decode() for i in range(cmnc)]; o += cmnc * cmnl
        if flags & 4:
            sz, = struct.unpack_from('<i', b, o); o += 4 + sz * 2 + cbc * 20
        self.dirindex = b[o:o + dis]
        self.ucas = utoc[:-5] + '.ucas'

    def read_entry(self, i):
        off = be40(self.offlen, i * 10); ln = be40(self.offlen, i * 10 + 5)
        first, last = off // self.bs, (off + ln - 1) // self.bs
        data = bytearray()
        with open(self.ucas, 'rb') as f:
            for k in range(first, last + 1):
                e = self.blocks[k * self.cbes:(k + 1) * self.cbes]
                boff = int.from_bytes(e[0:5], 'little'); csz = int.from_bytes(e[5:8], 'little'); usz = int.from_bytes(e[8:11], 'little'); m = e[11]
                f.seek(boff); raw = f.read(csz)
                data += raw[:usz] if m == 0 else oodle(raw, usz)
        start = off - first * self.bs
        return bytes(data[start:start + ln])

    def file_index(self):
        """path -> toc entry index, from the directory index."""
        d = self.dirindex; p = 0
        if len(d) < 4: return {}
        mount, p = fstring(d, p)
        n, = struct.unpack_from('<i', d, p); p += 4
        dirs = [struct.unpack_from('<4I', d, p + i * 16) for i in range(n)]; p += n * 16
        n, = struct.unpack_from('<i', d, p); p += 4
        fents = [struct.unpack_from('<3I', d, p + i * 12) for i in range(n)]; p += n * 12
        n, = struct.unpack_from('<i', d, p); p += 4
        strings = []
        for _ in range(n):
            s, p = fstring(d, p); strings.append(s)
        out = {}; NONE = 0xFFFFFFFF
        stack = [(0, '')] if dirs else []
        while stack:
            i, prefix = stack.pop()
            while i != NONE:
                name, child, sib, ffile = dirs[i]
                here = prefix + (strings[name] + '/' if name != NONE else '')
                f = ffile
                while f != NONE:
                    fn, nxt, ud = fents[f]; out[mount + here + strings[fn]] = ud; f = nxt
                if child != NONE: stack.append((child, here))
                i = sib
        return out

def find(paks, suffix):
    for t in sorted(glob.glob(os.path.join(paks, '*.utoc'))):
        c = Container(t)
        for path, idx in c.file_index().items():
            if path.endswith(suffix): yield c, path, idx

if __name__ == '__main__':
    paks, suffix = sys.argv[1], sys.argv[2]
    for c, path, idx in find(paks, suffix):
        data = c.read_entry(idx)
        hasver, hsize = struct.unpack_from('<II', data, 0); pflags, = struct.unpack_from('<I', data, 16)
        print(path, len(data), 'bytes; headerSize', hsize, 'packageFlags', hex(pflags), 'UNVERSIONED' if pflags & 0x2000 else 'tagged', 'methods', c.methods)
        open(os.path.join(os.path.dirname(__file__), os.path.basename(path) + '.bin'), 'wb').write(data)
        break
