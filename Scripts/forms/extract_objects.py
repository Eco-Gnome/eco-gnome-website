"""Extrait un « bundle d'objets » (meshes texturés des meubles, portes, tables d'artisanat…) depuis les prefabs d'Eco, au
format des bundles de formes (même dossier, même index.json), pour que le rendu 3D du planner dessine le vrai modèle sur
la case d'ancrage d'un objet et laisse vides les autres cases de son occupancy.

    python Scripts/forms/extract_objects.py --content "<checkout>/Eco/Content/Art" --manifest Scripts/forms/objects/ObjectsHewn.json --out ecocraft/wwwroot/assets/forms
    python Scripts/forms/extract_objects.py --content "<checkout>/Eco/Content/Art" --prefab HewnDoorObject --set Objects --out ecocraft/wwwroot/assets/forms

Manifeste : { "set": "ObjectsHewn", "prefabs": ["HewnDoorObject", "HewnBenchObject=HewnBenchItem", …] } (item = nom du
prefab sans « Object » + « Item » par défaut). Un set par famille : un atlas porte au plus 64 textures. Champ optionnel
"plain": { "<guid d'un .mat absent du checkout>": [r, g, b] } : tuile unie (0..1) à la place du matériau manquant.

Sources : `Player Built Objects/**/<Prefab>.prefab` (les dossiers Old/Backup écartés). Un prefab est lu récursivement :
MeshFilter → FBX + MeshRenderer → .mat → albedo, `PrefabInstance` (classe 1001) → prefab source (variantes Hardwood /
Softwood / par pierre : matériaux en override `m_Materials.Array.data[i]`) ou FBX instancié (tables : les modèles du FBX,
matériaux du `externalObjects` de son .meta), `WorldObjectOccupancyObject` (portes) ou champs `size` / `occupancyOffset`
du WorldObject → cases. Renderers LOD ≥ 1, colliders et GameObjects inactifs (eux ou un parent, ou retirés par une
instance) ignorés ; un mesh est découpé par slot de matériau (une part par slot, ordre des sous-meshes Unity). Sortie : `<Set>.json`, `<Set>.webp`, `<Set>.preview.png`, `index.json` (fusionné :
`sets.<Set>` et `objects.<Item> = <Set>`).

Repères : mêmes conventions que extract_forms.py (--axis, rotation planner en degrés autour de +z). Unity cuit dans le
mesh importé le « geometric transform » du nœud FBX (offset d'objet 3ds Max : GeometricTranslation/Rotation/Scaling,
Euler XYZ) et recale les sommets sur son RotationPivot ; le transform du nœud lui-même (Lcl*, PreRotation) va sur le
GameObject importé, que le prefab n'utilise pas, sauf FBX instancié (ses nœuds sont ces GameObjects) ou animé (clip du
prefab) ; un mesh skinné est posé par ses os. Le mesh est ensuite placé par la chaîne de Transform du prefab (position,
rotation, échelle), le transform propre de la racine ignoré (le jeu pose la racine au centre de la case d'ancrage). Une rotation r (0..3) d'un
objet du plan = Quaternion Unity de 90·r autour de Y (Geometry.Rotate) : le bundle donne par objet l'angle planner
correspondant (`rot[r]`).
"""
import argparse
import hashlib
import json
import math
import os
import re
import sys
import tempfile
import time

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import fbx                                                            # noqa: E402
import textures                                                       # noqa: E402
import unity_yaml                                                     # noqa: E402
from extract_forms import DEFAULT_AXIS, UNITS_PER_BLOCK, UV_MAPPED_SHADERS, apply, parse_axis, planner_angle   # noqa: E402
from PIL import Image, ImageStat                                      # noqa: E402

BUNDLE_VERSION = 1
CLASS_GAME_OBJECT, CLASS_TRANSFORM, CLASS_MESH_RENDERER, CLASS_MESH_FILTER, CLASS_ANIMATOR, CLASS_MONO, CLASS_SKINNED, CLASS_PREFAB_INSTANCE = 1, 4, 23, 33, 95, 114, 137, 1001
OCCUPANCY_SCRIPT = 'aa7b3363a5ff8f84ca4ee964488589b3'                # WorldObjectOccupancyObject.cs
# Niveaux de détail réduits, colliders, bâche des fondations (ville, pays, fédération : masquée par l'Animator une fois
# « Founded », état durable de l'objet).
SKIP_RENDERER = re.compile(r'lod[_ ]?[1-9]|collider|collision|foundation_cloth', re.I)
SKIP_PATH = re.compile(r'[\\/](Old|Backup|Deprecated)[\\/]|_(Old|Backup)\.prefab$', re.I)
ALBEDO_PROPS = ['_MainTex', '_BaseMap', '_BaseColorMap', '_Albedo']
# Shader « peinture » des meubles (Curved/TintableTextureArrayShader.shadergraph) : _MainTex est un Texture2DArray (image
# découpée en flipbook à l'import), tranche = UV1.x × 20 par sommet, échantillonnée aux UV0 ; couleur × (1 − alpha du sommet).
TINTABLE_SHADER = '954e7862fff54e34190c4c1b5c56f44b'
ARRAY_SLICE_SCALE = 20
IDENTITY = {'x': 0.0, 'y': 0.0, 'z': 0.0, 'w': 1.0}
HALF_TURN_Y = {'x': 0.0, 'y': 1.0, 'z': 0.0, 'w': 0.0}
ORIGIN = (0.0, 0.0, 0.0)
UNIT = (1.0, 1.0, 1.0)

log_lines = []


def log(msg=''):
    print(msg.encode(sys.stdout.encoding or 'utf-8', 'replace').decode(sys.stdout.encoding or 'utf-8'))
    log_lines.append(msg)


# --- géométrie -------------------------------------------------------------------------------------------------------

def euler_xyz(deg, v):
    """Rotation FBX Euler XYZ (X d'abord, puis Y, puis Z) d'un vecteur."""
    x, y, z = v
    for axis, a in enumerate(deg):
        c, s = math.cos(math.radians(a)), math.sin(math.radians(a))
        if axis == 0: y, z = y * c - z * s, y * s + z * c
        elif axis == 1: x, z = x * c + z * s, -x * s + z * c
        else: x, y = x * c - y * s, x * s + y * c
    return (x, y, z)


def quat_rotate(q, v):
    """Rotation d'un vecteur par un quaternion Unity {x, y, z, w}."""
    qx, qy, qz, qw = q['x'], q['y'], q['z'], q['w']
    x, y, z = v
    tx, ty, tz = 2 * (qy * z - qz * y), 2 * (qz * x - qx * z), 2 * (qx * y - qy * x)
    return (x + qw * tx + (qy * tz - qz * ty), y + qw * ty + (qz * tx - qx * tz), z + qw * tz + (qx * ty - qy * tx))


def quat_mul(a, b):
    return {'x': a['w'] * b['x'] + a['x'] * b['w'] + a['y'] * b['z'] - a['z'] * b['y'],
            'y': a['w'] * b['y'] - a['x'] * b['z'] + a['y'] * b['w'] + a['z'] * b['x'],
            'z': a['w'] * b['z'] + a['x'] * b['y'] - a['y'] * b['x'] + a['z'] * b['w'],
            'w': a['w'] * b['w'] - a['x'] * b['x'] - a['y'] * b['y'] - a['z'] * b['z']}


def compose(ppos, prot, pos, rot, pscl=None, scl=None):
    """Transform (pos, rot, échelle) exprimé dans le repère parent (ppos, prot, pscl) → (pos, rot, échelle) monde. L'échelle
    du parent passe dans le repère de l'enfant (permutée par sa rotation : exact pour les quarts de tour, les seuls
    rencontrés avec une échelle non uniforme)."""
    pscl, scl = pscl or UNIT, scl or UNIT
    rp = quat_rotate(prot, tuple(pscl[i] * pos[i] for i in range(3)))
    if max(pscl) - min(pscl) > 1e-6:
        pscl = tuple(abs(c) for c in quat_rotate({'x': -rot['x'], 'y': -rot['y'], 'z': -rot['z'], 'w': rot['w']}, pscl))
    return tuple(ppos[i] + rp[i] for i in range(3)), quat_mul(prot, rot), tuple(pscl[i] * scl[i] for i in range(3))


def affine_mul(A, B):
    """Produit de deux matrices affines 4×4 (listes de lignes)."""
    return [[sum(A[r][k] * B[k][c] for k in range(4)) for c in range(4)] for r in range(4)]


def affine_inv(M):
    """Inverse d'une matrice affine 4×4."""
    a = [row[:3] for row in M[:3]]
    det = a[0][0] * (a[1][1] * a[2][2] - a[1][2] * a[2][1]) - a[0][1] * (a[1][0] * a[2][2] - a[1][2] * a[2][0]) + a[0][2] * (a[1][0] * a[2][1] - a[1][1] * a[2][0])
    ia = [[(a[(c + 1) % 3][(r + 1) % 3] * a[(c + 2) % 3][(r + 2) % 3] - a[(c + 1) % 3][(r + 2) % 3] * a[(c + 2) % 3][(r + 1) % 3]) / det for c in range(3)] for r in range(3)]
    t = [-sum(ia[r][k] * M[k][3] for k in range(3)) for r in range(3)]
    return [ia[0] + [t[0]], ia[1] + [t[1]], ia[2] + [t[2]], [0.0, 0.0, 0.0, 1.0]]


