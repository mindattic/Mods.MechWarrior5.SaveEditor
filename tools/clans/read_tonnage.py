"""Reads each Clan chassis's tonnage from the game's own _MDA asset (M5SE-T52). Read-only.
Usage: python -I tools/clans/read_tonnage.py "<MW5Clans/Content/Paks>"

Clans cooks unversioned properties (no names), so the field is located empirically: the int32 that follows the byte
context 000b00080103 in each chassis's main _MDA export. Calibrated on the 25 pre-existing catalog chassis only, it
occurs exactly once in every one and equals its tonnage (25/25). Read the same way, 2026-11-10: FMT 20, IFR 45,
INC 30, JR7IIC 35, NGA 80, matching ClansCatalog. Rerun after a game patch; a CHECK line means the layout moved."""
import glob, os, re, struct, sys, collections
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from iostore import Container
from zen import exports

paks = sys.argv[1]
ORIGINAL = {'ACH':30,'ADR':35,'BANE':100,'DWF':100,'EBJ':65,'EXE':95,'GAR':80,'HBK-IIC':50,'HBR':65,'HMN':50,'KDK':100,'KFX':30,'LBK':65,'MDD':60,'MLX':25,'NTG':75,'NVA':50,'RFLIIC':65,'SCR':55,'SHC':45,'SHD-IIC':45,'SMN':70,'TBR':75,'VPR':40,'WHK':85}
NEW = ['FMT', 'IFR', 'INC', 'JR7IIC', 'NGA']
def base(code): return next((p for p in ('HBK-IIC','SHD-IIC','JR7IIC','RFLIIC') if code.startswith(p)), code.split('-')[0])
mda = {}
for t in sorted(glob.glob(os.path.join(paks, '*.utoc'))):
    c = Container(t)
    for path, idx in c.file_index().items():
        m = re.search(r'/Art/Mechs/[^/]+/MDA/([^/]+)_MDA\.uasset$', path)
        if m: mda.setdefault(base(m.group(1)), []).append((m.group(1), c, idx))
def main_export(code):
    variant, c, idx = sorted(mda[code])[0]
    ex = exports(c.read_entry(idx))
    return variant, next(b for n, _, b in ex if n.endswith('_MDA'))
ctx_sets = []
for code, tons in ORIGINAL.items():
    _, b = main_export(code)
    ctx = set()
    for o in range(8, len(b) - 3):
        if struct.unpack_from('<i', b, o)[0] == tons:
            for k in (4, 6, 8): ctx.add(b[o - k:o])
    ctx_sets.append(ctx)
common = set.intersection(*ctx_sets)
print('contexts shared by all 25 originals:', [c.hex() for c in sorted(common, key=len, reverse=True)][:6])
if common:
    key = max(common, key=len)
    print('using context', key.hex())
    for code in list(ORIGINAL) + NEW:
        variant, b = main_export(code)
        vals = [struct.unpack_from('<i', b, m.end())[0] for m in re.finditer(re.escape(key), b) if m.end() + 4 <= len(b)]
        tag = '' if code in NEW else ('ok' if ORIGINAL[code] in vals and len(set(vals)) == 1 else f'CHECK want {ORIGINAL[code]}')
        print(f'  {code:8} {variant:10} read {vals}  {tag}')
