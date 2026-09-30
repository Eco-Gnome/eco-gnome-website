"""Vitrine FarEast : toutes les formes du bundle, plan JSON importable dans le planner.

    python Scripts/forms/fixtures/make_fareast_showcase.py ecocraft/wwwroot/assets/forms/FarEast.json Scripts/forms/fixtures/fareast-showcase.json

Orientation des pièces à bras (coins, T, bouts, barrières) choisie d'après la bbox du mesh tourné dans le bundle. Aucune
zone fermée (brèche dans la barrière, porte des maisons) : le planner n'y détecte aucune pièce.
"""
import json, sys, math

bundle = json.load(open(sys.argv[1], encoding='utf-8'))
M = 'FarEastLumberItem'
ops = []


def op(form, a, b=None, rot=0, name=None, **kw):
    ops.append(dict({'id': 'g%d' % (len(ops) + 1), 'kind': 'box', 'subtract': False, 'material': M, 'a': list(a), 'b': list(b or a),
                     'form': form, 'rot': rot, 'name': name or '%s rot %d' % (form, rot)}, **kw))


def entry(form, r):
    f = bundle['forms'][form]
    if isinstance(f, list): return f[r] if len(f) == 4 else f[0]
    g = f['rots'][r] if 'rots' in f else f
    return next((c for c in g['cases'] if not c['conds']), g['cases'][-1])


def reach(form, r):
    """Côtés (E, S, W, N) atteints par le mesh de la forme en rotation r."""
    e = entry(form, r)
    pos = bundle['meshes'][e['mesh']]['pos']
    c, s = round(math.cos(math.radians(e['rot']))), round(math.sin(math.radians(e['rot'])))
    pts = [(pos[i] * c - pos[i + 1] * s, pos[i] * s + pos[i + 1] * c) for i in range(0, len(pos), 3)]
    return {d for d, ok in (('E', max(p[0] for p in pts) > 0.45), ('S', max(p[1] for p in pts) > 0.45),
                            ('W', min(p[0] for p in pts) < -0.45), ('N', min(p[1] for p in pts) < -0.45)) if ok}


def rot_for(form, need):
    """Rotation dont le mesh atteint exactement les côtés `need`, sinon qui les contient."""
    for exact in (True, False):
        for r in range(4):
            got = reach(form, r)
            if (got == set(need)) if exact else set(need) <= got: return r
    raise SystemExit('%s: no rotation reaches %s' % (form, need))


Z = 1
# 1. Les 19 murs, en rotation 0 (y = 1) et 1 (y = 3).
for i in range(19):
    op('Wall_%02d' % (i + 1), (1 + 2 * i, 1, Z), (1 + 2 * i, 1, Z + 1), 0, 'Wall_%02d rot 0' % (i + 1))
    op('Wall_%02d' % (i + 1), (1 + 2 * i, 3, Z), (1 + 2 * i, 3, Z + 1), 1, 'Wall_%02d rot 1' % (i + 1))
# 2. Coins, T, X (y = 6), colonnes, fenêtres, cubes, empilés, échelle (y = 9).
x = 1
for fam in ('WallCorner', 'WallT', 'WallX'):
    for i in range(1, 5):
        op('%s_%02d' % (fam, i), (x, 6, Z), (x, 6, Z + 1)); x += 2
for f in ('Column_01', 'Column_02', 'Column_03'):
    op(f, (x, 6, Z), (x, 6, Z + 2)); x += 2
x = 1
for f, rot in (('Window', 0), ('Window', 1)):
    op(f, (x, 9, Z), (x, 9, Z + 1), rot); x += 2
op('WindowGrilles', (x, 9, Z), (x + 2, 9, Z + 1), 0, 'WindowGrilles 3x2'); x += 4
op('Column', (x, 9, Z), (x, 9, Z + 2), 0, 'Column stack of 3'); x += 2
for f in ('Cube', 'RoofCube', 'Stacked1', 'Stacked2', 'Stacked3'):
    op(f, (x, 9, Z)); x += 2