def vec(d):
    d = d or {}
    return (float(d.get('x') or 0), float(d.get('y') or 0), float(d.get('z') or 0))


def scale(d):
    d = d or {}
    return tuple(float(1 if d.get(k) is None else d.get(k)) for k in 'xyz')


def quat(d):
    d = d or {}
    return {k: float((1 if k == 'w' else 0) if d.get(k) is None else d.get(k)) for k in 'xyzw'}    # w = 0 : demi-tour, pas l'identité


_node_cache = {}


def _node_info(fbx_path):
    """(transforms, racines, unité en blocs, modèles skinnés, demi-tour « bake ») d'un FBX, mémorisé."""
    if fbx_path not in _node_cache:
        try:
            meta = unity_yaml.parse_plain(fbx_path + '.meta') if os.path.exists(fbx_path + '.meta') else None
            bake = bool((((meta or {}).get('ModelImporter') or {}).get('meshes') or {}).get('bakeAxisConversion'))
            transforms, roots = fbx.node_transforms(fbx_path, bake=bake)
            nodes = fbx.parse(fbx_path)[1]
            unit = fbx._unit_scale(nodes) / UNITS_PER_BLOCK
            skinned = fbx.skinned_models(fbx_path)
            axes = {p['props'][0]: p['props'][4] for p in fbx.find(nodes, 'P') if p['props'][0] in ('FrontAxisSign', 'CoordAxisSign')}
            flipped = axes.get('FrontAxisSign', 1) == -1 and axes.get('CoordAxisSign', 1) == -1
        except Exception:
            transforms, roots, unit, skinned, bake, flipped = {}, [], 1.0, set(), False, False
        _node_cache[fbx_path] = (transforms, roots, unit, skinned, bake, HALF_TURN_Y if bake and not flipped else None)
    return _node_cache[fbx_path]


def bake_turn(fbx_path):
    """« Bake Axis Conversion » (.meta bakeAxisConversion: 1 : étagères, livres, pots…) : Unity cuit la conversion d'axes
    dans les sommets, un demi-tour autour de Y de plus qu'à l'import normal ; un fichier −1/−1 (FrontAxisSign et
    CoordAxisSign) le compense par celui de ses racines (BoxCollider et LODGroup calés par Unity : étagères en +X / −Z,
    poubelle primitive −1/−1 sans demi-tour). Quaternion à appliquer aux sommets, ou None."""
    return _node_info(fbx_path)[5]


def node_pose(fbx_path, model, skip_single_root, transforms=None):
    """Transform du nœud FBX `model` (chaîne Lcl Translation + pivot / PreRotation / Lcl Rotation jusqu'à la racine du
    fichier), en blocs et repère Unity (X négatif, quaternion (x, −y, −z, w)), et son échelle (Lcl Scaling cumulés) : celle
    du GameObject importé du nœud, qui ne sert qu'à un FBX instancié (ou animé) ; le mesh importé ne la contient pas.
    skip_single_root : un FBX à racine unique instancié dans un prefab voit la position et la rotation de sa racine
    écrasées par l'instance. Fichier « bake » : pose conjuguée par son demi-tour (sommets tournés par bake_turn).
    transforms : ceux d'overridden_transforms (overrides de nœuds internes d'une instance), sinon ceux du fichier."""
    cached, roots, unit, _, bake, turn = _node_info(fbx_path)
    npos, nrot, nscl = (transforms or cached).get(model, (ORIGIN, dict(IDENTITY), UNIT))
    if skip_single_root and len(roots) == 1 and model == roots[0]: return ORIGIN, dict(IDENTITY), nscl
    pos, rot = (-npos[0] * unit, npos[1] * unit, npos[2] * unit), {'x': nrot['x'], 'y': -nrot['y'], 'z': -nrot['z'], 'w': nrot['w']}
    if bake: pos, rot = quat_rotate(HALF_TURN_Y, pos), quat_mul(quat_mul(HALF_TURN_Y, rot), turn or IDENTITY)
    return pos, rot, nscl


def overridden_transforms(fbx_path, overrides, pose=None):
    """Transforms des nœuds du FBX (fbx.node_transforms) quand une instance remplace le transform local de certains
    nœuds : overrides {nœud: {'m_LocalPosition.x': v, …}} en repère Unity (composante absente : celle du FBX), ramenés au
    repère du fichier par l'inverse des conversions de node_pose (miroir X, unité, demi-tour « bake ») ; pose : clip_pose."""
    _, _, unit, _, bake, turn = _node_info(fbx_path)

    def inv(q): return {'x': -q['x'], 'y': -q['y'], 'z': -q['z'], 'w': q['w']}

    def override(name, trs):
        o = overrides.get(name)
        if not o: return trs
        t, q, s = trs
        pos, rot = (-t[0] * unit, t[1] * unit, t[2] * unit), {'x': q['x'], 'y': -q['y'], 'z': -q['z'], 'w': q['w']}
        if bake: pos, rot = quat_rotate(HALF_TURN_Y, pos), quat_mul(quat_mul(HALF_TURN_Y, rot), turn or IDENTITY)
        pos = tuple(o.get('m_LocalPosition.' + a, pos[i]) for i, a in enumerate('xyz'))
        rot = {k: o.get('m_LocalRotation.' + k, rot[k]) for k in 'xyzw'}
        s = tuple(o.get('m_LocalScale.' + a, s[i]) for i, a in enumerate('xyz'))
        if bake: pos, rot = quat_rotate(HALF_TURN_Y, pos), quat_mul(quat_mul(inv(HALF_TURN_Y), rot), inv(turn or IDENTITY))
        return (-pos[0] / unit, pos[1] / unit, pos[2] / unit), {'x': rot['x'], 'y': -rot['y'], 'z': -rot['z'], 'w': rot['w']}, s
    return fbx.node_transforms(fbx_path, override, pose, bake)[0]


def clip_pose(fbx_path, clip_id):
    """Pose de la première image du clip `clip_id` (m_Motion d'un AnimatorController) d'un FBX : take (takeName) de
    l'entrée clipAnimations du .meta dont l'internalID est clip_id, lue par fbx.take_pose ; None sans clip."""
    if clip_id is None or not os.path.exists(fbx_path + '.meta'): return None
    importer = (unity_yaml.parse_plain(fbx_path + '.meta') or {}).get('ModelImporter') or {}
    clip = next((c for c in (importer.get('animations') or {}).get('clipAnimations') or [] if int((c or {}).get('internalID') or 0) == clip_id), None)
    return fbx.take_pose(fbx_path, clip['takeName']) or None if clip and clip.get('takeName') else None


_XXH = (11400714785074694791, 14029467366897019727, 1609587929392839161, 9650029242287828579, 2870177450012600261)
_M64 = (1 << 64) - 1


def xxh64(data):
    """xxHash64 (graine 0) d'un bytes."""
    def rotl(x, r): return ((x << r) | (x >> (64 - r))) & _M64
    def rnd(acc, v): return (rotl((acc + v * _XXH[1]) & _M64, 31) * _XXH[0]) & _M64
    n, i = len(data), 0
    if n >= 32:
        v = [(_XXH[0] + _XXH[1]) & _M64, _XXH[1], 0, (-_XXH[0]) & _M64]
        while i + 32 <= n:
            v = [rnd(v[j], int.from_bytes(data[i + 8 * j:i + 8 * j + 8], 'little')) for j in range(4)]; i += 32
        h = (rotl(v[0], 1) + rotl(v[1], 7) + rotl(v[2], 12) + rotl(v[3], 18)) & _M64
        for x in v: h = ((h ^ rnd(0, x)) * _XXH[0] + _XXH[3]) & _M64
    else: h = _XXH[4]
    h = (h + n) & _M64
    while i + 8 <= n: h = (rotl(h ^ rnd(0, int.from_bytes(data[i:i + 8], 'little')), 27) * _XXH[0] + _XXH[3]) & _M64; i += 8
    if i + 4 <= n: h = (rotl(h ^ (int.from_bytes(data[i:i + 4], 'little') * _XXH[0]) & _M64, 23) * _XXH[1] + _XXH[2]) & _M64; i += 4
    while i < n: h = (rotl(h ^ (data[i] * _XXH[4]) & _M64, 11) * _XXH[0]) & _M64; i += 1
    h ^= h >> 33; h = (h * _XXH[1]) & _M64; h ^= h >> 29; h = (h * _XXH[2]) & _M64; h ^= h >> 32
    return h - (1 << 64) if h >> 63 else h


