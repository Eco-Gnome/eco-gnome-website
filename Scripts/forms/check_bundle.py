"""Vérifications d'un bundle de formes (v2 à v4).

    python Scripts/forms/check_bundle.py ecocraft/wwwroot/assets/forms/MortaredStone.json

Évalue les cas compilés (premier cas dont toutes les conds sont vraies) sur les 16 masques latéraux, bits verticaux
à 0, pour Wall et WindowGrilles ; la table Column ; la bbox de chaque mesh et les triangles attendus ; pour les
tuyaux (bitMeans `type:…`), les 64 masques des 6 voisins : le mesh choisi doit atteindre le bord de chaque face raccordée.
Code de sortie 1 si une vérification échoue.
"""
import json
import sys

# Mortared Stone : Stairs 40 sommets FBX, Wall 8, RoofSide 78 → triangles après triangulation.
EXPECTED_TRIS = {'MortaredStone': {'MortaredStoneStairs': 58, 'Wall': 12, 'MS_RoofSide': 122}}
OVERHANG = 0.12                                                   # débord toléré hors de la cellule (tuiles de toit, rampes de quai 0.108)
OVERHANG_BY_SET = {'FarEast': 0.41}                               # FarEast : avant-toits (0.31), limons d'escalier (0.4), haut d'échelle
OVERHANG_BY_MESH = {'RoofSupportCube': 0.36,                     # préfixe de mesh : avant-toit du RoofCube (0.2 à 0.351 selon le set)
                    'FlatRoof_CorrugatedSteel': 0.25, 'FR_': 0.33,  # rebords des toits plats T4
                    'RoofSlope': 0.32, 'RoofInnerCorner': 0.32, 'RoofCornerSlope': 0.32, 'Roof_Flat': 0.32,   # toit CL du béton, plus haut que la case
                    'FlatRoof_': 0.26, 'Pediment': 0.25,                           # T5 : rebords des toits plats, frontons (RampA-D)
                    'Column_Bottom': 0.19, 'Column_Top': 0.19, 'Collumn_': 0.19,  # T5 : colonnes larges (Ashlar, Composite)
                    'AshlarFloatingStairs': 0.19, 'flatsteel_stairs': 0.16}       # T5 : limons des escaliers flottants
# Angle bas de RoofCorner en rotation 0 (BuildingGenerator.CornerRot) : sud-est en FarEast et pour le coin CL (béton, T5 ;
# les bois du Composite Lumber sont des bundles Composite*Lumber), sud-ouest pour MS_RoofCorner.
CORNER_R0 = {'FarEast': (1, 1), 'ReinforcedConcrete': (1, 1), 'AshlarStone': (1, 1), 'FlatSteel': (1, 1), 'FramedGlass': (1, 1)}
# Colonnes sans mesh solo ni sommet dans le jeu (béton = bas seul, et bas retourné en haut de pile), ou aux noms numérotés.
COLUMN_MESHES = {'ReinforcedConcrete': {(): 'Cement_ColumnBottom', ((0, 0, -1),): 'Cement_ColumnBottom_X180'},
                 'FramedGlass': {((0, 0, 1),): 'FG_ColumnEnd', ((0, 0, -1),): 'FG_ColumnEnd_X180'},
                 'FlatSteel': {(): 'flatsteel_column_large_solo', ((0, 0, 1),): 'flatsteel_column_large_01', ((0, 0, -1),): 'flatsteel_column_large_03',
                               ((0, 0, -1), (0, 0, 1)): 'flatsteel_column_large_02'}}
# Murs aux meshes numérotés (flatsteel_wall_NN) : pas de contrôle des noms, les bras vers les voisins suffisent.
NUMBERED_WALLS = {'FlatSteel'}
LATERAL = [(1, 0, 0), (0, 1, 0), (-1, 0, 0), (0, -1, 0)]          # E S W N en repère planner
FACES = LATERAL + [(0, 0, 1), (0, 0, -1)]                         # + haut, bas


def evaluate(form, on):
    """Premier cas dont toutes les conds sont vraies ; `on` = offsets dont le voisin satisfait le prédicat."""
    for case in form['cases']:
        if all((tuple(c[:3]) in on) == bool(c[3]) for c in case['conds']): return case
    return None


def lateral_table(form):
    rows = []
    for combo in range(16):
        on = {LATERAL[i] for i in range(4) if (combo >> i) & 1}
        case = evaluate(form, on)
        rows.append((''.join(str((combo >> i) & 1) for i in range(4)), case['mesh'] if case else None, case['rot'] if case else -1))
    return rows


