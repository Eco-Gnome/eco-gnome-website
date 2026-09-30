# Stub minimal de bundle de formes (format lu par building-planner.js) : un set « MortaredStone » avec
# un cube, un pan de toit (prisme), un mur mince + table de voisinage, et un atlas 2048² à 4 tuiles.
import json, os

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'stub')
os.makedirs(OUT, exist_ok=True)

FACES = [(1, 0, 0), (-1, 0, 0), (0, 1, 0), (0, -1, 0), (0, 0, 1), (0, 0, -1)]

def box(x0, x1, y0, y1, z0, z1):
    """Pavé : 6 faces × 4 sommets, normales de face, 12 triangles."""
    lo, hi = (x0, y0, z0), (x1, y1, z1)
    pos, nrm, idx = [], [], []
    for k, d in enumerate(FACES):
        ax = 0 if d[0] else 1 if d[1] else 2
        u, w = (ax + 1) % 3, (ax + 2) % 3
        for cu, cw in ((0, 0), (1, 0), (1, 1), (0, 1)):
            p = [0.0, 0.0, 0.0]
            p[ax] = hi[ax] if d[ax] > 0 else lo[ax]
            p[u] = hi[u] if cu else lo[u]
            p[w] = hi[w] if cw else lo[w]
            pos += p; nrm += list(map(float, d))
        b = k * 4
        idx += [b, b + 1, b + 2, b, b + 2, b + 3]
    return {'pos': pos, 'nrm': nrm, 'idx': idx}

def wedge():
    """Pan de toit descendant vers +x : bas plein, face arrière (x = −0.5) pleine, pente de (−0.5, z=0.5) à (0.5, z=−0.5).
    v3 : uv + chan par sommet (dessous = dessus triplanaire 1, dos et côtés = côté 0, pente = détail 2 aux uv)."""
    s = 2 ** -0.5
    pos, nrm, idx, uv, chan = [], [], [], [], []
    def quad(pts, n, ch, uvs=None):
        b = len(pos) // 3
        for k, p in enumerate(pts): pos.extend(p); nrm.extend(n); uv.extend(uvs[k] if uvs else (0, 0)); chan.append(ch)
        idx.extend([b, b + 1, b + 2, b, b + 2, b + 3])
    quad([(-0.5, -0.5, -0.5), (0.5, -0.5, -0.5), (0.5, 0.5, -0.5), (-0.5, 0.5, -0.5)], (0, 0, -1), 1)      # dessous
    quad([(-0.5, -0.5, -0.5), (-0.5, 0.5, -0.5), (-0.5, 0.5, 0.5), (-0.5, -0.5, 0.5)], (-1, 0, 0), 0)      # dos
    quad([(-0.5, -0.5, 0.5), (-0.5, 0.5, 0.5), (0.5, 0.5, -0.5), (0.5, -0.5, -0.5)], (s, 0, s), 2, [(0, 1), (1, 1), (1, 0.25), (0, 0.25)])   # pente
    # deux triangles latéraux
    for y, n in ((-0.5, (0, -1, 0)), (0.5, (0, 1, 0))):
        b = len(pos) // 3
        for p in ((-0.5, y, -0.5), (0.5, y, -0.5), (-0.5, y, 0.5)): pos.extend(p); nrm.extend(n); uv.extend((0, 0)); chan.append(0)
        idx.extend([b, b + 1, b + 2])
    return {'pos': pos, 'nrm': nrm, 'idx': idx, 'uv': uv, 'chan': chan}

T = 0.1875   # demi-épaisseur du mur (37,5 / 100 / 2)
meshes = {
    'Cube': box(-0.5, 0.5, -0.5, 0.5, -0.5, 0.5),
    'RoofSide': wedge(),
    'Wall': box(-0.5, 0.5, -T, T, -0.5, 0.5),               # mur le long de x
    'Wall_Corner': box(-0.5, T, -T, 0.5, -0.5, 0.5),        # L : bras vers +x... simplifié en un pavé
    'T_Wall': box(-0.5, 0.5, -T, 0.5, -0.5, 0.5),
    'X_Wall': box(-0.5, 0.5, -0.5, 0.5, -0.5, 0.5),
    'Pillar': box(-T, T, -T, T, -0.5, 0.5),
}

