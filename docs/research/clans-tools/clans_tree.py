"""Parses a decompressed MW5 Clans save (UE 5.4+ tags) into a value tree and prints a census of the inventory actors."""
import collections
import json
import struct
import sys

NATIVE = {"Guid", "LinearColor", "Color", "Vector", "Vector2D", "Vector4", "Rotator", "Quat", "DateTime", "Timespan",
          "IntPoint", "IntVector", "GameplayTagContainer", "GameplayTag", "FrameNumber"}
BLOBS = {"PersistentModelData", "ByteData", "DeploymentData", "InventoryData"}


class R:
    def __init__(self, b, p, end):
        self.b, self.p, self.end = b, p, end

    def i32(self):
        v = struct.unpack_from("<i", self.b, self.p)[0]; self.p += 4; return v

    def u8(self):
        v = self.b[self.p]; self.p += 1; return v

    def fstr(self):
        n = self.i32()
        if n == 0:
            return ""
        if n < 0:
            s = self.b[self.p:self.p - 2 * n].decode("utf-16-le")[:-1]; self.p += -2 * n; return s
        s = self.b[self.p:self.p + n - 1].decode("latin-1"); self.p += n; return s

    def type_name(self):
        name = self.fstr()
        return (name, tuple(self.type_name() for _ in range(self.i32())))


def scalar(r, t, end):
    k = t[0]
    if k == "IntProperty": return r.i32()
    if k == "FloatProperty":
        v = struct.unpack_from("<f", r.b, r.p)[0]; r.p += 4; return round(v, 4)
    if k == "DoubleProperty":
        v = struct.unpack_from("<d", r.b, r.p)[0]; r.p += 8; return v
    if k == "Int64Property":
        v = struct.unpack_from("<q", r.b, r.p)[0]; r.p += 8; return v
    if k == "ObjectProperty":
        path = r.fstr()
        if r.p < end:  # inline object: zero byte + its properties
            r.u8()
            return {"__class": path, **props(r, end)}
        return path
    if k in ("StrProperty", "NameProperty", "SoftObjectProperty"): return r.fstr()
    if k == "EnumProperty": return r.fstr()
    if k == "ByteProperty":
        return r.fstr() if t[1] and end - r.p > 1 else r.u8()
    if k == "BoolProperty": return bool(r.u8())
    if k == "StructProperty" and t[1][0][0] in NATIVE:
        raw = r.b[r.p:end]; r.p = end
        if t[1][0][0] == "Guid": return raw.hex().upper()
        if t[1][0][0] == "DateTime": return {"ticks": struct.unpack("<q", raw)[0]}
        return "<" + t[1][0][0] + ">"
    raw = r.b[r.p:end]; r.p = end
    return f"<{k} {len(raw)}b>"


def props(r, end):
    out = {}
    while r.p < end:
        name = r.fstr()
        if name in ("None", ""):
            break
        TRACE.append((name, r.p))
        t = r.type_name()
        size = r.i32()
        flags = r.u8()
        if flags & 1: r.i32()
        if flags & 2: r.p += 16
        if flags & 4:
            if r.u8() & 2: r.u8()
        start = r.p
        if t[0] == "BoolProperty":
            val = bool(flags & 0x10)
        elif name in BLOBS and t[0] == "ArrayProperty":
            r.p = start + 4
            val = blob(r, start + size)
        elif t[0] == "StructProperty" and t[1][0][0] not in NATIVE and not flags & 8:
            val = props(R(r.b, start, start + size), start + size)
        elif t[0] == "ArrayProperty":
            sub = R(r.b, start, start + size)
            n = sub.i32()
            inner = t[1][0]
            if inner[0] == "StructProperty" and inner[1][0][0] not in NATIVE:
                val = [props(sub, start + size) for _ in range(n)]
            elif inner[0] == "StructProperty":
                each = (size - 4) // max(n, 1)
                val = [scalar(sub, inner, sub.p + each) for _ in range(n)]
            elif inner[0] in ("ObjectProperty", "SoftObjectProperty"):
                val = [sub.fstr() for _ in range(n)]
            elif inner[0] in ("IntProperty", "NameProperty", "StrProperty", "EnumProperty", "BoolProperty", "FloatProperty"):
                val = [scalar(sub, inner, start + size) for _ in range(n)]
            else:
                val = f"<array {show(inner)} x{n}>"
        else:
            val = scalar(r, t, start + size)
        key = name if name not in out else f"{name}#{len(out)}"
        out[key] = val
        r.p = start + size
    return out


def blob(r, end):
    """An object stream: [class FString] + zero byte + property list."""
    out = {}
    if r.b[r.p] != 0:
        out["__class"] = r.fstr()
    assert r.u8() == 0
    out.update(props(r, end))
    return out


def show(t):
    return t[0] + ("(" + ",".join(show(x) for x in t[1]) + ")" if t[1] else "")


data = open(sys.argv[1], "rb").read()
TRACE = []
try:
    tree = props(R(data, 5, len(data)), len(data))
except Exception as e:
    print("FAIL", e, [(n[:40], p) for n, p in TRACE[-15:]]); raise SystemExit
json.dump(tree, open(sys.argv[1] + ".json", "w"), indent=1, default=str)
actors = tree["InventoryData"]["Array"]
census = collections.Counter(a["ActorClass"] for a in actors)
for cls, n in census.most_common():
    sample = next(a for a in actors if a["ActorClass"] == cls)["ByteData"]
    print(f"{n:4} {cls}: {list(sample)[:14]}")