def fbx_game_object_id(node_path):
    """fileID du GameObject importé d'un nœud FBX (fileIdsGeneration 2) : xxHash64 de « Type:GameObject->//RootNode/root/
    <chemin>0 », chemin sans la racine d'un fichier à racine unique, fusionnée avec la racine du modèle (vérifié : racine
    « //RootNode/root0 » 919132149155446097, les 4 GameObjects retirés du tri des déchets, le collider de l'établi de
    mécanicien, les couvercles désactivés des poubelles modernes). Transform : fbx_transform_id."""
    return xxh64(('Type:GameObject->//RootNode/root/%s0' % node_path).encode('utf-8'))


def fbx_transform_id(node_path):
    """fileID du Transform importé d'un nœud FBX (fileIdsGeneration 2) : xxHash64 de « Type:Transform->//RootNode/root/
    <chemin>/Transform0 », chemin de fbx_game_object_id, vide pour la racine (« //RootNode/root/Transform0 »,
    −8679921383154817045). Vérifié : socle et cube du cube tournant, racines du moulin à vent, pot de la petite marmite ;
    les composants suivent la même forme (MeshRenderer du socle : « …/CubeGlobe/MeshRenderer0 »)."""
    return xxh64(('Type:Transform->//RootNode/root/%sTransform0' % (node_path + '/' if node_path else '')).encode('utf-8'))


def fbx_renderer_ids(node_path):
    """fileIDs possibles du MeshRenderer / SkinnedMeshRenderer importé d'un nœud FBX : forme par chemin (fbx_transform_id,
    « …/<chemin>/MeshRenderer0 ») ou par nom seul du nœud (vérifié sur la barge en bois : « Type:SkinnedMeshRenderer->
    WoodenBargeMain0 », ses rampes, manivelles et bascule)."""
    name = node_path.split('/')[-1]
    return {xxh64(s.encode('utf-8')) for k in ('MeshRenderer', 'SkinnedMeshRenderer')
            for s in ('Type:%s->//RootNode/root/%s%s0' % (k, node_path + '/' if node_path else '', k), 'Type:%s->%s0' % (k, name))}


def mesh_file_id(name):
    """fileID du Mesh importé nommé `name` (fileIdsGeneration 2) : xxHash64 de « Type:Mesh-><nom>0 » (vérifié sur les
    étiquettes du tri des déchets, WasteLabel_2, _3, _5)."""
    return xxh64(('Type:Mesh->%s0' % name).encode('utf-8'))


def is_skinned(fbx_path, model):
    return model in _node_info(fbx_path)[3]


def fit_score(lo, hi, cells):
    """Écart d'une bbox planner à la boîte des cases (débord en x, y, dessous du sol) : 0 = parfaitement contenue."""
    if not cells: return 0.0
    clo = [min(c[i] for c in cells) - 0.5 for i in range(3)]
    chi = [max(c[i] for c in cells) + 0.5 for i in range(3)]
    return sum(max(0.0, clo[i] - lo[i]) + max(0.0, hi[i] - chi[i]) for i in range(2)) + max(0.0, clo[2] - lo[2]) + abs(lo[2] - clo[2])


# --- assets ----------------------------------------------------------------------------------------------------------

class Assets:
    """guid → chemin, index des « Player Built Objects » étendu à tout Art à la première référence extérieure."""

    def __init__(self, art_root):
        self.art_root = art_root
        self.objects_root = os.path.join(art_root, 'Player Built Objects')
        self.index = unity_yaml.build_guid_index(self.objects_root)
        self.full = False
        self.prefabs = {}
        for dirpath, _, filenames in os.walk(self.objects_root):
            for name in filenames:
                if name.endswith('.prefab'): self.prefabs.setdefault(name[:-7], []).append(os.path.join(dirpath, name))

    def get(self, guid):
        if not guid: return None
        if guid not in self.index and not self.full:
            # Index de tout Art (40 000 .meta, lent) : mis en cache un jour dans le dossier temporaire.
            cache = os.path.join(tempfile.gettempdir(), 'eco-forms-guid-index-%s.json' % hashlib.md5(os.path.normcase(self.art_root).encode('utf-8')).hexdigest()[:8])
            if os.path.exists(cache) and time.time() - os.path.getmtime(cache) < 86400:
                with open(cache, encoding='utf-8') as f: full = json.load(f)
            else:
                log('  guid %s hors « Player Built Objects » : index de tout Art…' % guid)
                full = unity_yaml.build_guid_index(self.art_root)
                with open(cache, 'w', encoding='utf-8') as f: json.dump(full, f)
            self.index = {**full, **self.index}
            self.full = True
        return self.index.get(guid)

    def prefab(self, name):
        """Chemin du prefab `name` : hors dossiers Old/Backup, sous Prefabs/ de préférence ; (chemin, nombre de candidats)."""
        found = [p for p in self.prefabs.get(name, []) if not SKIP_PATH.search(p)]
        if len(found) > 1:
            preferred = [p for p in found if os.sep + 'Prefabs' + os.sep in p]
            if len(preferred) == 1: found = preferred
        return (found[0] if len(found) == 1 else None), len(found)

    def fbx_mesh_names(self, fbx_path):
        """fileID d'un Mesh importé → nom du mesh, d'après l'internalIDToNameTable du .meta (classe 43 ; FBX à
        fileIdsGeneration 1, les fichiers récents hachent l'id et laissent la table vide : voir mesh_file_id)."""
        meta = fbx_path + '.meta'
        if not os.path.exists(meta): return {}
        importer = (unity_yaml.parse_plain(meta) or {}).get('ModelImporter') or {}
        out = {}
        for entry in importer.get('internalIDToNameTable') or []:
            first = (entry or {}).get('first') or {}
            if '43' in first: out[first['43']] = entry.get('second')
        return out

    def fbx_materials(self, fbx_path):
        """Nom de matériau FBX → guid du .mat, d'après le externalObjects du .meta (FBX instancié dans un prefab)."""
        meta = fbx_path + '.meta'
        if not os.path.exists(meta): return {}
        importer = (unity_yaml.parse_plain(meta) or {}).get('ModelImporter') or {}
        out = {}
        for entry in importer.get('externalObjects') or []:
            first, second = (entry or {}).get('first') or {}, (entry or {}).get('second') or {}
            if first.get('type') == 'UnityEngine:Material' and second.get('guid'): out[first.get('name')] = second['guid']
        return out


# --- prefab ----------------------------------------------------------------------------------------------------------