def window():
    """Fenêtre le long de x : cadre = pavé opaque (idx), vitre = quad à y = 0 vu des deux côtés, dans glass (mêmes sommets)."""
    m = box(-0.5, 0.5, -T, T, -0.5, 0.5)
    glass = []
    for ny in (-1.0, 1.0):
        b = len(m['pos']) // 3
        for p in ((-0.4, 0.0, -0.4), (0.4, 0.0, -0.4), (0.4, 0.0, 0.4), (-0.4, 0.0, 0.4)):
            m['pos'] += list(p); m['nrm'] += [0.0, ny, 0.0]
        glass += [b, b + 1, b + 2, b, b + 2, b + 3]
    m['glass'] = glass
    return m

meshes['Window'] = window()
meshes['Wall_End'] = box(-0.5, 0.0, -T, T, -0.5, 0.5)
meshes['Column_Top'] = box(-T, T, -T, T, -0.5, 0.4)
meshes['Column_Bottom'] = box(-T, T, -T, T, -0.4, 0.5)

# Cas de mur écrits à la main (format v2, rotations expansées), voisins E, S, W, N (repère plan : x est, y sud).
# Ordre : 0 X ; 1-4 T (côté manquant E,S,W,N) ; 5-8 coin (paire ES, SW, WN, NE) ; 9 droit EW ; 10 droit SN ;
# 11-14 bout (un seul voisin E,S,W,N) ; 15 pilier (repli, sans conds).
DIRS = [[1, 0, 0], [0, 1, 0], [-1, 0, 0], [0, -1, 0]]
def conds(bits): return [DIRS[i] + [bits[i]] for i in range(4)]
wall_cases = [{'mesh': 'X_Wall', 'rot': 0, 'conds': conds([1, 1, 1, 1])}]
for i in range(4): wall_cases.append({'mesh': 'T_Wall', 'rot': i * 90, 'conds': conds([0 if j == i else 1 for j in range(4)])})
for i in range(4): wall_cases.append({'mesh': 'Wall_Corner', 'rot': i * 90, 'conds': conds([1 if j in (i, (i + 1) % 4) else 0 for j in range(4)])})
wall_cases.append({'mesh': 'Wall', 'rot': 0, 'conds': [DIRS[0] + [1], DIRS[2] + [1]]})
wall_cases.append({'mesh': 'Wall', 'rot': 90, 'conds': [DIRS[1] + [1], DIRS[3] + [1]]})
for i in range(4): wall_cases.append({'mesh': 'Wall_End', 'rot': i * 90, 'conds': [DIRS[i] + [1]]})
wall_cases.append({'mesh': 'Pillar', 'rot': 0, 'conds': []})