op('Wall_01', (x, 9, Z), (x, 9, Z + 2), 0, 'Wall_01 behind the ladder')
op('Ladder', (x, 10, Z), (x, 10, Z + 2), 0, 'Ladder 3 high'); x += 2

# 3. Barrière en boucle 7 x 5 sur 2 hauteurs (y = 13..17), avec un T, une croix, un bout et une barrière seule.
x0, y0, x1, y1 = 1, 13, 7, 17
for x in range(x0 + 1, x1):
    for y in (y0, y1):
        if (x, y) not in ((x0 + 2, y0), (x1 - 1, y0)): op('FenceMid', (x, y, Z), (x, y, Z + 1), rot_for('FenceMid', 'EW'), 'FenceMid along x')   # deux brèches
for y in range(y0 + 1, y1):
    for x in (x0, x1): op('FenceMid', (x, y, Z), (x, y, Z + 1), rot_for('FenceMid', 'NS'), 'FenceMid along y')
for (x, y), need in (((x0, y0), 'ES'), ((x1, y0), 'WS'), ((x1, y1), 'WN'), ((x0, y1), 'EN')):
    op('FenceCorner', (x, y, Z), (x, y, Z + 1), rot_for('FenceCorner', need), 'FenceCorner ' + need)
# T sur le côté sud de la boucle vers l'intérieur, croix au centre, bout.
xt = 4
ops = [o for o in ops if not (o['form'] == 'FenceMid' and o['a'][:2] == [xt, y1])]
op('FenceT', (xt, y1, Z), (xt, y1, Z + 1), rot_for('FenceT', 'EWN'), 'FenceT')
op('FenceMid', (xt, y1 - 1, Z), (xt, y1 - 1, Z + 1), rot_for('FenceMid', 'NS'), 'FenceMid along y')
op('FenceX', (xt, y1 - 2, Z), (xt, y1 - 2, Z + 1), 0, 'FenceX')
op('FenceEnd', (xt - 1, y1 - 2, Z), (xt - 1, y1 - 2, Z + 1), rot_for('FenceEnd', 'E'), 'FenceEnd toward E')
op('FenceEnd', (xt + 1, y1 - 2, Z), (xt + 1, y1 - 2, Z + 1), rot_for('FenceEnd', 'W'), 'FenceEnd toward W')
op('FenceEnd', (xt, y1 - 3, Z), (xt, y1 - 3, Z + 1), rot_for('FenceEnd', 'S'), 'FenceEnd toward S')
op('FenceSolo', (x1 + 2, y0, Z), (x1 + 2, y0, Z + 1), 0, 'FenceSolo 2 high')

# 4. Escaliers : chaque forme en 4 rotations (y = 20), volée de 3 de large sur 3 marches (x = 20..22, y = 13..15).
x = 1
for f in ('StairsMid', 'StairsEndLeft', 'StairsEndRight', 'StairsTurn', 'StairsCorner'):
    for r in range(4):
        op(f, (x, 20, Z), None, r); x += 2
    x += 1
for k in range(3):
    xs = 22 - k
    op('StairsEndLeft', (xs, 13, Z + k), None, 0, 'flight rot 0 step %d' % k)
    op('StairsMid', (xs, 14, Z + k), None, 0, 'flight rot 0 step %d' % k)
    op('StairsEndRight', (xs, 15, Z + k), None, 0, 'flight rot 0 step %d' % k)

# 5. Dalle 5 x 4 (x = 26..30) et plafond 5 x 4 (x = 32..36), y = 13..16.
op('Floor', (26, 13, Z), (30, 16, Z), 0, 'Floor 5x4')
op('Ceiling', (32, 13, Z), (36, 16, Z), 0, 'Ceiling 5x4')