def load_prefab(path, assets, notes, depth=0):
    """Scène d'un prefab dans le repère de sa racine (transform propre de la racine ignoré) :
    renderers [{name, fbx, model, materials: [guid par slot], pos, rot, id (MeshRenderer), go}], cells [(pos Unity, blockType)],
    occupancy {size, offset, override} | None. Les PrefabInstance sont résolues récursivement."""
    if depth > 4: return {'name': None, 'renderers': [], 'cells': [], 'occupancy': None, 'ids': set()}
    docs = unity_yaml.parse_documents(path)
    by_id = {fid: (cid, next(iter(val.values()))) for cid, fid, val in docs if isinstance(val, dict) and val}
    transforms = {fid: val for fid, (cid, val) in by_id.items() if cid == CLASS_TRANSFORM}
    gos = {fid: val for fid, (cid, val) in by_id.items() if cid == CLASS_GAME_OBJECT}
    instances = {fid: val for fid, (cid, val) in by_id.items() if cid == CLASS_PREFAB_INSTANCE}
    of_go = {(val.get('m_GameObject') or {}).get('fileID'): fid for fid, val in transforms.items()}
    stripped = {fid: (val.get('m_PrefabInstance') or {}).get('fileID') for fid, val in transforms.items() if (val.get('m_PrefabInstance') or {}).get('fileID')}
    # Transform racine de chaque instance (cible de ses overrides de position / rotation) : le Transform « stripped » de
    # l'instance rangé dans les m_Children d'un Transform du prefab. Les overrides visant un autre Transform de la source
    # (os d'un mesh skinné : machinist_table.fbx, os « root » tourné de 90° en Y) ne déplacent pas l'instance.
    children = {(c or {}).get('fileID') for val in transforms.values() for c in (val.get('m_Children') or [])}
    root_target = {stripped[fid]: (transforms[fid].get('m_CorrespondingSourceObject') or {}).get('fileID') for fid in stripped if fid in children}

    # Instances imbriquées : transform local (overrides m_LocalPosition/Rotation), parent, overrides de matériaux, activité.
    subs = {}
    for iid, inst in instances.items():
        mod = inst.get('m_Modification') or {}
        pos, rot, scl, materials, occ, active, root_go, mesh, inner = [0.0, 0.0, 0.0], dict(IDENTITY), [1.0, 1.0, 1.0], {}, {}, {}, None, None, {}
        removed = {(r or {}).get('fileID') for r in mod.get('m_RemovedGameObjects') or []}
        # Racine d'une variante (instance sans parent, son Transform n'est dans aucun m_Children) : la cible dont Unity
        # enregistre la rotation complète (petite marmite : la racine en identité, le pot descendu par un 2ᵉ override).
        root = root_target.get(iid) or next((((md.get('target') or {}).get('fileID')) for md in mod.get('m_Modifications') or []
                                             if str(md.get('propertyPath') or '').strip("'\"") == 'm_LocalRotation.w'), None)
        for md in mod.get('m_Modifications') or []:
            pp = str(md.get('propertyPath') or '').strip("'\"")
            target = (md.get('target') or {}).get('fileID')
            val, ref = md.get('value'), (md.get('objectReference') or {}).get('guid')
            if pp.startswith(('m_LocalPosition.', 'm_LocalRotation.', 'm_LocalScale.')) and root not in (None, target):
                inner.setdefault(target, {})[pp] = float(val or 0); continue
            if pp.startswith('m_LocalPosition.'): pos['xyz'.index(pp[-1])] = float(val or 0)
            elif pp.startswith('m_LocalRotation.'): rot[pp[-1]] = float(val or 0)
            elif pp.startswith('m_LocalScale.'): scl['xyz'.index(pp[-1])] = float(val or 0)
            elif pp.startswith('m_Materials.Array.data[') and ref: materials[(target, int(pp[pp.index('[') + 1:pp.index(']')]))] = ref
            elif pp.startswith('size.'): occ.setdefault('size', {})[pp[-1]] = val
            elif pp.startswith('occupancyOffset.'): occ.setdefault('offset', {})[pp[-1]] = val
            elif pp == 'm_IsActive': active[target] = val
            elif pp == 'm_Name': root_go = target                      # Unity nomme l'instance sur son GameObject racine
            elif pp == 'm_Mesh' and ref: mesh = ref
        subs[iid] = {'pos': tuple(pos), 'rot': rot, 'scl': tuple(scl), 'parent': (mod.get('m_TransformParent') or {}).get('fileID') or 0,
                     'source': assets.get((inst.get('m_SourcePrefab') or {}).get('guid')), 'materials': materials, 'occ': occ, 'active': active, 'root_go': root_go, 'removed': removed,
                     'mesh': assets.get(mesh) if mesh else None, 'inner': inner, 'guid': (inst.get('m_SourcePrefab') or {}).get('guid')}

    def world(tid):
        """Transform monde (repère de la racine de ce prefab) d'un Transform ou d'un Transform « stripped » d'instance."""
        if tid in stripped:
            sub = subs.get(stripped[tid])
            if not sub or not sub['parent']: return ORIGIN, dict(IDENTITY), UNIT   # racine (variante) : transform propre ignoré
            ppos, prot, pscl = world(sub['parent'])
            return compose(ppos, prot, sub['pos'], sub['rot'], pscl, sub['scl'])
        t = transforms.get(tid)
        if t is None: return ORIGIN, dict(IDENTITY), UNIT
        father = (t.get('m_Father') or {}).get('fileID') or 0
        if not father: return ORIGIN, dict(IDENTITY), UNIT                  # racine : le jeu la pose au centre de la case
        ppos, prot, pscl = world(father)
        return compose(ppos, prot, vec(t.get('m_LocalPosition')), quat(t.get('m_LocalRotation')), pscl, scale(t.get('m_LocalScale')))

    def active(go):
        """GameObject actif et tous ses parents actifs (un parent inactif masque ses enfants : porte Fir en double)."""
        while go:
            if (gos.get(go) or {}).get('m_IsActive', 1) == 0: return False
            father = ((transforms.get(of_go.get(go)) or {}).get('m_Father') or {}).get('fileID')
            go = ((transforms.get(father) or {}).get('m_GameObject') or {}).get('fileID')
        return True

    # FBX dont un Animator du prefab joue les clips (porte de garage : l'état « Closed » remet le panneau à sa pose du FBX).
    animated, motions = set(), {}
    for cid, val in by_id.values():
        controller = assets.get((val.get('m_Controller') or {}).get('guid')) if cid == CLASS_ANIMATOR else None
        if controller and os.path.exists(controller):
            with open(controller, encoding='utf-8', errors='replace') as f:
                clips = set(re.findall(r'm_Motion: \{fileID: (-?\d+), guid: (\w+)', f.read()))
            animated.update(guid for _, guid in clips)
            # Un seul clip (boucle permanente : cerf-volant) : sa première image est la pose de l'objet en jeu.
            if len(clips) == 1:
                (clip, guid), = clips; motions.setdefault(guid, int(clip))

    renderers, cells, occupancy, name = [], [], None, None
    for fid, (cid, val) in by_id.items():
        go = (val.get('m_GameObject') or {}).get('fileID')
        if cid == CLASS_GAME_OBJECT and not (val.get('m_Father')) and fid in of_go and not (transforms[of_go[fid]].get('m_Father') or {}).get('fileID'):
            name = name or val.get('m_Name')
        if cid in (CLASS_MESH_FILTER, CLASS_SKINNED) and (val.get('m_Mesh') or {}).get('guid'):
            gob = gos.get(go) or {}
            if not active(go): continue
            rend = (fid, val) if cid == CLASS_SKINNED else next(((rid, v) for rid, (c, v) in by_id.items() if c == CLASS_MESH_RENDERER and (v.get('m_GameObject') or {}).get('fileID') == go), (None, {}))
            mats = [(m or {}).get('guid') for m in (rend[1].get('m_Materials') or [])]
            pos, rot, scl = world(of_go.get(go))
            own = transforms.get(of_go.get(go)) or {}
            # Mesh importé = géométrie dans le repère de son nœud FBX, placée par son seul GameObject ; exception : un
            # GameObject au transform local nul d'un FBX animé par un clip du prefab (porte de garage) ou d'un mesh skinné
            # sans os connus reçoit la pose du nœud, que le clip ou les os lui donnent en jeu. Une échelle non unitaire
            # compte (panneau de magasin : mesh sous son os, échelle 6,58 qui compense l'armature, pose déjà portée par les os).
            placed = (max(abs(c) for c in vec(own.get('m_LocalPosition'))) > 1e-4 or 1 - abs(quat(own.get('m_LocalRotation'))['w']) > 1e-6
                      or max(abs(c - 1) for c in scale(own.get('m_LocalScale'))) > 1e-4)
            renderers.append({'name': gob.get('m_Name'), 'fbx': assets.get(val['m_Mesh']['guid']), 'model': gob.get('m_Name'),
                              'materials': mats, 'pos': pos, 'rot': rot, 'scl': scl, 'id': rend[0], 'go': go, 'mesh_id': val['m_Mesh'].get('fileID'),
                              'posed': not placed and (cid == CLASS_SKINNED or val['m_Mesh']['guid'] in animated)})
            if cid == CLASS_SKINNED:
                # Mesh skinné : Unity le dessine par ses os (os × pose de liaison), pas par son GameObject : transform monde de
                # chaque os du prefab, par nom (celui du nœud FBX).
                bones = {}
                for b in val.get('m_Bones') or []:
                    t = transforms.get((b or {}).get('fileID'))
                    if t is not None: bones[(gos.get((t.get('m_GameObject') or {}).get('fileID')) or {}).get('m_Name')] = world(b['fileID'])
                if bones: renderers[-1]['bones'] = bones
        elif cid == CLASS_MONO and (val.get('m_Script') or {}).get('guid') == OCCUPANCY_SCRIPT:
            pos = world(of_go.get(go))[0]
            cells.append((tuple(int(round(c)) for c in pos), val.get('blockType')))
        elif cid == CLASS_MONO and 'hasOccupancy' in val:
            occupancy = {'size': val.get('size') or {}, 'offset': val.get('occupancyOffset') or {}, 'override': bool(val.get('overrideOccupancy'))}

    for iid, sub in subs.items():
        src = sub['source']
        ipos, irot, iscl = (ORIGIN, dict(IDENTITY), UNIT) if not sub['parent'] else world(sub['parent'])
        if sub['parent']: ipos, irot, iscl = compose(ipos, irot, sub['pos'], sub['rot'], iscl, sub['scl'])
        if not src:
            notes.append('%s: nested prefab source not found (guid), skipped' % os.path.basename(path)); continue
        if sub['root_go'] is not None and sub['active'].get(sub['root_go']) == 0:     # instance désactivée (déchets du tri)
            notes.append('%s: inactive instance of %s skipped' % (os.path.basename(path), os.path.basename(src))); continue
        if src.lower().endswith('.prefab'):
            scene = load_prefab(src, assets, notes, depth + 1)
            name = name or scene['name']
            # Override visant un renderer d'un FBX instancié dans la source : son fileID est un XOR de l'instance et d'un id
            # interne haché du FBX (fileIdsGeneration 2), non recalculable. Cible absente de la source et source à un seul
            # FBX instancié (tables Ashlar par pierre) : appliqué par slot à tous les modèles de ce FBX.
            known = scene['ids'] | {r['id'] for r in scene['renderers']}
            loose = {slot: guid for (target, slot), guid in sub['materials'].items() if target not in known}
            fbx_insts = {r['inst'] for r in scene['renderers'] if r.get('inst') is not None}
            if loose and len(fbx_insts) > 1:
                notes.append('%s: material overrides on nested FBX objects of %s ignored (several FBX instances)' % (os.path.basename(path), os.path.basename(src))); loose = {}
            for r in scene['renderers']:
                if sub['active'].get(r['go']) == 0: continue
                mats = list(r['materials'])
                for (target, slot), guid in sub['materials'].items():
                    if target == r['id']:
                        while len(mats) <= slot: mats.append(None)
                        mats[slot] = guid
                if r.get('inst') is not None:
                    for slot, guid in loose.items():
                        while len(mats) <= slot: mats.append(None)
                        mats[slot] = guid
                pos, rot, scl = compose(ipos, irot, r['pos'], r['rot'], iscl, r['scl'])
                renderers.append({**r, 'materials': mats, 'pos': pos, 'rot': rot, 'scl': scl})
                if 'bones' in r: renderers[-1]['bones'] = {k: compose(ipos, irot, *b[:2], iscl, b[2]) for k, b in r['bones'].items()}
            for cpos, kind in scene['cells']:
                pos = compose(ipos, irot, cpos, dict(IDENTITY), iscl)[0]
                cells.append((tuple(int(round(c)) for c in pos), kind))
            if scene['occupancy'] and occupancy is None:
                occupancy = {'size': {**scene['occupancy']['size'], **sub['occ'].get('size', {})},
                             'offset': {**scene['occupancy']['offset'], **sub['occ'].get('offset', {})}, 'override': scene['occupancy']['override']}
        elif src.lower().endswith('.fbx'):
            # FBX instancié : un renderer par modèle maillé (LOD ≥ 1 filtrés plus tard) ; matériaux du .meta, puis les
            # overrides par slot : ceux dont la cible est le renderer d'un nœud (fbx_renderer_ids) vont à ce nœud seul, les
            # autres (cible inconnue) à tous les modèles.
            # Les nœuds gardent leur transform FBX (Blender : Lcl Rotation 90/180 sur chaque objet), convertie main gauche
            # (X négatif, quaternion (x, −y, −z, w)) ; un FBX à racine unique la fusionne avec la racine de l'instance.
            ext = assets.fbx_materials(src)
            try: models = fbx.models(src)
            except Exception as e:                                    # FBX ASCII (outil animé du chevalet) : instance ignorée
                notes.append('%s: nested FBX %s unreadable (%s), skipped' % (os.path.basename(path), os.path.basename(src), e)); continue
            roots, paths = _node_info(src)[1], fbx.node_paths(src)
            chains = {n: (p.split('/')[1:] if len(roots) == 1 and p.split('/')[0] == roots[0] else p.split('/')) for n, p in paths.items()}
            rids = {rid: n for n, c in chains.items() for rid in fbx_renderer_ids('/'.join(c))}
            targeted, overrides = {}, {}
            for (target, slot), guid in sub['materials'].items():
                if target in rids: targeted.setdefault(rids[target], {})[slot] = guid
                else: overrides[slot] = guid
            meshed = [m for m, _ in models if not SKIP_RENDERER.search(m)]
            if len(meshed) > 1 and overrides:
                notes.append('%s: %s has several models, material overrides applied to all of them' % (os.path.basename(path), os.path.basename(src)))
            # Override de m_Mesh (animaux empaillés : FBX de l'élan instancié, mesh d'un autre FBX) : le GameObject garde la
            # pose du FBX source, le mesh vient du FBX de l'override (un seul modèle de part et d'autre).
            mesh_src = sub['mesh'] if sub['mesh'] and sub['mesh'].lower().endswith('.fbx') and len(models) == 1 else None
            if sub['mesh'] and not mesh_src:
                notes.append('%s: m_Mesh override on %s ignored' % (os.path.basename(path), os.path.basename(src)))
            # Transform d'un nœud interne du FBX en override (position, rotation, échelle), retrouvé par son fileID haché
            # (fbx_transform_id) : remplace le transform local du nœud, ses enfants suivent (petite marmite : pot descendu au
            # sol ; cube tournant : socle et cube ; moulin à vent : échelle 100 des racines ramenée à 1).
            tids = {fbx_transform_id('/'.join(c)): n for n, c in chains.items()}
            over = {tids[t]: v for t, v in sub['inner'].items() if t in tids}
            if len(over) < len(sub['inner']):
                notes.append('%s: transform overrides on %d nested object(s) of %s ignored' % (os.path.basename(path), len(sub['inner']) - len(over), os.path.basename(src)))
            # FBX dont un Animator du prefab joue un clip (cerf-volant : KiteLoop) : ses meshes skinnés sont posés par leurs os
            # à la première image du clip, comme en jeu (mât debout, cerf-volant en l'air ; au repos, mât couché).
            pose = clip_pose(src, motions.get(sub['guid']))
            posed = overridden_transforms(src, over, pose) if over or pose else None
            # GameObjects du FBX retirés (m_RemovedGameObjects) ou désactivés par l'instance, retrouvés par leur fileID haché ;
            # un nœud part avec son parent (tri des déchets : la racine du prefab est le FBX du broyeur, tous ses meshes retirés).
            gone = sub['removed'] | {t for t, v in sub['active'].items() if v == 0}
            # Géométrie instanciée par plusieurs nœuds (pales du moulin à vent) : un renderer par nœud, même mesh.
            shared = fbx.shared_models(src)
            for model, mat_names in models:
                for node in [model] + shared.get(model, []):
                    chain = chains.get(node, [node])
                    if gone and any(fbx_game_object_id('/'.join(chain[:k + 1])) in gone for k in range(len(chain))):
                        notes.append('%s: %s/%s removed or inactive in the instance, skipped' % (os.path.basename(path), os.path.basename(src), node)); continue
                    own = {**overrides, **targeted.get(node, {})}
                    mats = [own.get(i, ext.get(n)) for i, n in enumerate(mat_names)] or [own.get(0)]
                    npos, nrot, nscl = node_pose(src, node, skip_single_root=True, transforms=posed)
                    pos, rot, scl = compose(ipos, irot, npos, nrot, iscl, nscl)
                    mesh_fbx, mesh_model = (mesh_src, fbx.models(mesh_src)[0][0]) if mesh_src else (src, model)
                    renderers.append({'name': node, 'fbx': mesh_fbx, 'model': mesh_model, 'materials': mats, 'pos': pos, 'rot': rot, 'scl': scl, 'id': None, 'go': None, 'node': True, 'inst': iid,
                                      'alt': {'name': node, 'fbx': mesh_fbx, 'model': mesh_model, 'materials': mats, 'pos': ipos, 'rot': irot, 'scl': iscl, 'id': None, 'go': None}})
                    sk = fbx.skin(src, node) if pose and not mesh_src and is_skinned(src, node) else None
                    if sk:
                        bones = {b: node_pose(src, b, True, posed) for b, _, _ in sk['clusters']}
                        renderers[-1]['bones'] = {b: compose(ipos, irot, bpos, brot, iscl, bscl) for b, (bpos, brot, bscl) in bones.items()}
        else:
            notes.append('%s: nested source %s not supported' % (os.path.basename(path), os.path.basename(src)))
    return {'name': name, 'renderers': renderers, 'cells': cells, 'occupancy': occupancy, 'ids': set(by_id)}


