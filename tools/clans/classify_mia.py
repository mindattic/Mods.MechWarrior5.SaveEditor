"""Classifies every Clans _MIA by the tags in its Zen name map (no property schema needed). Read-only.
Usage: python -I tools/clans/classify_mia.py "<MW5Clans/Content/Paks>"
M5SE-T46, 2026-11-10: 54 Clan-origin variants (30 chassis, all on the market) + 351 Inner Sphere enemy variants."""
import collections, glob, os, re, struct, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from iostore import Container

def names(d):
    hasver, = struct.unpack_from('<I', d, 0)
    o = 4 + 4 + 8 + 4 + 4 + 4 * 7
    if hasver:
        o += 4 + 8 + 4; n, = struct.unpack_from('<i', d, o); o += 4 + n * 20
    num, = struct.unpack_from('<I', d, o); o += 16 + num * 8
    lens = [struct.unpack_from('>h', d, o + i * 2)[0] for i in range(num)]; o += num * 2
    out = []
    for L in lens:
        if L < 0: out.append(d[o:o - L * 2].decode('utf-16-le')); o += -L * 2
        else: out.append(d[o:o + L].decode('latin-1')); o += L
    return out

def main():
    paks = sys.argv[1]
    rows = []
    for t in sorted(glob.glob(os.path.join(paks, '*.utoc'))):
        c = Container(t)
        for path, idx in c.file_index().items():
            m = re.search(r'/Mechs/([^/]+)/MDA/([^/]+)_MIA\.uasset$', path)
            if not m: continue
            ns = names(c.read_entry(idx))
            origin = next((n.split('.')[-1] for n in ns if n.startswith('ItemCollection.Origin.')), '?')
            plugin = re.search(r'/Plugins/([^/]+)/', path)
            rows.append((m.group(1), m.group(2), origin, any('Market_Mech' in n for n in ns), any('OmniMech' in n for n in ns), plugin.group(1) if plugin else 'base'))
    by = collections.Counter((r[2], r[3]) for r in rows)
    print('variants by (origin, on market):', dict(by))
    clan = collections.defaultdict(lambda: [set(), set(), set()])
    for ch, code, origin, market, omni, plug in rows:
        if origin == 'Clan':
            clan[ch][0].add(code); clan[ch][1].add(plug); clan[ch][2].add('market' if market else 'no-market')
    print(len(clan), 'Clan-origin chassis:')
    for ch, (codes, plugs, mk) in sorted(clan.items()):
        print(f'  {ch:16} {",".join(sorted(plugs)):12} {"/".join(sorted(mk)):18} {", ".join(sorted(codes))[:80]}')

if __name__ == '__main__':
    main()