def rotated_bbox(mesh, deg):
    """bbox du mesh tourné de deg autour de +z (x' = x·cos − y·sin, y' = x·sin + y·cos)."""
    c, s = {0: (1, 0), 90: (0, 1), 180: (-1, 0), 270: (0, -1)}[deg % 360]
    pos = mesh['pos']
    pts = [(pos[i] * c - pos[i + 1] * s, pos[i] * s + pos[i + 1] * c, pos[i + 2]) for i in range(0, len(pos), 3)]
    return [min(p[k] for p in pts) for k in range(3)], [max(p[k] for p in pts) for k in range(3)]


def pipe_table(bundle, name, form):
    """64 masques des 6 voisins : un cas doit répondre, et son mesh doit toucher le bord de chaque face raccordée."""
    ok, used = True, {}
    for combo in range(64):
        on = {FACES[i] for i in range(6) if (combo >> i) & 1}
        case = evaluate(form, on)
        if case is None: print('  ! %s mask %s: no case' % (name, combo)); ok = False; continue
        lo, hi = rotated_bbox(bundle['meshes'][case['mesh']], case['rot'])
        for d in on:
            k = [i for i in range(3) if d[i]][0]
            reach = hi[k] if d[k] > 0 else -lo[k]
            if reach < 0.45:
                print('  ! %s mask %s: %s@%d does not reach %s' % (name, ''.join(str((combo >> i) & 1) for i in range(6)), case['mesh'], case['rot'], d)); ok = False
        used[case['mesh']] = used.get(case['mesh'], 0) + 1
    print('\n%s: %d cases, bitMeans %s, 64 masks -> %d meshes, faces reached: %s' % (name, len(form['cases']), form['bitMeans'], len(used), 'OK' if ok else 'FAILED'))
    return ok


def rotated_points(mesh, deg, mirror=False):
    c, s = {0: (1, 0), 90: (0, 1), 180: (-1, 0), 270: (0, -1)}[deg % 360]
    pos = mesh['pos']
    pts = [(-pos[i] if mirror else pos[i], pos[i + 1], pos[i + 2]) for i in range(0, len(pos), 3)]
    return [(x * c - y * s, x * s + y * c, z) for x, y, z in pts]


def floor_sides(bundle, name, form, mirror=False):
    """Dalle à bordure (FarEast) : le plancher (sommets à z = 0) va jusqu'au bord de chaque côté latéral dont le voisin
    est attendu, et s'arrête avant la bordure sinon. Retourne (conformes, écarts) ; mirror = miroir X ajouté pour comparer."""
    agree = disagree = 0
    for case in form['cases']:
        pts = [p for p in rotated_points(bundle['meshes'][case['mesh']], case['rot'], mirror) if abs(p[2]) < 0.02]
        if not pts: continue
        for c in case['conds']:
            if c[2] or (c[0] and c[1]): continue
            reaches = max(p[0] * c[0] + p[1] * c[1] for p in pts) > 0.45
            if reaches == bool(c[3]): agree += 1
            else: disagree += 1
    return agree, disagree


def roof_low_side(bundle, form):
    """Centre du sommet d'une forme de toit en rotation 0 (repli sans condition) ; le côté bas est à l'opposé. RoofSide :
    côté bas vers +x (contrat LowToward du générateur)."""
    g = form['rots'][0] if isinstance(form, dict) and 'rots' in form else form
    entry = form[0] if isinstance(form, list) else next((c for c in g['cases'] if not c['conds']), g['cases'][-1])
    pts = rotated_points(bundle['meshes'][entry['mesh']], entry['rot'])
    top = max(p[2] for p in pts)
    high = [p for p in pts if p[2] > top - 0.05]
    return sum(p[0] for p in high) / len(high), sum(p[1] for p in high) / len(high)