def occupancy_cells(prefab):
    """Cases Unity de l'objet : la boîte size/occupancyOffset du WorldObject plus les WorldObjectOccupancyObject (prises de
    tuyaux, cases de porte : ils changent le type d'une case de la boîte, comme dans l'export du jeu)."""
    markers = [c for c, _ in prefab['cells']]
    occ = prefab['occupancy']
    if not occ: return markers
    size = [int(occ['size'].get(k) or 1) for k in 'xyz']
    off = [int(occ['offset'].get(k) or 0) for k in 'xyz']
    # Taille négative (banderoles : size.x −6, offset 1) : cases offset + size .. offset − 1, comme WorldObjectOccupancyAutoGen.
    span = [range(min(o, o + n), max(o, o + n)) for o, n in zip(off, size)]
    box = [(x, y, z) for x in span[0] for y in span[1] for z in span[2]]
    return box + [c for c in markers if c not in box]


# --- mesh ------------------------------------------------------------------------------------------------------------

def pick_model(wanted, candidates):
    """Modèle FBX d'un GameObject : même nom, sinon même nom sans suffixe « .NNN » (copies d'un même mesh : vis, pieds),
    sinon sans suffixe de copie Unity « _N » / « (N) » (chaises du tribunal, ports de tuyau), sinon le premier."""
    if wanted in candidates: return wanted
    base = re.sub(r'\.\d+$', '', wanted or '')
    if base:
        same = [c for c in candidates if re.sub(r'\.\d+$', '', c) == base]
        if same: return same[0]
        copy = re.sub(r'(_\d+| \([^)]*\))$', '', base)
        if copy in candidates: return copy
    return candidates[0]


