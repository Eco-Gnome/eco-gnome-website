"""Contrôle d'un bundle d'objets (extract_objects.py) : chaque objet a des parts valides, son mesh tient dans l'emprise de
ses cases d'occupancy (± TOL) et repose au niveau du sol de sa case d'ancrage (z min ≈ −0.5), les textures existent.

    python Scripts/forms/check_objects.py ecocraft/wwwroot/assets/forms/ObjectsHewn.json [--verbose]

RESULT OK ou FAILED (code de retour 1). Les objets suspendus (lampes de plafond, panneaux accrochés) ne touchent pas le
sol : signalés en « warn », pas en échec.
"""
import argparse
import json
import math
import os
import sys

TOL = 0.6          # débord toléré au-delà des cases (manteau de cheminée, dossier…)
MAX_TRIS = 20000   # au-delà, l'objet pèse sur le budget de faces du rendu 3D
# Débord x/y admis (blocs) du vrai modèle au-delà de ses cases : flèche de la grue, rampe immergée du chantier naval,
# cerf-volant en l'air, socle du moulin à vent (case d'ancrage absente de l'occupancy du prefab).
OVERFLOW = {'CraneItem': 10.3, 'MediumShipyardItem': 1.05, 'WindmillItem': 1.05, 'KiteItem': 9.0}


def bbox(bundle, obj):
    lo, hi = [math.inf] * 3, [-math.inf] * 3
    for part in obj['parts']:
        pos = bundle['meshes'][part['mesh']]['pos']
        for i in range(3):
            lo[i] = min(lo[i], min(pos[i::3])); hi[i] = max(hi[i], max(pos[i::3]))
    return lo, hi


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument('bundle')
    ap.add_argument('--verbose', action='store_true')
    args = ap.parse_args()
    with open(args.bundle, encoding='utf-8') as f: bundle = json.load(f)
    folder = os.path.dirname(args.bundle)
    errors, warns = [], []

    if bundle.get('kind') != 'objects': errors.append('kind != objects')
    atlas = os.path.join(folder, bundle['set'] + '.webp')
    if bundle.get('materials') and not os.path.exists(atlas): errors.append('atlas missing: ' + atlas)
    for name, mesh in bundle['meshes'].items():
        n = len(mesh['pos']) // 3
        if len(mesh['nrm']) != 3 * n or len(mesh['uv']) != 2 * n or len(mesh['chan']) != n: errors.append('%s: arrays mismatch' % name)
        if len(mesh['idx']) % 3 or (mesh['idx'] and max(mesh['idx']) >= n): errors.append('%s: bad indices' % name)
        if 'shade' in mesh and (len(mesh['shade']) != n or not all(0 <= s <= 255 for s in mesh['shade'])): errors.append('%s: bad shade' % name)
        if mesh['uv'] and min(mesh['uv'][0::2]) < -0.5: errors.append('%s: u < -0.5 (read as an untextured vertex by the renderer)' % name)

    tris_total = 0
    for item, obj in sorted(bundle['objects'].items()):
        if not obj['parts']: errors.append('%s: no part' % item); continue
        if len(obj.get('rot') or []) != 4: errors.append('%s: rot must have 4 entries' % item)
        for p in obj['parts']:
            if p['mesh'] not in bundle['meshes']: errors.append('%s: unknown mesh %s' % (item, p['mesh']))
            if not 0 <= p['material'] < len(bundle['materials']): errors.append('%s: bad material index %d' % (item, p['material']))
            if 'glass' in p and (len(p['glass']) != 4 or not all(0 <= c <= 1 for c in p['glass'])): errors.append('%s: bad glass %s' % (item, p['glass']))
        if any(p['mesh'] not in bundle['meshes'] for p in obj['parts']): continue
        tris = sum(len(bundle['meshes'][p['mesh']]['idx']) // 3 for p in obj['parts'])
        tris_total += tris
        lo, hi = bbox(bundle, obj)
        cells = obj.get('cells') or [[0, 0, 0]]
        clo = [min(c[i] for c in cells) - 0.5 for i in range(3)]
        chi = [max(c[i] for c in cells) + 0.5 for i in range(3)]
        # Débord en x/y au-delà des cases : avertissement sous une case (l'occupancy du jeu est parfois plus petite que le
        # mesh) ou sous le débord admis de l'objet (OVERFLOW), erreur au-delà (mesh mal placé ou mal mis à l'échelle).
        # Hauteur et objets suspendus : avertissements.
        status = []
        for i, axis in enumerate('xy'):
            over = max(clo[i] - lo[i], hi[i] - chi[i])
            if over > TOL:
                msg = '%s outside cells by %.2f (%.2f..%.2f vs %.1f..%.1f)' % (axis, over, lo[i], hi[i], clo[i], chi[i])
                (errors if over >= max(1, OVERFLOW.get(item, 0)) else warns).append('%s: %s' % (item, msg)); status.append(msg)
        if hi[2] > chi[2] + TOL: warns.append('%s: taller than cells (%.2f > %.1f)' % (item, hi[2], chi[2]))
        if clo[2] <= -0.5 <= chi[2] and abs(lo[2] + 0.5) > 0.15: warns.append('%s: z min %.2f (expected -0.5)' % (item, lo[2]))
        if tris > MAX_TRIS: warns.append('%s: %d triangles' % (item, tris))
        if args.verbose or status:
            print('  %-40s %d part(s) %6d tris  bbox [%.2f %.2f %.2f]..[%.2f %.2f %.2f]  cells %d  %s' % (
                item, len(obj['parts']), tris, *lo, *hi, len(cells), 'OK' if not status else '; '.join(status)))

    print('%s: %d objects, %d meshes, %d triangles, %d textures (tile %s)' % (bundle['set'], len(bundle['objects']), len(bundle['meshes']), tris_total, len(bundle['materials']), bundle.get('tile')))
    for w in warns: print('warn: ' + w)
    for e in errors: print('error: ' + e)
    print('RESULT ' + ('OK' if not errors else 'FAILED'))
    sys.exit(0 if not errors else 1)


if __name__ == '__main__':
    main()