def main(path):
    bundle = json.load(open(path, encoding='utf-8'))
    ok = True
    print('set', bundle['set'], 'version', bundle['version'], 'calibration', bundle['calibration']['axis'], 'rotSign', bundle['calibration'].get('rotSign'))

    for name in ('Wall', 'WindowGrilles', 'Window'):
        form = bundle['forms'].get(name)
        if not isinstance(form, dict) or 'cases' not in form: continue   # forme « rots » (FarEast) : pas de table
        print('\n%s: %d cases, bitMeans %s - lateral table (bits E S W N = +x, +y, -x, -y; other neighbours 0):' % (name, len(form['cases']), form.get('bitMeans')))
        for bits, mesh, rot in lateral_table(form): print('  %s  %-18s rot %3d' % (bits, mesh, rot))
        print('  offsets referenced:', sorted({tuple(c[:3]) for case in form['cases'] for c in case['conds']}))
    wall = bundle['forms'].get('Wall')
    if wall and bundle['set'] not in NUMBERED_WALLS:
        rows = dict((b, m) for b, m, _ in lateral_table(wall))
        checks = {'1111': 'X_Wall', '1010': 'Wall', '0101': 'Wall'}
        for b in ('1100', '0110', '0011', '1001'): checks[b] = 'WallCorner'
        for b in ('1110', '0111', '1011', '1101'): checks[b] = 'T_Wall'
        for b, expected in checks.items():
            if rows[b] != expected: print('  ! %s: %s expected %s' % (b, rows[b], expected)); ok = False
    # Bras du mur (et de la vitre contextuelle du verre, voisins Building seulement : la fenêtre Lumber lit ses rideaux) vers
    # chaque voisin latéral exigé (test du miroir X : sans lui, les coins et T partent du mauvais côté).
    for name in ('Wall', 'Window'):
        form = bundle['forms'].get(name)
        if not isinstance(form, dict) or 'cases' not in form: continue
        # Voisins Building seulement : un mur droit collé à un bloc plein (règle solid, T4) n'a pas de bras vers lui.
        joins = lambda k: (k[4] if len(k) > 4 else form.get('bitMeans')) == 'category:Building'
        missed = [(c['mesh'], c['rot'], k[:2]) for c in form['cases'] for k in c['conds'] if k[3] and not k[2] and not (k[0] and k[1]) and joins(k)
                  and max(p[0] * k[0] + p[1] * k[1] for p in rotated_points(bundle['meshes'][c['mesh']], c['rot'])) < 0.45]
        print('\n%s arms toward required neighbours: %s' % (name, 'OK' if not missed else 'missing %s' % missed[:4]))
        if missed: ok = False
    window = bundle['forms'].get('WindowGrilles')
    if isinstance(window, dict) and 'cases' in window:
        rows = dict((b, r) for b, _, r in lateral_table(window))
        if len(window['cases']) > 1 and (rows['1010'] - rows['0101']) % 180 != 90: print('  ! WindowGrilles: same rotation for x and y walls'); ok = False

    for name, form in bundle['forms'].items():
        if isinstance(form, dict) and str(form.get('bitMeans', '')).startswith('type:'): ok = pipe_table(bundle, name, form) and ok

    # Dalles : le mesh s'ouvre vers les voisins attendus. Ne fait échouer que les sets extraits avec le miroir X (les
    # autres le montrent pour information).
    floor = bundle['forms'].get('Floor')
    if isinstance(floor, dict) and 'cases' in floor:
        mirrored = bool(bundle['calibration'].get('mirrorX'))
        agree, disagree = floor_sides(bundle, 'Floor', floor)
        if agree + disagree:
            alt = floor_sides(bundle, 'Floor', floor, mirror=True)
            print('\nFloor sides vs expected neighbours: %d agree, %d disagree (with an extra X mirror: %d / %d)%s' % (
                agree, disagree, alt[0], alt[1], '' if mirrored else ' - set not mirrored, informative'))
            if mirrored and disagree: print('  ! floor meshes open on the wrong side'); ok = False
    corner = bundle['forms'].get('RoofCorner')
    if corner and 'RoofSide' in bundle['forms']:   # Adobe : RoofCorner est un angle de parapet, pas un coin de toit en pente
        hx, hy = roof_low_side(bundle, corner)
        want = CORNER_R0.get(bundle['set'], (1, 1) if bundle['set'].startswith('Composite') else (-1, 1))
        print('\nRoofCorner r0 high corner at (%.2f, %.2f), low corner expected toward (%+d, %+d)' % (hx, hy, want[0], want[1]))
        if hx * want[0] > -0.2 or hy * want[1] > -0.2: print('  ! RoofCorner r0 low corner (BuildingGenerator.CornerRot)'); ok = False
    roof = bundle['forms'].get('RoofSide')
    if roof:
        hx, hy = roof_low_side(bundle, roof)
        print('\nRoofSide r0 high side at (%.2f, %.2f): low side toward %s' % (hx, hy, '+x' if hx < -0.2 else '?'))
        if hx >= -0.2: print('  ! RoofSide r0 low side must face +x (BuildingGenerator.LowToward)'); ok = False

    column = bundle['forms'].get('Column', {'cases': []})   # pas de colonne dans Garden Gravel
    print('\nColumn (%d cases):' % len(column['cases']))
    # Les noms de meshes varient par set (ColumnSingle, BrickColumnSolo, CL_ColumnSolo…) : on vérifie le suffixe.
    expected = {(): ('single', 'solo'), ((0, 0, 1),): ('bottom',), ((0, 0, -1),): ('top',), ((0, 0, -1), (0, 0, 1)): ('middle',)}   # voisin au-dessus seulement = bas de pile
    named = any(c['mesh'].lower().endswith(s) for c in column['cases'] for ss in expected.values() for s in ss)
    if not named: print('  (skipped: no column, or variants numbered as in FarEast)')
    for on, suffixes in (expected.items() if named else []):
        case = evaluate(column, set(on))
        got = case['mesh'] if case else None
        print('  above=%d below=%d -> %s' % ((0, 0, 1) in on, (0, 0, -1) in on, got))
        exact = COLUMN_MESHES.get(bundle['set'], {}).get(on)
        plain = got.lower().replace('large', '').rstrip('_') if got else ''   # colonnes larges T5 : Column_Bottom_Large, Column_TopLarge
        if not got or (got != exact if exact else not plain.endswith(suffixes)): print('  ! expected *%s' % '/'.join(suffixes)); ok = False

    print('\nmeshes:')
    for name, mesh in bundle['meshes'].items():
        pos = mesh['pos']
        lo = [min(pos[i::3]) for i in range(3)]; hi = [max(pos[i::3]) for i in range(3)]
        over = max(-min(lo), max(hi)) - 0.5
        tris = len(mesh['idx']) // 3
        flag = ''
        expected = EXPECTED_TRIS.get(bundle['set'], {})
        allowed = next((v for k, v in OVERHANG_BY_MESH.items() if name.startswith(k)), OVERHANG_BY_SET.get(bundle['set'], OVERHANG))
        if over > allowed: flag += '  ! outside cell by %.3f' % over; ok = False
        if name in expected and expected[name] != tris: flag += '  ! expected %d tris' % expected[name]; ok = False
        glass = ('  + %d glass' % (len(mesh['glass']) // 3) if mesh.get('glass') else '') + \
                ''.join('  + %d slot %d' % (len(s) // 3, k + 1) for k, s in enumerate(mesh.get('slots') or []) if s)
        print('  %-22s %4d verts %4d tris%s  [%s]..[%s]%s' % (name, len(pos) // 3, tris, glass, ' '.join('%.3f' % c for c in lo), ' '.join('%.3f' % c for c in hi), flag))

    print('\nexplicit forms:')
    for form, entries in bundle['forms'].items():
        if isinstance(entries, list): print('  %-10s %s' % (form, ' '.join('r%d=%s@%d' % (i, e['mesh'], e['rot']) for i, e in enumerate(entries))))
    # v4 : formes orientées et contextuelles (quais, rampes Brick) : 4 jeux de cas, chacun avec un repli sans condition ou
    # des cas qui couvrent toutes les combinaisons de leurs voisins (rampes : 4 cas sur 2 voisins).
    def covered(g):
        offs = sorted({tuple(c[:3]) for case in g['cases'] for c in case['conds']})
        return len(offs) <= 10 and all(evaluate(g, {o for i, o in enumerate(offs) if (k >> i) & 1}) for k in range(1 << len(offs)))
    rots = {form: f['rots'] for form, f in bundle['forms'].items() if isinstance(f, dict) and 'rots' in f}
    if rots:
        print('\nrotated contextual forms:')
        for form, groups in rots.items():
            fallback = [any(not c['conds'] for c in g['cases']) or covered(g) for g in groups]
            print('  %-26s cases %s fallback %s' % (form, [len(g['cases']) for g in groups], ''.join('1' if b else '0' for b in fallback)))
            if len(groups) != 4 or not all(fallback): print('  ! expected 4 rotations with a fallback case each'); ok = False
    print('\nskins:', bundle['skins'])
    print('\nRESULT', 'OK' if ok else 'FAILED')
    return 0 if ok else 1


if __name__ == '__main__':
    sys.exit(main(sys.argv[1]))