def convert_mesh(path, renderer, m, is_array=lambda s: False):
    """FBX → meshes planner par (slot de matériau, tranche) {(slot, tranche|None): {pos, nrm, idx, uv, chan[, shade]}}
    (sommets dédoublonnés pos + normale + uv, canal détail partout), bbox globale, mesh brut. Un slot où is_array(slot)
    (texture en tableau) est découpé par tranche (UV1.x × 20 du premier coin du polygone) et porte l'ombrage du sommet
    (shade = 255 × (1 − alpha)). u décalé d'un entier par mesh pour rester ≥ 0 (même rendu en répétition)."""
    raw = fbx.load_mesh(path, renderer['model'])
    gt, gr, gs = fbx.geometric_transform(path, raw['name'])
    pv, rot, rscl, turn = raw['pivot'], renderer['rot'], renderer.get('scl') or UNIT, bake_turn(path) or IDENTITY
    # Échelle : unité du fichier (si useFileScale), globalScale de l'importeur (.meta), puis celle des Transform (renderer['scl']).
    # Le Lcl Scaling du nœud est l'échelle du GameObject importé, pas celle du mesh : il n'est dans renderer['scl'] que pour
    # un nœud de FBX instancié (node_pose), pas pour un MeshFilter du prefab (pieux de revendication, banderoles 0,01, pompe
    # à balancier 2,54 : BoxCollider du même GameObject, calé sur le mesh).
    importer = (unity_yaml.parse_plain(path + '.meta') or {}).get('ModelImporter') if os.path.exists(path + '.meta') else None
    meshes_opts = (importer or {}).get('meshes') or {}
    scale = (raw['unit_scale'] / UNITS_PER_BLOCK if meshes_opts.get('useFileScale', 1) else 1.0) * float(meshes_opts.get('globalScale') or 1)

    # Unity importe un FBX (main droite) en négativant X (main gauche) : l'occupancy des prefabs (table : cases X −1..0)
    # est calée sur le mesh importé, le mesh brut s'étend en +X. Le miroir inverse aussi le sens des triangles.
    def to_unity(v):
        v = tuple((v[i] + pv[i]) * gs[i] for i in range(3))            # load_mesh a déjà retiré le pivot : on repart du sommet brut
        v = euler_xyz(gr, v)
        v = tuple((v[i] + gt[i] - pv[i]) * scale for i in range(3))
        v = quat_rotate(turn, (-v[0], v[1], v[2]))                     # demi-tour d'un fichier « bake »
        return quat_rotate(rot, (v[0] * rscl[0], v[1] * rscl[1], v[2] * rscl[2]))    # échelle des Transform du prefab

    def to_unity_dir(n):
        n = euler_xyz(gr, n)
        n = quat_rotate(turn, (-n[0], n[1], n[2]))
        return quat_rotate(rot, (n[0] / (rscl[0] or 1), n[1] / (rscl[1] or 1), n[2] / (rscl[2] or 1)))

    # Mesh skinné aux os connus : pose de repos par ses os, comme Unity (moyenne pondérée sur les clusters de
    # os du prefab · TransformLink⁻¹ · liaison du mesh · transform « geometric »), sommets bruts (sans recalage sur le pivot),
    # repère Unity (miroir X) et blocs. Remplace la transform du GameObject et la pose du nœud.
    sk = fbx.skin(path, raw['name']) if renderer.get('bones') else None
    if sk:
        cols = [euler_xyz(gr, tuple(gs[c] if k == c else 0.0 for k in range(3))) for c in range(3)]         # T · R · S « geometric »
        geo = [[cols[c][r] for c in range(3)] + [gt[r]] for r in range(3)] + [[0.0, 0.0, 0.0, 1.0]]
        per_vertex, total = {}, []
        for bone, tl, weights in sk['clusters']:
            if bone not in renderer['bones']: continue
            A = affine_mul(affine_inv(tl), affine_mul(sk['bind'], geo))
            total.append((sum(weights.values()), bone, A))
            for vi, w in weights.items(): per_vertex.setdefault(vi, []).append((w, bone, A))
        if not total: sk = None
    if sk:
        dominant = max(total, key=lambda t: t[0])

        def skinned(vi, v, direction=False):
            acc, wsum = [0.0, 0.0, 0.0], 0.0
            for w, bone, A in per_vertex.get(vi) or [(1.0, dominant[1], dominant[2])]:
                q = [sum(A[r][c] * v[c] for c in range(3)) + (0.0 if direction else A[r][3]) for r in range(3)]
                bpos, brot, bscl = renderer['bones'][bone]
                q = (-q[0] * (1.0 if direction else scale) * bscl[0], q[1] * (1.0 if direction else scale) * bscl[1], q[2] * (1.0 if direction else scale) * bscl[2])
                q = quat_rotate(brot, quat_rotate(turn, q))                    # demi-tour d'un fichier « bake » (cerf-volant)
                for i in range(3): acc[i] += w * (q[i] + (0.0 if direction else bpos[i]))
                wsum += w
            return tuple(c / (wsum or 1.0) for c in acc)
        raw_pos = [tuple(v[i] + pv[i] for i in range(3)) for v in raw['pos']]            # sommets bruts, sans pivot
        skin_pos = [skinned(vi, v) for vi, v in enumerate(raw_pos)]
        raw['skin_bones'] = True

    # Slots dans l'ordre des sous-meshes Unity, celui des matériaux du renderer : première apparition de l'index FBX dans
    # les polygones (bouée : flotteur, index 1, d'abord → slot 0), puis les index inutilisés (fbx.models, même ordre).
    order = []
    for s in raw['mat']:
        if s not in order: order.append(s)
    order += [i for i in range(len(raw.get('mat_names') or [])) if i not in order]
    rank = {s: k for k, s in enumerate(order)}
    nrm, uvs, mats = raw['nrm'], raw['uv'] or [], [rank[s] for s in raw['mat']]
    uv1, alpha = raw.get('uv1'), raw.get('alpha')
    slots = {}
    k = 0
    raw['mixed_slices'] = 0
    lo, hi = [math.inf] * 3, [-math.inf] * 3
    for p, poly in enumerate(raw['polys']):
        s = mats[p] if p < len(mats) else 0
        tiled = bool(uv1) and is_array(s)
        corners, slices = [], set()
        for v in poly:
            if sk:
                pos = apply(m, skin_pos[v])
                n = apply(m, skinned(v, nrm[k], True)) if nrm else None
            else:
                pos = apply(m, tuple(to_unity(raw['pos'][v])[i] + renderer['pos'][i] for i in range(3)))
                n = apply(m, to_unity_dir(nrm[k])) if nrm else None
            uv = uvs[k] if uvs else (0.0, 0.0)
            shade = round(255 * (1 - min(1.0, max(0.0, alpha[k])))) if tiled and alpha else 255
            if tiled: slices.add(int(round(uv1[k][0] * ARRAY_SLICE_SCALE)))
            k += 1
            corners.append((pos, n, uv, shade))
        if not nrm:
            a, b, c = (corners[i][0] for i in range(3))
            u = tuple(b[i] - a[i] for i in range(3)); w = tuple(c[i] - a[i] for i in range(3))
            n = (u[1] * w[2] - u[2] * w[1], u[2] * w[0] - u[0] * w[2], u[0] * w[1] - u[1] * w[0])
            corners = [(pos, n, uv, sh) for pos, _, uv, sh in corners]
        if len(slices) > 1: raw['mixed_slices'] += 1
        tranche = int(round(uv1[k - len(poly)][0] * ARRAY_SLICE_SCALE)) if tiled else None
        slot = slots.setdefault((s, tranche), {'verts': [], 'lookup': {}, 'idx': []})
        ids = []
        for pos, n, uv, shade in corners:
            length = math.sqrt(sum(c * c for c in n)) or 1.0
            key = tuple(round(c, 4) for c in pos) + tuple(round(c / length, 3) for c in n) + tuple(round(c, 4) for c in uv) + (shade,)
            if key not in slot['lookup']:
                slot['lookup'][key] = len(slot['verts']); slot['verts'].append(key)
            ids.append(slot['lookup'][key])
            for i in range(3): lo[i] = min(lo[i], pos[i]); hi[i] = max(hi[i], pos[i])
        flip = rscl[0] * rscl[1] * rscl[2] < 0                               # échelle négative : sens rétabli
        for i in range(1, len(ids) - 1): slot['idx'].extend((ids[0], ids[i], ids[i + 1]) if flip else (ids[0], ids[i + 1], ids[i]))   # sens inversé par le miroir X
    out = {}
    for s, slot in slots.items():
        verts = slot['verts']
        # Le rendu lit un u < −0,5 comme un mode sans texture : décalage entier, invisible en répétition.
        shift = math.ceil(-min(v[6] for v in verts)) if verts and min(v[6] for v in verts) < 0 else 0
        out[s] = {'pos': [c for v in verts for c in v[:3]], 'nrm': [c for v in verts for c in v[3:6]], 'idx': slot['idx'],
                  'uv': [c + (shift if j == 0 else 0) for v in verts for j, c in enumerate(v[6:8])], 'chan': [2] * len(verts)}
        if s[1] is not None and any(v[8] != 255 for v in verts): out[s]['shade'] = [v[8] for v in verts]
    return out, (lo, hi), raw


_alpha_cache = {}