bundle = {
    'set': 'MortaredStone', 'version': 3, 'tile': 512,
    'meshes': meshes,
    'forms': {
        'RoofSide': [{'mesh': 'RoofSide', 'rot': r * 90} for r in range(4)],
        'Stairs': [{'mesh': 'RoofSide', 'rot': r * 90 + 90} for r in range(4)],   # importRotation cuite : même mesh, +90
        'Cube': [{'mesh': 'Cube', 'rot': 0}],
        'Wall': {'bitMeans': 'category:Building', 'cases': wall_cases},
        'Column': {'bitMeans': 'sameType', 'cases': [
            {'mesh': 'Pillar', 'rot': 0, 'conds': [[0, 0, 1, 1], [0, 0, -1, 1]]},
            {'mesh': 'Column_Bottom', 'rot': 0, 'conds': [[0, 0, 1, 1], [0, 0, -1, 0]]},
            {'mesh': 'Column_Top', 'rot': 0, 'conds': [[0, 0, 1, 0], [0, 0, -1, 1]]},
            {'mesh': 'Pillar', 'rot': 0, 'conds': []}]},
        # Dalle : un mur (catégorie Building, surcharge du bitMeans par le 5e élément) au-dessus → cube, sinon pilier.
        'Floor': {'bitMeans': 'sameType', 'cases': [
            {'mesh': 'Cube', 'rot': 0, 'conds': [[0, 0, 1, 1, 'category:Building']]},
            {'mesh': 'Pillar', 'rot': 0, 'conds': []}]},
        # Aucun cas satisfait possible → cube du set.
        'RoofPeak': {'bitMeans': 'sameType', 'cases': [{'mesh': 'Pillar', 'rot': 0, 'conds': [[0, 0, 0, 0]]}]},
        'Broken': [{'mesh': 'NoSuchMesh', 'rot': 0}] * 4,
        'WindowGrilles': {'bitMeans': 'category:Building', 'cases': [{'mesh': 'Window', 'rot': 0, 'conds': []}]},
        # v4 : forme orientée et contextuelle (quais) : un jeu de cas par rotation de l'op ; r2 absent → aucun cas (cube du set).
        # r0 : dessous = barrière de même sens (catégorie par rotation) → Column_Top, sinon Wall ; r1 : dessus plein (solid) → Pillar, sinon Wall à 90°.
        'DocksFenceMid': {'rots': [
            {'bitMeans': 'category:DocksFenceRope1', 'cases': [{'mesh': 'Column_Top', 'rot': 0, 'conds': [[0, 0, -1, 1]]}, {'mesh': 'Wall', 'rot': 0, 'conds': []}]},
            {'bitMeans': 'solid', 'cases': [{'mesh': 'Pillar', 'rot': 90, 'conds': [[0, 0, 1, 1]]}, {'mesh': 'Wall', 'rot': 90, 'conds': []}]},
            None,
            {'cases': [{'mesh': 'Wall', 'rot': 270, 'conds': []}]}]},
        # Logs empilés : un voisin plein au-dessus → cube, sinon pile.
        'Stacked1': {'bitMeans': 'solid', 'cases': [{'mesh': 'Cube', 'rot': 0, 'conds': [[0, 0, 1, 1]]}, {'mesh': 'Pillar', 'rot': 0, 'conds': []}]},
    },
    'glass': [0.1, 0.1, 0.3, 0.5],
    'categories': {'Wall': 'Building', 'Floor': 'Terrain', 'Cube': 'Terrain', 'Column': 'Default', 'RoofSide': 'Terrain', 'Stairs': 'Terrain', 'RoofPeak': 'Terrain', 'WindowGrilles': 'Building',
                   'DocksFenceMid': ['DocksFenceRope1', 'DocksFenceRope2', 'DocksFenceRope1', 'DocksFenceRope2'], 'Stacked1': 'Default'},
    # v3 : matériaux à trois tuiles (côté, dessus, détail) ; skins = index de matériau ; surcharge par forme (toit du grès → matériau 1).
    'materials': [{'side': 0, 'top': 1, 'detail': 2, 'scale': 0.2, 'offset': 0.5}, {'side': 3, 'top': None, 'detail': None, 'scale': 1, 'offset': 0}],
    'skins': {'MortaredSandstoneItem': 0, 'MortaredGraniteItem': 0, 'MortaredLimestoneItem': 0, 'MortaredStoneItem': 1},
    'formMaterials': {'MortaredSandstoneItem': {'RoofSide': 1}},
    # Bloc à plusieurs matériaux : slot FBX 1 sur le matériau 1, slot 2 sans matériau (repli sur celui de la forme).
    'slotMaterials': {'MortaredSandstoneItem': {'Wall': [1, None]}},
}
with open(os.path.join(OUT, 'MortaredStone.json'), 'w') as f: json.dump(bundle, f)