# 6. Formes de toit seules, 4 rotations, 4 formes par rangée (y = 23, 26, 29, 32).
roofs = ['RoofSide', 'RoofCorner', 'RoofTurn', 'RoofPeak', 'RoofEdgeSide', 'RoofEdgeCorner', 'RoofEdgeTurn', 'RoofPeakCorner', 'RoofPeakT',
         'RoofUnderslopeSide', 'RoofUnderslopeTurn', 'RoofUnderslopeCorner', 'UnderInnerPeak']
for n, f in enumerate(roofs):
    x, y = 1 + (n % 4) * 9, 23 + (n // 4) * 3
    for r in range(4): op(f, (x + 2 * r, y, Z), None, r)
op('RoofPeakX', (10, 32, Z))


# 7. Maison 7 x 5, murs Wall_01 sur 3 hauteurs, toit à 4 pans (convention du générateur : côté bas de RoofSide r
#    vers E, N, W, S ; angle bas de RoofCorner r0 au sud-est), première course en rives (RoofEdge*).
def house(X0, Y0, edge):
    X1, Y1 = X0 + 6, Y0 + 4
    for xx in range(X0 + 1, X1):
        for yy in (Y0, Y1):
            if (xx, yy) != (X0 + 3, Y0): op('Wall_01', (xx, yy, Z), (xx, yy, Z + 2), 0, 'house wall x')   # porte au nord
    for yy in range(Y0 + 1, Y1):
        for xx in (X0, X1): op('Wall_01', (xx, yy, Z), (xx, yy, Z + 2), 1, 'house wall y')
    for (xx, yy), need in (((X0, Y0), 'ES'), ((X1, Y0), 'WS'), ((X1, Y1), 'WN'), ((X0, Y1), 'EN')):
        op('WallCorner_01', (xx, yy, Z), (xx, yy, Z + 2), rot_for('WallCorner_01', need), 'house corner ' + need)
    lu, hu, lv, hv = X0, X1, Y0, Y1
    for k in range(2):
        z = Z + 3 + k
        side, corner = ('RoofEdgeSide', 'RoofEdgeCorner') if edge and k == 0 else ('RoofSide', 'RoofCorner')
        op(side, (lu + 1, lv, z), (hu - 1, lv, z), 1, 'roof N')
        op(side, (lu + 1, hv, z), (hu - 1, hv, z), 3, 'roof S')
        op(side, (lu, lv + 1, z), (lu, hv - 1, z), 2, 'roof W')
        op(side, (hu, lv + 1, z), (hu, hv - 1, z), 0, 'roof E')
        for (xx, yy), r in (((lu, lv), 2), ((hu, lv), 1), ((lu, hv), 3), ((hu, hv), 0)): op(corner, (xx, yy, z), None, r, 'roof corner')
        lu += 1; hu -= 1; lv += 1; hv -= 1
    op('RoofPeak', (lu, lv, Z + 5), (hu, hv, Z + 5), 0, 'ridge along x')


house(26, 1, False)
house(35, 1, True)

W, D = 44, 35
plan = {'schemaVersion': 4, 'name': 'Formes FarEastLumberItem', 'mode': 'architecture', 'grid': {'width': W, 'depth': D},
        'architecture': {'height': 8, 'ops': ops},
        'defaults': {'wallHeight': 3},
        'levels': [{'name': 'RDC', 'height': 3, 'rooms': [], 'objects': []}],
        'groundIndex': 0, 'analysis': {'residents': 1, 'propertyType': 'Residence'}, 'prices': {}}
used = {o['form'] for o in ops}
missing = [f for f in bundle['forms'] if f not in used]
assert all(0 <= o['a'][0] and o['b'][0] < W and 0 <= o['a'][1] and o['b'][1] < D for o in ops), 'op outside the grid'
print('%d ops, forms used %d / %d, missing %s' % (len(ops), len(used & set(bundle['forms'])), len(bundle['forms']), missing))
json.dump(plan, open(sys.argv[2], 'w', encoding='utf-8'), indent=1, ensure_ascii=False)