def cutout_coverage(mat_path, tex_path, mesh):
    """Part d'un matériau de décalcomanies « TransparentCutout » (Decals.mat, Decal_Decorative_01.mat : vis, ornements ; pas
    les surfaces ajourées, grillage, vannerie, filet, tapis) : fraction de sa surface où l'alpha de la texture passe le
    seuil (grille de 15 échantillons par triangle, aux UV) ; None pour un autre matériau. Le rendu du planner n'a pas
    d'alpha : le fond transparent d'une décalcomanie y serait un aplat (blanc)."""
    tags = unity_yaml.material_document(mat_path).get('stringTagMap') or {}
    if tags.get('RenderType') != 'TransparentCutout' or 'decal' not in os.path.basename(mat_path).lower(): return None
    if tex_path not in _alpha_cache:
        img = Image.open(tex_path)
        _alpha_cache[tex_path] = img.convert('RGBA').getchannel('A') if 'A' in img.getbands() else None
    alpha = _alpha_cache[tex_path]
    if alpha is None: return 1.0
    w, h = alpha.size
    uv, idx, hits, total = mesh['uv'], mesh['idx'], 0, 0
    for t in range(0, len(idx), 3):
        corners = [(uv[2 * idx[t + k]] % 1.0, uv[2 * idx[t + k] + 1] % 1.0) for k in range(3)]
        for a, b in ((i / 4, j / 4) for i in range(5) for j in range(5 - i)):     # grille barycentrique, coins compris
            u, v = (corners[0][k] * a + corners[1][k] * b + corners[2][k] * (1 - a - b) for k in range(2))
            hits += alpha.getpixel((min(w - 1, int(u * w)), min(h - 1, int((1 - v) * h)))) >= 128; total += 1
    return hits / total if total else 1.0


# --- extraction ------------------------------------------------------------------------------------------------------