# Bundle d'objets (extract_objects.py) : une porte 1×2 (pavé fin le long de x, deux cases de haut) dessinée à sa case d'ancrage.
objects = {
    'set': 'Objects', 'version': 1, 'kind': 'objects', 'tile': 512,
    'meshes': {'Door': box(-0.4, 0.4, -0.1, 0.1, -0.5, 1.5)},
    'materials': [{'side': 0, 'top': None, 'detail': 0, 'scale': 1.0, 'offset': 0.0}],
    'objects': {'HewnDoorItem': {'parts': [{'mesh': 'Door', 'material': 0}], 'rot': [0, 270, 180, 90], 'cells': [[0, 0, 0], [0, 0, 1]]},
                'Broken': {'parts': [{'mesh': 'NoSuchMesh', 'material': 0}], 'rot': [0, 270, 180, 90], 'cells': [[0, 0, 0]]}},
}
with open(os.path.join(OUT, 'Objects.json'), 'w') as f: json.dump(objects, f)
# Tuyaux (extract_pipes.py) : une forme par métal, prédicat « type: » (même métal ou prise d'objet), forme par défaut du skin.
# Cas du cuivre : droit le long de y (±y raccordés, ±x libres), coude +x/+y, bouchon (seul −y raccordé), repli isolé.
pipe_free = lambda *offs: [[o[0], o[1], o[2], 0] for o in offs]
pipes = {
    'set': 'Pipes', 'version': 4, 'tile': 512, 'calibration': {'axis': 'x,z,y'},
    'meshes': {'PStraightY': box(-0.1, 0.1, -0.5, 0.5, -0.4, -0.2), 'PBend': box(-0.1, 0.5, -0.1, 0.5, -0.4, -0.2),
               'PCapN': box(-0.1, 0.1, -0.5, 0.1, -0.4, -0.2), 'PSolo': box(-0.1, 0.1, -0.1, 0.1, -0.4, -0.2)},
    'forms': {
        'CopperPipe': {'bitMeans': 'type:CopperPipe,PipeSlot', 'cases': [
            {'mesh': 'PStraightY', 'rot': 0, 'conds': [[0, 1, 0, 1], [0, -1, 0, 1]] + pipe_free((1, 0, 0), (-1, 0, 0))},
            {'mesh': 'PBend', 'rot': 0, 'conds': [[1, 0, 0, 1], [0, 1, 0, 1]] + pipe_free((-1, 0, 0), (0, -1, 0))},
            {'mesh': 'PCapN', 'rot': 0, 'conds': [[0, -1, 0, 1]] + pipe_free((0, 1, 0), (1, 0, 0), (-1, 0, 0))},
            {'mesh': 'PSolo', 'rot': 0, 'conds': []}]},
        'IronPipe': {'bitMeans': 'type:IronPipe,PipeSlot', 'cases': [
            {'mesh': 'PStraightY', 'rot': 0, 'conds': [[0, 1, 0, 1], [0, -1, 0, 1]]},
            {'mesh': 'PSolo', 'rot': 0, 'conds': []}]},
    },
    'categories': {'CopperPipe': 'Pipe', 'IronPipe': 'Pipe'},
    'materials': [{'side': 0, 'top': None, 'detail': 0, 'scale': 1.0, 'offset': 0.0}],
    'skins': {'CopperPipeItem': 0, 'IronPipeItem': 0}, 'skinForms': {'CopperPipeItem': 'CopperPipe', 'IronPipeItem': 'IronPipe'}, 'formMaterials': {},
}
with open(os.path.join(OUT, 'Pipes.json'), 'w') as f: json.dump(pipes, f)
with open(os.path.join(OUT, 'index.json'), 'w') as f:
    json.dump({'sets': {'MortaredStone': {'json': 'MortaredStone.json', 'atlas': 'MortaredStone.webp'}, 'Objects': {'json': 'Objects.json', 'atlas': 'Objects.webp'}, 'Pipes': {'json': 'Pipes.json', 'atlas': 'Pipes.webp'}},
               'materials': dict({k: 'MortaredStone' for k in bundle['skins']}, **{k: 'Pipes' for k in pipes['skins']}), 'objects': {'HewnDoorItem': 'Objects', 'Broken': 'Objects'}}, f, indent=1)

# Atlas 2048² : 4 tuiles 512² colorées avec un appareil de briques, pour le test navigateur.
try:
    from PIL import Image, ImageDraw
    img = Image.new('RGB', (2048, 2048), (20, 20, 20))
    d = ImageDraw.Draw(img)
    colors = [(214, 190, 150), (150, 150, 160), (225, 220, 200), (120, 115, 110)]
    for t, c in enumerate(colors):
        ox, oy = (t % 4) * 512, (t // 4) * 512
        d.rectangle([ox, oy, ox + 511, oy + 511], fill=c)
        dark = tuple(max(0, v - 50) for v in c)
        for row in range(8):
            y = oy + row * 64
            d.line([ox, y, ox + 511, y], fill=dark, width=4)
            for col in range(4):
                x = ox + col * 128 + (64 if row % 2 else 0)
                d.line([x, y, x, y + 63], fill=dark, width=4)
    try:
        img.save(os.path.join(OUT, 'MortaredStone.webp'), 'WEBP', quality=80)
    except Exception as e:
        print('webp failed, png instead:', e); img.save(os.path.join(OUT, 'MortaredStone.png'))
except ImportError:
    print('PIL absent : pas d\'atlas')
print('stub written to', OUT, sorted(os.listdir(OUT)))