def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument('--content', required=True, help='dossier Eco/Content/Art')
    ap.add_argument('--prefab', action='append', default=[], metavar='Name[=Item]', help='prefab sous Player Built Objects, ex. HewnDoorObject (item = nom sans « Object » + « Item »)')
    ap.add_argument('--manifest', help='JSON { "set": "…", "prefabs": ["Name[=Item]", …] }')
    ap.add_argument('--set', help='nom du bundle (défaut : celui du manifeste, sinon Objects)')
    ap.add_argument('--out', required=True, help='dossier de sortie (assets/forms)')
    ap.add_argument('--axis', default=DEFAULT_AXIS, help='axes Unity pour x,y,z planner (défaut %s)' % DEFAULT_AXIS)
    args = ap.parse_args()

    specs, set_name, plain = list(args.prefab), args.set or 'Objects', {}
    if args.manifest:
        with open(args.manifest, encoding='utf-8') as f: manifest = json.load(f)
        specs = list(manifest.get('prefabs') or []) + specs
        set_name = args.set or manifest.get('set') or set_name
        plain = manifest.get('plain') or {}
    if not specs: ap.error('--prefab or --manifest required')

    started = time.time()
    m = parse_axis(args.axis)
    rots = [planner_angle(m, 90 * r) for r in range(4)]
    os.makedirs(args.out, exist_ok=True)
    log('== %s  axis %s  rot r0..3 → %s' % (set_name, args.axis, rots))

    assets = Assets(args.content)
    log('guid index: %d assets, %d prefab names' % (len(assets.index), len(assets.prefabs)))

    meshes, mesh_by_key, objects, texture_paths, tex_index, notes = {}, {}, {}, [], {}, []
    albedo_cache, fbx_models = {}, {}

    def fbx_model_names(fbx_path):
        """Modèles maillés d'un FBX hors LOD ≥ 1 et colliders, mémorisés ; [] si illisible (noté)."""
        if fbx_path not in fbx_models:
            try: fbx_models[fbx_path] = [name for name, _ in fbx.models(fbx_path) if not SKIP_RENDERER.search(name)] if (fbx_path or '').lower().endswith('.fbx') else []
            except Exception as e: fbx_models[fbx_path] = []; notes.append('%s: FBX unreadable (%s)' % (os.path.basename(fbx_path), e))
        return fbx_models[fbx_path]

    def albedo(mat_guid):
        """(chemin du .mat, chemin de l'albedo ou None, (lignes, colonnes) du flipbook si c'est un tableau du shader
        peinture, sinon None, teinte rgb ou None, verre rgba ou None) ; mémorisé. CurvedStandard / Standard multiplient la
        _MainTex par _Color (panneaux Joshua : texture Hardwood teintée) : teinte non blanche cuite dans la tuile. Rendu
        transparent (_Mode 2 fondu, 3 transparent, ou RenderType Transparent : vitres, verre des lampes) : verre de couleur
        _Color × albédo moyen, alpha _Color.a."""
        if mat_guid not in albedo_cache:
            mat_path = assets.get(mat_guid)
            tex_path, grid, tint, glass = None, None, None, None
            if mat_path and mat_path.lower().endswith('.mat'):
                _, tex_guid = unity_yaml.material_texture_guid(mat_path, ALBEDO_PROPS)
                tex_path = assets.get(tex_guid)
                tex_path = tex_path if tex_path and os.path.exists(tex_path) else None
                doc = unity_yaml.material_document(mat_path)
                shader = (doc.get('m_Shader') or {}).get('guid')
                props = doc.get('m_SavedProperties') or {}
                floats = {k: v for e in props.get('m_Floats') or [] for k, v in (e or {}).items()}
                color = next((v for e in props.get('m_Colors') or [] for k, v in (e or {}).items() if k == '_Color'), None) or {}
                rgba = tuple(1.0 if color.get(c) is None else float(color[c]) for c in 'rgba')
                if shader in UV_MAPPED_SHADERS and rgba[:3] != (1.0, 1.0, 1.0): tint = rgba[:3]
                if shader != TINTABLE_SHADER and (float(floats.get('_Mode') or 0) >= 2 or (doc.get('stringTagMap') or {}).get('RenderType') == 'Transparent'):
                    mean = (1.0, 1.0, 1.0)
                    if tex_path:
                        with Image.open(tex_path) as img: mean = tuple(c / 255 for c in ImageStat.Stat(img.convert('RGB').resize((64, 64))).mean)
                    glass = [round(rgba[i] * mean[i], 3) for i in range(3)] + [round(rgba[3], 3)]
                if shader == TINTABLE_SHADER and tex_path and os.path.exists(tex_path + '.meta'):
                    importer = (unity_yaml.parse_plain(tex_path + '.meta') or {}).get('TextureImporter') or {}
                    if importer.get('textureShape') == 4:
                        grid = (int(importer.get('flipbookRows') or 1), int(importer.get('flipbookColumns') or 1))
            albedo_cache[mat_guid] = (mat_path, tex_path, grid, tint, glass)
        return albedo_cache[mat_guid]

    for spec in specs:
        prefab_name, _, item = spec.partition('=')
        item = item or (prefab_name[:-len('Object')] if prefab_name.endswith('Object') else prefab_name) + 'Item'
        path, count = assets.prefab(prefab_name)
        if not path:
            notes.append('%s: %d prefabs found, skipped' % (prefab_name, count)); continue
        prefab = load_prefab(path, assets, notes)
        log('  prefab %-34s %s' % (prefab_name, os.path.relpath(path, assets.objects_root)))
        cells = sorted(set(tuple(int(c) for c in apply(m, c)) for c in occupancy_cells(prefab)))
        log('    occupancy (planner) %s' % (cells or 'none'))
        parts = []
        # Mesh partagé : un GameObject nommé comme un modèle du FBX désigne ce modèle pour les autres GameObjects au même
        # fileID de mesh (roue 1 des charrettes = mesh de la roue 2).
        siblings = {(r['fbx'], r['mesh_id']): r['model'] for r in prefab['renderers'] if r.get('mesh_id') and r['model'] in fbx_model_names(r['fbx'])}
        for r in prefab['renderers']:
            if r['name'] and SKIP_RENDERER.search(r['name']): continue
            if any(abs(c) < 1e-6 for c in r['scl']): continue           # échelle nulle : invisible (sillage du chalutier)
            fbx_path = r['fbx']
            if not fbx_path or not fbx_path.lower().endswith('.fbx'):
                notes.append('%s/%s: mesh not an FBX (%s)' % (prefab_name, r['name'], fbx_path)); continue
            candidates = fbx_model_names(fbx_path)
            if not candidates: notes.append('%s/%s: no mesh model in %s' % (prefab_name, r['name'], os.path.basename(fbx_path))); continue
            # Mesh désigné par son fileID (table du .meta ou id haché : tente du campement, GameObject « TentBedSection » et
            # mesh « BedSection » ; étiquettes « WasteLabel » du tri → WasteLabel_2, _3… ; sinon un GameObject frère au même
            # fileID), sinon par le nom du GameObject.
            by_id = (assets.fbx_mesh_names(fbx_path).get(r.get('mesh_id')) or next((c for c in candidates if mesh_file_id(c) == r.get('mesh_id')), None)
                     or siblings.get((fbx_path, r.get('mesh_id'))))
            model = by_id if by_id in candidates else pick_model(r['model'], candidates)
            if by_id not in candidates and len(candidates) > 1 and re.sub(r'(\.\d+|_\d+| \([^)]*\))$', '', model) != re.sub(r'(\.\d+|_\d+| \([^)]*\))$', '', r['model'] or ''):
                notes.append('%s/%s: FBX has %d models, « %s » not among them, « %s » used' % (prefab_name, r['name'], len(candidates), r['model'], model))
            def slot_material(s, r=r):
                return r['materials'][s] if s < len(r['materials']) else (r['materials'][0] if r['materials'] else None)

            def is_array(s):
                return albedo(slot_material(s))[2] is not None
            arrays = tuple(is_array(s) for s in range(max(1, len(r['materials']))))
            # MeshFilter du prefab : le mesh importé est dans le repère de son nœud (ni pose ni Lcl Scaling du nœud : BoxCollider
            # du même GameObject, calés par Unity sur le mesh), placé par le seul GameObject ; « posed » (clip du prefab ou os
            # inconnus) : pose du nœud en plus. L'autre choix sert de repli aux meshes skinnés que les os ne posent pas.
            other = None
            if not r.get('node'):
                with_pose = {**r, **dict(zip(('pos', 'rot', 'scl'), compose(r['pos'], r['rot'], *node_pose(fbx_path, model, skip_single_root=False)[:2], r['scl'])))}
                r, other = (with_pose, r) if r.get('posed') else (r, with_pose)
            key = (fbx_path, model, tuple(round(c, 4) for c in r['pos']), tuple(round(r['rot'][k], 4) for k in 'xyzw'), tuple(round(c, 4) for c in r['scl']), arrays)
            if key not in mesh_by_key:
                try:
                    slots, (lo, hi), raw = convert_mesh(fbx_path, {**r, 'model': model}, m, is_array)
                    # Mesh skinné sans ses os dans le prefab : Unity le place par ses os, dont la pose de liaison ne suit pas
                    # toujours le nœud (scierie : sommets déjà Y-up). Entre les deux poses, garder celle qui rentre dans l'occupancy.
                    if is_skinned(fbx_path, model) and cells and not raw.get('skin_bones'):
                        alt = r.get('alt') or other
                        slots2, (lo2, hi2), raw2 = convert_mesh(fbx_path, {**alt, 'model': model}, m, is_array)
                        if fit_score(lo2, hi2, cells) < fit_score(lo, hi, cells) - 1e-6:
                            slots, (lo, hi), raw = slots2, (lo2, hi2), raw2
                            notes.append('%s/%s: skinned mesh placed %s its node pose (occupancy fit)' % (prefab_name, r['name'], 'with' if alt is other and not r.get('posed') else 'without'))
                except Exception as e:                                # FBX exotique : part ignorée
                    notes.append('%s/%s: FBX unreadable (%s)' % (prefab_name, r['name'], e)); continue
                base = os.path.splitext(os.path.basename(fbx_path))[0]
                if len(candidates) > 1: base = '%s_%s' % (base, raw['name'])
                names, several = {}, len({s for s, _ in slots}) > 1
                for (s, tranche), data in slots.items():
                    name = '%s_m%d' % (base, s) if several else base
                    if tranche is not None: name += '_s%d' % tranche
                    while name in meshes: name += '_'
                    meshes[name] = data; names[(s, tranche)] = name
                mesh_by_key[key] = names
                if raw['mixed_slices']: notes.append('%s/%s: %d polygon(s) span several texture array slices, first corner used' % (prefab_name, r['name'], raw['mixed_slices']))
                log('    mesh %-32s %4d verts %4d polys → %2d slot(s) %5d verts %5d tris  bbox %s..%s  uv %s' % (
                    base, len(raw['pos']), len(raw['polys']), len(slots), sum(len(d['pos']) // 3 for d in slots.values()),
                    sum(len(d['idx']) // 3 for d in slots.values()), ['%.3f' % c for c in lo], ['%.3f' % c for c in hi], 'yes' if raw['uv'] else 'NO'))
            for (s, tranche), mesh_name in mesh_by_key[key].items():
                mat_path, tex_path, grid, tint, glass = albedo(slot_material(s))
                color = plain.get(slot_material(s)) if not mat_path else None        # matériau absent : teinte du manifeste
                if glass and glass[3] < 0.05:
                    notes.append('%s/%s: slot %d is invisible glass (%s, alpha %.2f), part skipped' % (prefab_name, r['name'], s, os.path.basename(mat_path), glass[3])); continue
                if not tex_path and not glass and not color:
                    notes.append('%s/%s: albedo not resolved for slot %d (%s), part skipped' % (prefab_name, r['name'], s, os.path.basename(mat_path) if mat_path else 'no material')); continue
                cover = cutout_coverage(mat_path, tex_path, meshes[mesh_name]) if tex_path else None
                if cover is not None and cover < 0.5:
                    notes.append('%s/%s: cutout decal %s opaque on %d%% of its surface, part skipped' % (prefab_name, r['name'], os.path.basename(mat_path), round(100 * cover))); continue
                # Tranche d'un tableau : une tuile par tranche utilisée, découpée dans l'image source (flipbook) ; verre sans
                # texture ou matériau absent : tuile unie de sa couleur.
                if tranche is not None: entry, tex_key = (tex_path, None, None, grid + (tranche,)), (tex_path, tranche)
                elif not tex_path: entry = tex_key = (None, tuple(color or glass[:3]))
                elif tint: entry = tex_key = (tex_path, tint)
                else: entry = tex_key = tex_path
                if tex_key not in tex_index:
                    tex_index[tex_key] = len(texture_paths); texture_paths.append(entry)
                log('    part %-32s %s → %s%s%s%s (tile %d)' % (mesh_name, os.path.basename(mat_path) if mat_path else 'missing material', os.path.relpath(tex_path, assets.art_root) if tex_path else 'plain',
                    '' if tranche is None else ' slice %d' % tranche, ' × %.3f %.3f %.3f' % tint if tint else '', ' glass %s' % glass if glass else '', tex_index[tex_key]))
                part = {'mesh': mesh_name, 'material': tex_index[tex_key]}
                if glass: part['glass'] = glass
                if part not in parts: parts.append(part)          # torche allumée et éteinte superposées : une seule fois
        if not parts:
            notes.append('%s: no renderable part, skipped' % prefab_name); continue
        objects[item] = {'parts': parts, 'rot': rots, 'cells': [list(c) for c in cells]}

    # Meshes qu'aucune part ne référence (slot sans texture) : retirés du bundle.
    used = {p['mesh'] for o in objects.values() for p in o['parts']}
    meshes = {k: v for k, v in meshes.items() if k in used}

    bundle = {
        'set': set_name, 'version': BUNDLE_VERSION, 'kind': 'objects',
        'calibration': {'axis': args.axis, 'unityToPlanner': m, 'rotation': 'degrees around +z applied to the mesh: x\' = x·cos − y·sin, y\' = x·sin + y·cos',
                        'note': 'objects.<Item>.parts = meshes drawn at the anchor cell centre, rot[r] = planner angle for the object rotation r; cells = occupancy offsets (planner axes) for reference'},
        'meshes': meshes,
        'materials': [{'side': i, 'top': None, 'detail': i, 'scale': 1.0, 'offset': 0.0} for i in range(len(texture_paths))],
        'objects': objects,
    }
    atlas, bundle['tile'] = textures.build_atlas(texture_paths) if texture_paths else (None, 512)
    json_path = os.path.join(args.out, set_name + '.json')
    with open(json_path, 'w', encoding='utf-8') as f:
        json.dump(bundle, f, separators=(',', ':'))
    webp_path, preview_path = os.path.join(args.out, set_name + '.webp'), os.path.join(args.out, set_name + '.preview.png')
    if atlas is not None: textures.save_atlas(atlas, webp_path, preview_path)

    index_path = os.path.join(args.out, 'index.json')
    index_doc = {'sets': {}, 'materials': {}}
    if os.path.exists(index_path):
        with open(index_path, encoding='utf-8') as f: index_doc = json.load(f)
    index_doc.setdefault('sets', {})[set_name] = {'json': set_name + '.json', 'atlas': set_name + '.webp'}
    index_doc.setdefault('objects', {})
    for item in objects: index_doc['objects'][item] = set_name
    with open(index_path, 'w', encoding='utf-8') as f:
        json.dump(index_doc, f, indent=2, sort_keys=True)

    log()
    log('== summary')
    log('objects (%d): %s' % (len(objects), ', '.join('%s(%s)' % (k, ', '.join(p['mesh'] for p in v['parts'])) for k, v in objects.items())))
    log('meshes: %d, vertices: %d, triangles: %d, textures: %d (tile %d)' % (len(meshes), sum(len(v['pos']) // 3 for v in meshes.values()),
        sum(len(v['idx']) // 3 for v in meshes.values()), len(texture_paths), bundle['tile']))
    for n in notes: log('note: ' + n)
    for path in (json_path, webp_path, preview_path, index_path):
        if os.path.exists(path): log('wrote %s (%d KB)' % (path, os.path.getsize(path) // 1024))
    log('done in %.1fs' % (time.time() - started))


if __name__ == '__main__':
    main()
