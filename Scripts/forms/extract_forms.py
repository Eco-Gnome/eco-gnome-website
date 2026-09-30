"""Extrait un « bundle de formes » (meshes + cas de sélection + atlas de textures) d'un set de blocs d'Eco.

    python Scripts/forms/extract_forms.py --content "<checkout>/Eco/Content/Art" --set "Mortared Stone" --out ecocraft/wwwroot/assets/forms

Sources : `Blocks/Blocksets/Player Building Blocks/*.asset` (blocs → builder + matériau + catégorie), les builders
`.asset` du dossier du set (cas d'usage : mesh, importRotation, conditions), les FBX/prefabs des meshes et le `.mat`
de chaque skin. Sortie : `<Set>.json`, `<Set>.webp`, `<Set>.preview.png`, `index.json` (fusionné).

Repères. Unity : X, Y haut, Z (main gauche). Planner : x est, y sud, z haut. La matrice UNITY_TO_PLANNER (option
--axis) est la seule constante à calibrer en jeu, avec les BASE_OFFSET par forme (option --base-offset).
Rotation d'une forme dans le bundle : angle en degrés autour de +z, appliqué au mesh recentré par
x' = x·cos − y·sin, y' = x·sin + y·cos.
"""
import argparse
import glob
import json
import math
import os
import re
import sys
import time

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import fbx                # noqa: E402
import textures           # noqa: E402
import unity_yaml         # noqa: E402

BUNDLE_VERSION = 4
UNITS_PER_BLOCK = 100.0

# Défaut : x = X, y = Z, z = Y (PlanDocument.cs : x → Eco X, y → Eco Z, z → Eco Y).
DEFAULT_AXIS = 'x,z,y'
# Décalage de rotation (degrés Unity, autour de Y) ajouté aux formes explicites, par forme ; à régler en jeu.
BASE_OFFSET = {}

# Formes produites en v1 (nom court = nom du bloc sans le préfixe du matériau ni le suffixe 90/180/270).
EXPLICIT_FORMS = ['Stairs', 'RoofSide', 'RoofTurn', 'RoofCorner', 'RoofPeak', 'Cube']
CONTEXT_FORMS = ['Wall', 'Floor', 'Column', 'WindowGrilles', 'Chimney', 'Roof', 'RoofPeakSet']
# Quais (Hewn Logs) et divers : classés d'après leurs blocs — 4 blocs tournés sans condition → explicite (Ladder,
# DocksRampA…), un bloc à conditions → contextuel (DocksColumn, Stacked1…), 4 blocs tournés à conditions → « rots »
# (un jeu de cas par rotation : DocksPlatform, DocksFenceMid…).
DOCK_FORMS = ['DocksPlatform', 'DocksPlatformFill', 'DocksColumn', 'DocksPillar', 'DocksPillarBeam', 'DocksPillarBeamCorner',
              'DocksPillarBeamEnd', 'DocksPillarBeamEndAlt', 'DocksPillarBeamJunction', 'DocksPillarBeamT', 'DocksPillarBeamX',
              'DocksFenceMid', 'DocksFenceCorner', 'DocksFenceT', 'DocksFenceX', 'DocksFenceEndCap', 'DocksFenceEndCapDouble',
              'DocksFenceSolo', 'DocksRamps', 'DocksRampsCorner', 'DocksRampsCornerInverted', 'DocksRampA', 'DocksRampB',
              'DocksRampC', 'DocksRampD', 'DocksBarrelPlatform', 'Ladder', 'Stacked1', 'Stacked2', 'Stacked3']
# FarEast : murs à colombages (le joueur choisit la variante et l'orientation), barrières, escaliers, toits à rives.
FAREAST_FORMS = ['Wall_%02d' % i for i in range(1, 20)] + ['%s_%02d' % (w, i) for w in ('WallCorner', 'WallT', 'WallX') for i in range(1, 5)] + [
    'Column_01', 'Column_02', 'Column_03', 'Ceiling', 'Window', 'FenceMid', 'FenceCorner', 'FenceT', 'FenceX', 'FenceSolo', 'FenceEnd',
    'StairsMid', 'StairsEndLeft', 'StairsEndRight', 'StairsTurn', 'StairsCorner', 'RoofEdgeSide', 'RoofEdgeCorner', 'RoofEdgeTurn',
    'RoofPeakCorner', 'RoofPeakT', 'RoofPeakX', 'RoofUnderslopeSide', 'RoofUnderslopeTurn', 'RoofUnderslopeCorner', 'UnderInnerPeak', 'RoofCube']
# Formes propres à un set (d'autres sets en ont aussi en jeu, ex. ThinFloorTop de Brick, non extraites). Verre (set hors
# de Full Building Types) : vitres minces au milieu, en haut, en bas ou au bord de la case, pile de 4 ; Window (vitre qui
# se raccorde à ses voisins) et Cube (vitre dans un cadre en bois) sont dans les listes communes.
SET_FORMS = {'Glass': ['FlatRoof', 'ThinFloorTop', 'ThinFloorBottom', 'ThinWallStraight', 'ThinWallCorner', 'EdgeWall', 'EdgeWallTurn', 'Stacked4'],
             # Brick : toutes les formes au marteau de Brick.cs (contreforts, pentes simples, dalles et murs minces, fenêtres
             # de bord, aqueduc, rampes du blockset des routes) + Stacked4.
             'Brick': ['Aqueduct', 'BasicSlopePoint', 'BasicSlopeSide', 'UnderSlopePeak', 'UnderSlopeSide', 'Brace', 'BraceCorner', 'BraceTurn',
                       'SideBrace', 'SmallCornerBrace', 'UnderBrace', 'UnderBraceCorner', 'UnderBraceTurn', 'ThinFloorTop', 'ThinFloorBottom',
                       'ThinWallEdge', 'WindowEdge', 'WindowGrillesEdge', 'RampA', 'RampB', 'RampC', 'RampD', 'Stacked4'],
             # Lumber : barrière au marteau (Lumber.cs ; contextuelle : droite, coin, T, croix, coiffée si rien dessus).
             'Lumber': ['Fence'],
             # T4 : formes au marteau de CorrugatedSteel.cs et ReinforcedConcrete.cs + Stacked4. Les blocs hors marteau du
             # blockset (SlopeSide, ClippedFloatStairs*, pièces en bouleau du Composite Lumber du béton) restent ignorés.
             'Corrugated Steel': ['Fence', 'DoubleWindow', 'FlatRoof', 'BasicSlopeSide', 'BasicSlopePoint', 'UnderSlopeSide', 'UnderSlopePeak',
                                  'FloatStairs', 'FloatStairsTurn', 'FloatStairsCorner', 'RoadBarrier', 'Stacked4'],
             'Reinforced Concrete': ['ThinColumn', 'DoubleWindow', 'PeakSet', 'UnderPeakSet', 'RoadBarrier', 'Fence', 'FlatRoof', 'UnderStairs',
                                     'BasicSlopeSide', 'BasicSlopeCorner', 'BasicSlopeTurn', 'BasicSlopePoint', 'UnderSlopeSide', 'UnderSlopeCorner',
                                     'UnderSlopeTurn', 'UnderSlopePeak', 'HalfSlopeA', 'HalfSlopeB', 'Stacked4'],
             # T5 : formes au marteau d'AshlarBasalt.cs, CompositeLumber.cs, FlatSteel.cs, FramedGlass.cs + Stacked4 (le reste est
             # dans les listes communes). Ignorés : pentes intelligentes dépréciées (Slope*), PeakSet et UnderPeakSet du verre encadré.
             'Ashlar Stone': ['FullWall', 'FlatRoof', 'Fence', 'DoubleWindow', 'PeakSet', 'UnderPeakSet', 'RampA', 'RampB', 'RampC', 'RampD',
                              'UnderStairs', 'FloatStairs', 'FloatStairsTurn', 'FloatStairsCorner', 'Brace', 'BraceCorner', 'BraceTurn', 'SideBrace',
                              'UnderBrace', 'UnderBraceCorner', 'UnderBraceTurn', 'BasicSlopeSide', 'BasicSlopeCorner', 'BasicSlopeTurn',
                              'BasicSlopePoint', 'UnderSlopeSide', 'UnderSlopeCorner', 'UnderSlopeTurn', 'UnderSlopePeak', 'HalfSlopeA', 'HalfSlopeB',
                              'Stacked4'],
             'Composite Lumber': ['FullWall', 'WindowWall', 'CladWall', 'WallTrim', 'SideFence', 'Fence', 'DoubleWindow', 'FlatRoof', 'ThinColumn',
                                  'PeakSet', 'UnderPeakSet', 'RampA', 'RampB', 'RampC', 'RampD', 'UnderStairs', 'FloatStairs', 'FloatStairsTurn',
                                  'FloatStairsCorner', 'Brace', 'UnderBrace', 'SideBrace', 'BasicSlopeSide', 'BasicSlopeCorner', 'BasicSlopeTurn',
                                  'BasicSlopePoint', 'UnderSlopeSide', 'UnderSlopeCorner', 'UnderSlopeTurn', 'UnderSlopePeak', 'Stacked4'],
             'Flat Steel': ['FlatRoof', 'ThinColumn', 'DoubleWindow', 'PeakSet', 'UnderPeakSet', 'Fence', 'BasicSlopeSide', 'BasicSlopeCorner',
                            'BasicSlopeTurn', 'BasicSlopePoint', 'UnderSlopeSide', 'UnderSlopeCorner', 'UnderSlopeTurn', 'UnderSlopePeak',
                            'FloatStairs', 'FloatStairsTurn', 'FloatStairsCorner', 'WindowCorners', 'Stacked4'],
             'Framed Glass': ['DoubleWindow', 'BasicSlopeSide', 'BasicSlopeCorner', 'BasicSlopeTurn', 'BasicSlopePoint', 'UnderSlopeSide',
                              'UnderSlopeCorner', 'UnderSlopeTurn', 'UnderSlopePeak', 'Stacked4'],
             # Adobe : murs et parapets posés tournés par le joueur (Mid, Solo, End, Corner, T, X), le reste est commun.
             'Adobe Brick': ['WallSolo', 'WallMid', 'WallEnd', 'WallT', 'WallCorner', 'WallX', 'RoofSolo', 'RoofMid', 'RoofEnd', 'RoofT',
                             'RoofX', 'RoofFill', 'StairsSolo', 'UnderSlopeSide'],
             # Routes (Player Built Blocks/Roads Types, blocksets Road Blocks) : asphalte à lignes blanches peintes, rampes.
             'Asphalt Road': ['WhiteCube', 'WhiteLine', 'WhiteDashLine', 'WhiteEdge', 'WhiteEdgeRotate', 'TwoWhiteEdgeRotate',
                              'WhiteRampLineA', 'WhiteRampLineB', 'WhiteRampLineC', 'WhiteRampLineD', 'WhiteRampDashLineA',
                              'WhiteRampDashLineB', 'WhiteRampDashLineC', 'WhiteRampDashLineD', 'WhiteRampEdgeA', 'WhiteRampEdgeB',
                              'WhiteRampEdgeC', 'WhiteRampEdgeD', 'RampA', 'RampB', 'RampC', 'RampD'],
             'Stone Road': ['RampA', 'RampB', 'RampC', 'RampD'],
             # Tapis (Player Built Blocks/Fabric Blocks) : dalle sans bordure, auvent de fenêtre, mur plein, pile de 4.
             'Fabric Blocks': ['SimpleFloor', 'CanopyWindow', 'FullWall', 'Stacked4']}
# Formes des listes communes à écarter pour un set : ancienne fenêtre en bouleau du béton (pas au marteau).
SET_EXCLUDED = {'Reinforced Concrete': ['WindowGrilles']}
# Blocksets rangés ailleurs dont les blocs appartiennent au set (chemin sous Blocks/Blocksets/) : rampes en brique,
# barrière de route en acier (seuls les blocs au préfixe du set sont pris).
EXTRA_BLOCKSETS = {'Brick': ['Road Blocks/Brick Ramp Blocks.asset'], 'Corrugated Steel': ['Road Blocks/Road Blocks.asset']}
# Skins sans item en jeu (préfixe des blocs) : pierre « Stone » d'Ashlar, bois dur et tendre du Composite Lumber.
SKIN_EXCLUDED = {'Ashlar Stone': ['AshlarStone'], 'Composite Lumber': ['CompositeHardwoodLumber', 'CompositeSoftwoodLumber']}
# Sets dont les décors des builders (decorativeBuilders) sont fondus dans les meshes : poutres des parapets Adobe, rebord
# en bois du gravier.
DECORATIVE_SETS = {'Adobe Brick', 'Garden Gravel'}
# Préfixes des skins imposés (item = préfixe + Item) quand le préfixe commun ne convient pas : un blockset pour 6 tissus (tapis
# seuls, les rideaux ne sont pas dans le planner), blocksets d'un seul bloc de la route en pierre (cube, rampes).
SKIN_PREFIXES = {'Fabric Blocks': ['CottonCarpet', 'NylonCarpet', 'WoolCarpet'], 'Stone Road': ['StoneRoad']}
# Shader du verre des blocs (Curved/Triplanar Obscure Glass) : les triangles d'un slot qui le porte sont translucides.
GLASS_SHADERS = {'19b4023ba40dcd04badd9156cb30af7f'}
# Prédicats de voisinage que le rendu sait évaluer ; un cas portant une autre règle est omis (jamais satisfait).
# 'solid' : voisin non vide (IsSolidType / NotSolidType ; le ruleString du jeu, ex. 'MetalRoof', est ignoré).
# NotEqualsType(WorldObject) (toits plats T4) est tenu pour vrai : toujours couplé à un NotSolid sur le même voisin, il
# n'écarte qu'un objet posé juste au-dessus, que le rendu ne voit pas comme un bloc.
SUPPORTED_PREDICATES = {'category:Building', 'sameType', 'solid'}
# Shaders sans masque triplanaire (CurvedStandard du ModKit, Standard d'Unity) : la _MainTex est plaquée par les UV du
# mesh sur tout le bloc (matériaux « Simple » des pierres et logs empilés). Les autres sont les TriplanarMasked*.
UV_MAPPED_SHADERS = {'b317ea5cc069fde4f94662eac4cb8f1e', '0000000000000000f000000000000000'}
# Shader métallique (TriplanarMaskedMetalness) : _SideMetalness / _TopMetalness / _DetailMetalness, R = métal. Le planner ne
# rend pas la métallicité ; un métal au rendu du jeu paraît plus sombre que son albédo (toit en tôle du béton blanchâtre
# sinon) : la tuile est assombrie de METAL_DARKEN × métal. Facteur à caler en jeu.
METAL_SHADERS = {'b4554f25ef93f3f4b925546eafe8713c'}
METAL_DARKEN = 0.5
# Texture « détail » de remplacement (chemin sous Blocks/) pour les sets dont celle du jeu n'est pas dans Content/Art :
# le cerclage en bois du gravier de jardin.
DETAIL_FALLBACK = {'Garden Gravel': os.path.join('Player Built Blocks', 'Full Building Types', 'Wood Textures', 'WoodDetail_Hardwood_Albedo.png')}

# OffsetCondition.OffsetMapping (Unity : x, y haut, z), index = enum Offset.
OFFSET_MAPPING = [
    (-1, 1, -1), (0, 1, -1), (1, 1, -1), (-1, 1, 0), (0, 1, 0), (1, 1, 0), (-1, 1, 1), (0, 1, 1), (1, 1, 1),
    (-1, 0, -1), (0, 0, -1), (1, 0, -1), (-1, 0, 0), (1, 0, 0), (-1, 0, 1), (0, 0, 1), (1, 0, 1),
    (-1, -1, -1), (0, -1, -1), (1, -1, -1), (-1, -1, 0), (0, -1, 0), (1, -1, 0), (-1, -1, 1), (0, -1, 1), (1, -1, 1),
]
# BlockRule.RuleType
RULE_NOT_EQUALS_TYPE, RULE_NOT_SOLID, RULE_IS_SOLID, RULE_EQUALS_CATEGORY, RULE_NOT_EQUALS_CATEGORY, RULE_EQUALS_THIS, RULE_NOT_EQUALS_THIS = 1, 2, 3, 4, 5, 6, 7
RULE_NAMES = ['EqualsType', 'NotEqualsType', 'NotSolidType', 'IsSolidType', 'EqualsCategory', 'NotEqualsCategory',
              'EqualsThisType', 'NotEqualsThisType', 'SameOrHigherPriority', 'LowerPriorirty']


log_lines = []


def log(msg=''):
    print(msg.encode(sys.stdout.encoding or 'utf-8', 'replace').decode(sys.stdout.encoding or 'utf-8'))
    log_lines.append(msg)


# --- repères ---------------------------------------------------------------------------------------------------------

def parse_axis(spec):
    """'x,z,y' → matrice 3×3 M telle que planner = M · unity (lignes = axes planner x, y, z)."""
    rows = []
    for token in spec.split(','):
        token = token.strip().lower()
        sign = -1 if token.startswith('-') else 1
        axis = 'xyz'.index(token.lstrip('+-'))
        rows.append([sign if i == axis else 0 for i in range(3)])
    if sorted(abs(v) for row in rows for v in row if v) != [1, 1, 1] or len(rows) != 3:
        raise ValueError('bad --axis %r' % spec)
    return rows


def apply(m, v):
    return tuple(sum(m[i][j] * v[j] for j in range(3)) for i in range(3))


def rot_y(deg, v):
    """Rotation Unity autour de Y (quaternion.RotateY) : +90° envoie +Z sur +X."""
    c, s = round(math.cos(math.radians(deg))), round(math.sin(math.radians(deg)))
    x, y, z = v
    return (x * c + z * s, y, -x * s + z * c)


def planner_angle(m, unity_deg):
    """Angle autour de +z (formule x' = x cos − y sin) équivalent à une rotation Unity de unity_deg autour de Y."""
    inv = [[m[j][i] for j in range(3)] for i in range(3)]   # M est une permutation signée : inverse = transposée
    u = apply(inv, (1, 0, 0))
    p = apply(m, rot_y(unity_deg, u))
    return round(math.degrees(math.atan2(p[1], p[0]))) % 360


# --- assets ----------------------------------------------------------------------------------------------------------

def form_name(block_name, prefix):
    """'MortaredSandstoneStairs90' → ('Stairs', 1)."""
    m = re.fullmatch(r'(.+?)(90|180|270)?', block_name[len(prefix):])
    return m.group(1), {None: 0, '90': 1, '180': 2, '270': 3}[m.group(2)]


def common_prefix(names):
    prefix = os.path.commonprefix(names)
    return prefix


def load_blockset(path):
    doc = unity_yaml.first_document(path)
    blocks = [b for b in doc.get('Blocks', []) or [] if b.get('Builder', {}).get('guid')]
    names = [b['Name'] for b in blocks]
    return {'path': path, 'name': doc.get('m_Name'), 'blocks': blocks, 'prefix': common_prefix(names)}


def load_builder(path):
    doc = unity_yaml.first_document(path)
    cases = []
    for uc in doc.get('usageCases', []) or []:
        if not uc.get('enabled', 1): continue
        mesh = uc.get('mesh') or {}
        conditions = []
        for cond in uc.get('conditions', []) or []:
            rules = [(r.get('ruleType'), (r.get('ruleString') or '').strip()) for r in cond.get('rules', []) or []]
            conditions.append((OFFSET_MAPPING[cond['offsetType']], rules))
        rot = uc.get('importRotation') or {}
        cases.append({
            'mesh_guid': mesh.get('guid'), 'mesh_file_id': mesh.get('fileID'),
            'import_rot': float(rot.get('y', 0) or 0),
            # x et z de importRotation (Euler ZXY, appliqués avant y) : retournements cuits dans le mesh (pentes « Under »).
            'flip': (round(float(rot.get('x', 0) or 0)) % 360, round(float(rot.get('z', 0) or 0)) % 360),
            'conditions': conditions,
            'all_rotations': bool(uc.get('applyConditionsToAllRotations', 1)),
            'axis': uc.get('axis', 1), 'dont_rotate': bool(uc.get('dontRotateBaseMesh', 0)),
            'decorative': [d['guid'] for d in uc.get('decorativeBuilders', []) or [] if d and d.get('guid')],
        })
    return {'path': path, 'name': doc.get('m_Name'), 'cases': cases}


def decorate(compiled, index, unsupported):
    """Builders décoratifs (poutres des parapets Adobe, rebord du gravier) : le jeu ajoute au mesh du cas retenu celui du
    premier cas du décor dont les conditions tiennent (aucun sinon ; un cas sans mesh = « rien »). Sur les cas compilés
    (conditions finales, rotations déroulées) : chaque cas à décor devient un cas par cas du décor compatible (conditions
    réunies, mesh du décor dans parts : (guid, fileID, flip, rotation Unity)), puis le cas seul ; décors imbriqués compris.
    Une entrée qu'une entrée plus haut couvre (sous-ensemble de ses conditions) ne peut jamais servir : retirée."""
    out = []
    for c in compiled:
        entries = [dict(c, parts=[])]
        for guid in c['decorative']:
            sub = [d for d in decorate(compile_cases(load_builder(index[guid])['cases'], unsupported), index, unsupported) if not d['dead']] if guid in index else []
            def merged(e, d):
                conds = dict(((tuple(off), pred), exp) for off, exp, pred in e['conds'])
                for off, exp, pred in d['conds']:
                    if conds.setdefault((tuple(off), pred), exp) != exp: return None   # conditions contraires
                return dict(e, conds=[(off, exp, pred) for (off, pred), exp in conds.items()],
                            parts=e['parts'] + ([(d['mesh_guid'], d['file_id'], d['flip'], d['rot_unity'])] if d['mesh_guid'] else []) + d['parts'])
            entries = [x for x in (merged(e, d) for e in entries for d in sub) if x] + entries
        out += entries
    kept = []
    for e in out:
        key = {(tuple(off), exp, pred) for off, exp, pred in e['conds']}
        if not any(not k['dead'] and k['key'] <= key for k in kept): kept.append(dict(e, key=key))
    return kept


def resolve_mesh_path(index, guid, file_id):
    """guid d'un cas d'usage → chemin du FBX (via le MeshFilter racine si c'est un prefab)."""
    path = index.get(guid)
    if path is None: return None
    if path.lower().endswith('.prefab'):
        mesh_guid = unity_yaml.prefab_mesh_guid(path, file_id)
        path = index.get(mesh_guid) if mesh_guid else None
    return path if path and path.lower().endswith('.fbx') else None


# --- meshes ----------------------------------------------------------------------------------------------------------

def load_mask(path):
    """Masque de texture du shader TriplanarMasked (BuildingMask.tga) : fonction (u, v) → canal 0 côté (vert, triplanaire),
    1 dessus (rouge, triplanaire), 2 détail (bleu, plaqué par les UV du mesh)."""
    from PIL import Image
    img = Image.open(path).convert('RGB'); px = img.load(); w, h = img.size
    def channel(uv):
        r, g, b = px[int((uv[0] % 1.0) * (w - 1)), int(((1.0 - uv[1]) % 1.0) * (h - 1))]
        return 1 if r > g and r > b else 2 if b > g else 0
    return channel


def shader_guid(mat_path):
    if not mat_path: return None
    found = re.search(r'm_Shader:.*?guid: ([0-9a-f]+)', open(mat_path, encoding='utf-8', errors='replace').read())
    return found.group(1) if found else None


def is_uv_mapped(mat_path): return shader_guid(mat_path) in UV_MAPPED_SHADERS
def is_glass(mat_path): return shader_guid(mat_path) in GLASS_SHADERS


def convert_mesh(raw, m, mask=None, slot_masks=None, glass_slot=None):
    """FBX (unités, Unity) → planner : sommets dédoublonnés (pos + normale + canal de texture), triangles. Les polygones
    du sous-matériau FBX nommé « Glass… » ou « Solid Glass… » (vitres des fenêtres ; pas « FramedGlassSteel… », l'acier du
    verre encadré), sinon du slot glass_slot (slot dont le matériau du bloc a le shader du verre : vitres du set Glass), vont dans `glass`, dessinés translucides avec la teinte du set. Canal par
    sommet (uv du mesh → masque) : 0 côté, 1 dessus, 2 détail (uv conservés pour le plaquage).
    slot_masks (bloc à plusieurs matériaux : un masque par slot FBX) : les triangles du slot k ≥ 1 vont dans slots[k − 1],
    rendus avec le k-ième matériau du bloc ; un slot au-delà de la liste reste sur le matériau principal.
    Retourne (data, bbox, slot du verre ou None)."""
    nrm, uvs = raw['nrm'], raw['uv'] if mask or slot_masks else None
    glass_slot = next((i for i, n in enumerate(raw['mat_names']) if re.match(r'(solid )?glass', n.lower())), glass_slot)
    verts, lookup, idx, glass = [], {}, [], []
    slots = [[] for _ in range(len(slot_masks or []) - 1)]
    k = 0
    for pi, poly in enumerate(raw['polys']):
        s = raw['mat'][pi]
        out = glass if s == glass_slot else slots[s - 1] if 1 <= s <= len(slots) else idx
        if slot_masks: mask = slot_masks[s] if s < len(slot_masks) else slot_masks[0]
        corners = []
        for v in poly:
            p = apply(m, tuple(c * raw['unit_scale'] / UNITS_PER_BLOCK for c in raw['pos'][v]))   # fichiers en cm (×1) ou en m (×100)
            n = apply(m, nrm[k]) if nrm else None
            uv = uvs[k] if uvs else None
            chan = mask(uv) if uv and mask else 0
            k += 1
            corners.append((p, n, chan, uv if chan == 2 else (0.0, 0.0)))
        if not nrm:
            a, b, c = (corners[i][0] for i in range(3))
            u = tuple(b[i] - a[i] for i in range(3)); w = tuple(c[i] - a[i] for i in range(3))
            n = (u[1] * w[2] - u[2] * w[1], u[2] * w[0] - u[0] * w[2], u[0] * w[1] - u[1] * w[0])
            corners = [(p, n, ch, uv) for p, _, ch, uv in corners]
        ids = []
        for p, n, chan, uv in corners:
            length = math.sqrt(sum(c * c for c in n)) or 1.0
            key = tuple(round(c, 4) for c in p) + tuple(round(c / length, 3) for c in n) + (chan,) + tuple(round(c, 4) for c in uv)
            if key not in lookup:
                lookup[key] = len(verts); verts.append(key)
            ids.append(lookup[key])
        for i in range(1, len(ids) - 1): out.extend((ids[0], ids[i], ids[i + 1]))
    pos = [c for v in verts for c in v[:3]]
    nrm_out = [c for v in verts for c in v[3:6]]
    lo = [min(pos[i::3]) for i in range(3)]; hi = [max(pos[i::3]) for i in range(3)]
    data = {'pos': pos, 'nrm': nrm_out, 'idx': idx}
    if glass: data['glass'] = glass
    if any(slots): data['slots'] = slots
    if any(v[6] for v in verts):
        data['chan'] = [v[6] for v in verts]
        data['uv'] = [c for v in verts for c in v[7:9]]
    return data, (lo, hi), glass_slot if glass else None


def mat_color(text, prop):
    """Couleur [r, g, b, a] d'une propriété d'un .mat (texte), blanc si absente."""
    found = re.search(r'- %s:\s*\{r:\s*([\d.eE+-]+),\s*g:\s*([\d.eE+-]+),\s*b:\s*([\d.eE+-]+),\s*a:\s*([\d.eE+-]+)\}' % prop, text)
    return [float(v) for v in found.groups()] if found else [1.0, 1.0, 1.0, 1.0]


def glass_tint(index, block, slot, notes):
    """Teinte [r, g, b, a] (0..1) du matériau du bloc au slot du verre (0 = Material, i = Materials[i−1]). Shader du verre
    (Triplanar Obscure Glass, TRIPLANAR_FADE) : rgb = moyenne de la texture du canal dominant de son _MaskTex (rouge
    _TopTex, vert _SideTex, bleu _DetailTex) × teinte de ce canal, alpha = _Color.a. Autre shader (Standard transparent) :
    _Color × moyenne de la _MainTex. Repli sur le verre de tier 1 du jeu (noir, 26 % d'opacité) si le matériau n'est
    pas résolu."""
    default = [0.0, 0.0, 0.0, 0.26]
    subs = block.get('Materials') or []
    ref = block.get('Material') if slot == 0 else (subs[slot - 1] if 0 < slot <= len(subs) else None)
    mat_path = index.get((ref or {}).get('guid'))
    if not mat_path:
        notes.append('glass: material slot %d of %s not resolved, default tint' % (slot, block['Name'])); return default
    from PIL import Image
    text = open(mat_path, encoding='utf-8', errors='replace').read()
    def color_of(prop): return mat_color(text, prop)
    def mean_of(prop):
        path = index.get(unity_yaml.material_texture_guid(mat_path, [prop])[1])
        if not path: notes.append('glass: %s of %s not resolved, colour only' % (prop, os.path.basename(mat_path))); return [1.0] * 4
        return [c / 255.0 for c in Image.open(path).convert('RGBA').resize((1, 1), Image.BOX).getpixel((0, 0))]
    color = color_of('_Color')
    if is_glass(mat_path):
        mask = mean_of('_MaskTex')
        prop, tint = max(zip(mask[:3], [('_TopTex', '_TopTint'), ('_SideTex', '_SideTint'), ('_DetailTex', '_DetailTint')]))[1]
        rgb = [a * b for a, b in zip(mean_of(prop), color_of(tint))]
        return [round(c, 3) for c in rgb[:3]] + [round(color[3], 3)]
    mean = mean_of('_MainTex')
    return [round(color[i] * mean[i], 3) for i in range(4)]


# --- tables de sélection ---------------------------------------------------------------------------------------------

def compile_cases(cases, unsupported):
    """Cas d'usage → liste ordonnée de {mesh_guid, file_id, rot_unity, conds: [(offset_unity, attendu, prédicat)], dead}.

    Reproduit MeshUsageCase.Evaluate : le cas de base puis, si applyConditionsToAllRotations, ses variantes tournées
    de 90/180/270 autour de Y (offsets et mesh tournés ensemble) ; le rendu prend le premier cas dont toutes les
    conditions sont vraies. Attendu 1 = « le voisin satisfait le prédicat » (catégorie == ruleString, ou même type de
    bloc). Un cas portant une règle non supportée ne peut jamais être satisfait : il est marqué dead (omis).
    """
    compiled = []
    for case in cases:
        rotations = [0, 90, 180, 270] if case['all_rotations'] else [0]
        for extra in rotations:
            conds, dead = [], False
            for offset, rules in case['conditions']:
                off = rot_y(extra, offset)
                for rule_type, rule_string in rules:
                    # Catégorie vide (FenceEnd FarEast) : aucun bloc n'en a, « différent » est toujours vrai, « égal » jamais.
                    if rule_type == RULE_NOT_EQUALS_CATEGORY and not rule_string: continue
                    if rule_type == RULE_NOT_EQUALS_TYPE and rule_string == 'WorldObject': continue
                    if rule_type == RULE_EQUALS_CATEGORY and not rule_string:
                        unsupported.add(('category:', '')); dead = True; continue
                    if rule_type in (RULE_EQUALS_CATEGORY, RULE_NOT_EQUALS_CATEGORY): this = 'category:' + rule_string
                    elif rule_type in (RULE_EQUALS_THIS, RULE_NOT_EQUALS_THIS): this = 'sameType'
                    elif rule_type in (RULE_IS_SOLID, RULE_NOT_SOLID): this = 'solid'
                    else: this = RULE_NAMES[rule_type] if rule_type is not None and rule_type < len(RULE_NAMES) else str(rule_type)
                    if this not in SUPPORTED_PREDICATES and not this.startswith('category:'):
                        unsupported.add((this, rule_string)); dead = True; continue
                    conds.append((off, int(rule_type in (RULE_EQUALS_CATEGORY, RULE_EQUALS_THIS, RULE_IS_SOLID)), this))
            turn = 0 if case['dont_rotate'] else extra
            compiled.append({'mesh_guid': case['mesh_guid'], 'file_id': case['mesh_file_id'], 'flip': case['flip'],
                             'rot_unity': (case['import_rot'] + turn) % 360,
                             'decorative': case['decorative'], 'parts': [],
                             'conds': conds, 'dead': dead})
    return compiled


# --- extraction ------------------------------------------------------------------------------------------------------

def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument('--content', required=True, help='dossier Eco/Content/Art')
    ap.add_argument('--set', required=True, help='dossier du set sous Player Built Blocks/Full Building Types, ex. "Mortared Stone"')
    ap.add_argument('--out', required=True, help='dossier de sortie (assets/forms)')
    ap.add_argument('--axis', default=DEFAULT_AXIS, help='axes Unity pour x,y,z planner, signes autorisés (défaut %s)' % DEFAULT_AXIS)
    ap.add_argument('--base-offset', action='append', default=[], metavar='Form=deg', help='décalage de rotation (Unity, deg) par forme explicite')
    ap.add_argument('--skin', help="préfixe d'une seule skin, qui donne son nom au bundle (Composite Lumber : un bundle par bois)")
    args = ap.parse_args()

    started = time.time()
    m = parse_axis(args.axis)
    base_offset = dict(BASE_OFFSET)
    for spec in args.base_offset:
        form, _, deg = spec.partition('=')
        base_offset[form.strip()] = float(deg)
    rot_sign = 1 if planner_angle(m, 90) == 90 else -1

    blocks_root = os.path.join(args.content, 'Blocks')
    set_dir = os.path.join(blocks_root, 'Player Built Blocks', 'Full Building Types', args.set)
    if not os.path.isdir(set_dir): set_dir = os.path.join(blocks_root, 'Player Built Blocks', args.set)   # verre, tapis
    road = not os.path.isdir(set_dir)
    if road: set_dir = os.path.join(blocks_root, 'Player Built Blocks', 'Roads Types', args.set)   # routes
    if not os.path.isdir(set_dir): sys.exit('set folder not found: %s' % set_dir)
    set_id = args.skin or args.set.replace(' ', '')
    os.makedirs(args.out, exist_ok=True)

    log('== %s → %s' % (args.set, set_id))
    log('axis %s  (unityToPlanner %s, rotSign %d, baseOffsets %s)' % (args.axis, m, rot_sign, base_offset or '{}'))

    index = unity_yaml.build_guid_index(blocks_root)
    index.update(unity_yaml.build_guid_index(os.path.join(args.content, 'Player Built Objects', 'Glass Materials')))   # verre des fenêtres
    index.update(unity_yaml.build_guid_index(os.path.join(args.content, 'Player Built Objects', 'Player Housing', 'Furniture')))   # bois des piles de verre
    index.update(unity_yaml.build_guid_index(os.path.join(args.content, 'Player Built Objects', 'Building Additions', 'Fireplace')))   # cheminées Ashlar
    log('guid index: %d assets' % len(index))

    # Builders du set (guid → chemin), sous-dossiers compris : les blocksets dont ils font la majorité des blocs sont les
    # skins (le béton, qui emprunte des builders au Composite Lumber, n'en est pas une). Un builder absent du checkout (piles
    # du gravier) ne dit rien : son bloc compte. Blocksets de même préfixe fusionnés (Ashlar « Pediment », Composite « Ramp » :
    # rampes, pentes, toits de la même pierre ou du même bois).
    tree = os.path.normpath(set_dir)
    builder_guids = {guid for guid, path in index.items() if path.lower().endswith('.asset') and os.path.normpath(path).startswith(tree + os.sep)}
    blocksets = []
    # Routes : leurs blocksets sont rangés à part (Road Blocks), pris pour elles seules (les rampes en brique restent un ajout
    # de Brick, EXTRA_BLOCKSETS).
    for path in sorted(glob.glob(os.path.join(blocks_root, 'Blocksets', 'Road Blocks' if road else 'Player Building Blocks', '*.asset'))):
        bs = load_blockset(path)
        own = [b for b in bs['blocks'] if b['Builder']['guid'] in builder_guids or b['Builder']['guid'] not in index]
        if not any(b['Builder']['guid'] in builder_guids for b in own) or 2 * len(own) <= len(bs['blocks']): continue
        # Préfixe sans les blocs dont le builder est dans un autre dossier (TransparentBlock du verre, builder d'Ashlar) ; préfixes
        # imposés : le blockset est découpé en une skin par préfixe.
        fixed = SKIN_PREFIXES.get(args.set)
        parts = [dict(bs, prefix=p, blocks=[b for b in bs['blocks'] if b['Name'].startswith(p)]) for p in fixed] if fixed else [dict(bs, prefix=common_prefix([b['Name'] for b in own]))]
        for part in parts:
            if not part['blocks'] or part['prefix'] in SKIN_EXCLUDED.get(args.set, []) or args.skin and part['prefix'] != args.skin: continue
            same = next((o for o in blocksets if o['prefix'] == part['prefix']), None)
            if same: same['blocks'] += part['blocks']; same['name'] += ' + ' + part['name']
            else: blocksets.append(part)
    if not blocksets: sys.exit('no blockset references builders in %s' % set_dir)
    reference = blocksets[0]
    for extra in EXTRA_BLOCKSETS.get(args.set, []):   # rampes en brique : blocs « BrickRampA… », même préfixe que la skin
        reference['blocks'] += [b for b in load_blockset(os.path.join(blocks_root, 'Blocksets', extra))['blocks'] if b['Name'].startswith(reference['prefix'])]
    log('blocksets (skins): %s' % ', '.join('%s [%s]' % (bs['name'], bs['prefix']) for bs in blocksets))
    for bs in blocksets[1:]:
        a = {form_name(b['Name'], reference['prefix']): b['Builder']['guid'] for b in reference['blocks']}
        b_ = {form_name(b['Name'], bs['prefix']): b['Builder']['guid'] for b in bs['blocks']}
        diff = [k for k in a if k in b_ and a[k] != b_[k]] + [k for k in a if k not in b_]
        if diff: log('  ! %s differs from %s on %s' % (bs['name'], reference['name'], diff))

    # Blocs du blockset de référence → formes.
    unsupported = set()
    meshes, mesh_by_path, form_out, categories, notes = {}, {}, {}, {}, []
    builders = {}
    ignored = []
    mesh_glass_slot = {}                                   # nom du mesh → slot FBX du verre (meshes de fenêtre)
    glass_block = None                                     # (bloc, slot) du premier bloc dont un matériau est le verre

    # Masque de texture : le _MaskTex du matériau du bloc (BuildingMask.tga partagé, ou propre au matériau : murs à
    # colombages FarEast), sinon le fichier commun. Un même FBX sous deux masques donne deux meshes.
    default_mask = os.path.join(blocks_root, 'Player Built Blocks', 'Full Building Types', 'BuildingMask.tga')
    masks = {}
    def mask_of(mat_path):
        path = index.get(unity_yaml.material_texture_guid(mat_path, ['_MaskTex'])[1]) if mat_path else None
        path = path or default_mask
        if path not in masks:
            masks[path] = load_mask(path) if os.path.exists(path) else None
            log('texture mask: %s' % (os.path.relpath(path, blocks_root) if masks[path] else 'none (all side)'))
        return path
    # Miroir X de l'import FBX d'Unity (main droite → main gauche), tous sets : sans lui, les meshes contextuels chiraux
    # s'ouvrent du mauvais côté (dalles FarEast 24/316 ; murs Hewn, Mortared, Brick, Lumber ; coins et avant-toits du
    # toit intelligent dans le voisin plein). Pans de toit et escaliers n'en changent pas, les coins tournent d'un quart.
    log('meshes mirrored on X (Unity FBX import)')

    # Bloc en cours : canal de texture de chacun de ses matériaux (Material puis Materials[]) = 'uv' (shader standard : tout
    # en détail, aux UV) ou le chemin de son masque ; plusieurs matériaux → un masque par slot FBX (murs FarEast).
    # glass = slot dont le matériau a le shader du verre (tous les triangles des vitres du set Glass), None sinon.
    current = {'chans': [mask_of(None)], 'glass': None}
    def chan_fn(chan): return (lambda uv: 2) if chan == 'uv' else masks[chan]
    decor_parts = set()                                    # meshes de décor seuls (fondus dans les meshes composés)
    def mesh_key(guid, file_id, flip=(0, 0), parts=()):
        # parts : meshes de décor (guid, fileID, flip, quart de tour planner par rapport au mesh de base) fondus dans ce mesh.
        if parts:
            base = mesh_key(guid, file_id, flip)
            names = [mesh_key(g, f, fl) for g, f, fl, _ in parts]
            if base is None or None in names: return None
            key = (base,) + tuple((n, p[3]) for n, p in zip(names, parts))
            if key not in mesh_by_path:
                decor_parts.update(names)
                data = {k: list(v) for k, v in meshes[base].items()}
                if any(k in data for k in ('glass', 'slots')) or any(k in meshes[n] for n in names for k in ('glass', 'slots')): sys.exit('decorative mesh with glass or slots not supported')
                for n, (_, _, _, deg) in zip(names, parts):
                    part, nb = meshes[n], len(data['pos']) // 3
                    c, s = {0: (1, 0), 90: (0, 1), 180: (-1, 0), 270: (0, -1)}[deg % 360]
                    for k in ('pos', 'nrm'):
                        v = part[k]
                        data[k] += [c2 for i in range(0, len(v), 3) for c2 in (v[i] * c - v[i + 1] * s, v[i] * s + v[i + 1] * c, v[i + 2])]
                    data['idx'] += [i + nb for i in part['idx']]
                    if 'chan' in data or 'chan' in part:
                        data['chan'] = data.get('chan', [0] * nb) + part.get('chan', [0] * (len(part['pos']) // 3))
                        data['uv'] = data.get('uv', [0.0] * 2 * nb) + part.get('uv', [0.0] * 2 * (len(part['pos']) // 3))
                name = base + ''.join('+' + n for n in names)
                meshes[name] = data
                mesh_by_path[key] = name
            return mesh_by_path[key]
        path = resolve_mesh_path(index, guid, file_id)
        if path is None:
            notes.append('mesh not resolved for guid %s (%s)' % (guid, index.get(guid)))
            return None
        if any(a % 90 for a in flip):
            notes.append('%s: importRotation x/z %s not supported, ignored' % (os.path.basename(path), flip)); flip = (0, 0)
        key = (path, current['glass'], flip) + tuple(current['chans'])
        if key not in mesh_by_path:
            raw = fbx.load_mesh(path)
            # Transform « geometric » du nœud (rotation −90° X des barrières de route), cuit par Unity dans le mesh.
            gt, gr, _ = fbx.geometric_transform(path, raw['name'])
            if any(gt) or any(gr):
                q, pv = fbx._euler_quat(gr), raw['pivot']
                raw['pos'] = [tuple(c + gt[i] - pv[i] for i, c in enumerate(fbx._qrot(q, tuple(p[i] + pv[i] for i in range(3))))) for p in raw['pos']]
                if raw.get('nrm'): raw['nrm'] = [fbx._qrot(q, n) for n in raw['nrm']]
            # Miroir X de l'import, puis z puis x de importRotation (Euler d'Unity : Z, X, Y ; y est la rotation de la forme).
            (cx, sx), (cz, sz) = ((round(math.cos(math.radians(a))), round(math.sin(math.radians(a)))) for a in flip)
            def orient(v):
                x, y, z = -v[0], v[1], v[2]
                x, y = x * cz - y * sz, x * sz + y * cz
                return (x, y * cx - z * sx, y * sx + z * cx)
            raw['pos'] = [orient(p) for p in raw['pos']]
            if raw.get('nrm'): raw['nrm'] = [orient(n) for n in raw['nrm']]
            name = os.path.splitext(os.path.basename(path))[0] + ''.join('_%s%d' % (a, f) for a, f in zip('XZ', flip) if f)
            if name in meshes: name = name + '_' + str(len(meshes))
            chans = current['chans']
            data, (lo, hi), glass_slot = convert_mesh(raw, m, chan_fn(chans[0]), [chan_fn(c) for c in chans] if len(chans) > 1 else None, current['glass'])
            for tris in [data['idx'], data.get('glass', [])] + data.get('slots', []):   # le miroir retourne les faces
                for t in range(0, len(tris), 3): tris[t + 1], tris[t + 2] = tris[t + 2], tris[t + 1]
            meshes[name] = data
            mesh_by_path[key] = name
            if glass_slot is not None: mesh_glass_slot[name] = glass_slot
            outside = max(max(abs(c) for c in lo), max(abs(c) for c in hi)) - 0.5
            log('  mesh %-22s %4d verts %4d polys → %4d verts %4d tris  bbox %s..%s%s' % (
                name, len(raw['pos']), len(raw['polys']), len(data['pos']) // 3, len(data['idx']) // 3,
                ['%.3f' % c for c in lo], ['%.3f' % c for c in hi], '  ! exceeds cell by %.3f' % outside if outside > 1e-3 else ''))
        return mesh_by_path[key]

    by_form = {}
    for block in reference['blocks']:
        if not block['Name'].startswith(reference['prefix']): continue   # TransparentBlock du verre : pas une forme du set
        form, r = form_name(block['Name'], reference['prefix'])
        by_form.setdefault(form, {})[r] = block
    def contextual(form, builder, label):
        """Cas compilés d'un builder → {bitMeans?, cases} ; journalise sous `label`."""
        compiled = compile_cases(builder['cases'], unsupported)
        if args.set in DECORATIVE_SETS: compiled = decorate(compiled, index, unsupported)
        live = [c for c in compiled if not c['dead']]
        meanings = sorted({pred for c in live for _, _, pred in c['conds']})
        single = meanings[0] if len(meanings) == 1 else None
        out_cases = []
        for c in live:
            name = mesh_key(c['mesh_guid'], c['file_id'], c['flip'], tuple((g, f, fl, planner_angle(m, (r - c['rot_unity']) % 360)) for g, f, fl, r in c['parts']))
            if name is None: continue
            conds = [list(apply(m, off)) + [expected] + ([] if single else [pred]) for off, expected, pred in c['conds']]
            out_cases.append({'mesh': name, 'rot': planner_angle(m, (c['rot_unity'] + base_offset.get(form, 0)) % 360), 'conds': conds})
        if not any(not c['conds'] for c in live): notes.append('%s: no unconditional fallback case' % label)
        offsets = {tuple(off) for c in live for off, _, _ in c['conds']}
        log('  form %-10s %d cases (%d with rotations) -> %d emitted, %d omitted (unsupported rules), %d offsets, bit = %s' % (
            label, len(builder['cases']), len(compiled), len(out_cases), len(compiled) - len(live), len(offsets), single or 'per cond'))
        log('      meshes used: %s' % ', '.join(sorted({'%s@%d' % (c['mesh'], c['rot']) for c in out_cases})))
        return {'bitMeans': single, 'cases': out_cases} if single else {'cases': out_cases}

    for form, variants in by_form.items():
        if form not in EXPLICIT_FORMS + CONTEXT_FORMS + DOCK_FORMS + FAREAST_FORMS + SET_FORMS.get(args.set, []) or form in SET_EXCLUDED.get(args.set, []):
            ignored.append(form); continue
        block0 = variants[0] if 0 in variants else next(iter(variants.values()))
        block_mats = [index.get((ref or {}).get('guid')) for ref in [block0.get('Material')] + (block0.get('Materials') or [])]
        current['chans'] = ['uv' if is_uv_mapped(p) else mask_of(p) for p in block_mats]
        current['glass'] = next((k for k, p in enumerate(block_mats) if is_glass(p)), None)
        if current['glass'] is not None and glass_block is None: glass_block = (block0, current['glass'])
        # Catégorie par forme, ou par rotation quand les 4 blocs tournés diffèrent (quais : DocksPlatform1/2).
        cats = [(variants[r].get('Category') if r in variants else None) or 'Default' for r in range(4)]
        categories[form] = cats[0] if len(variants) < 4 or len(set(cats)) == 1 else cats
        if block0['Builder']['guid'] not in index: notes.append('%s: builder not in the checkout' % form); continue
        builder = load_builder(index[block0['Builder']['guid']])
        builders[form] = builder
        if not builder['cases']: notes.append('%s: builder without usage case' % form); continue
        vbuilders = {r: load_builder(index[variants[r]['Builder']['guid']]) for r in sorted(variants)}
        explicit = all(not c['conditions'] and not (args.set in DECORATIVE_SETS and c['decorative']) for vb in vbuilders.values() for c in vb['cases'])
        # Forme explicite (liste indexée par la rotation de l'op) : 4 blocs tournés sans condition (le joueur oriente
        # lui-même, fenêtre Brick comprise), ou forme fixe sans condition. Un bloc unique à conditions (cube du gravier :
        # bords, coins, plein selon les voisins) est contextuel, comme une dalle. 4 blocs tournés à conditions (quais) :
        # un jeu de cas par rotation (« rots »).
        if len(variants) == 4 and explicit or (form in EXPLICIT_FORMS and explicit):
            entries = []
            base_rot = builder['cases'][0]['import_rot']
            for r in sorted(variants):
                case = vbuilders[r]['cases'][0]
                if (case['import_rot'] - base_rot - 90 * r) % 360 != 0:
                    notes.append('%s rot %d: importRotation %g ≠ base %g + 90·%d' % (form, r, case['import_rot'], base_rot, r))
                name = mesh_key(case['mesh_guid'], case['mesh_file_id'], case['flip'])
                if name is None: continue
                unity = (case['import_rot'] + base_offset.get(form, 0)) % 360
                entries.append({'mesh': name, 'rot': planner_angle(m, unity)})
            form_out[form] = entries
            log('  form %-10s %s' % (form, ', '.join('r%d→%s@%d' % (i, e['mesh'], e['rot']) for i, e in enumerate(entries))))
        elif len(variants) == 4:
            form_out[form] = {'rots': [contextual(form, vbuilders[r], '%s r%d' % (form, r)) for r in range(4)]}
        else:
            form_out[form] = contextual(form, builder, form)

    # Slot FBX du verre du mesh de fenêtre (None : pas de vitre).
    window_form = form_out.get('WindowGrilles')
    if isinstance(window_form, dict):
        window_meshes = [c['mesh'] for r in window_form.get('rots', [window_form]) for c in r['cases']]
    else:
        window_meshes = [e['mesh'] for e in window_form or []]
    glass_slot_of_window = next((mesh_glass_slot[n] for n in window_meshes if n in mesh_glass_slot), None)

    # Matériaux : un par fichier .mat rencontré sur les blocs des skins (murs, toits…) → textures côté / dessus / détail
    # (tuiles d'atlas dédoublonnées) + échelle et décalage du triplanaire (Triplanar.cginc : uv = position × _TextureScale
    # + _TextureOffset). skins[item] = matériau du bloc Wall ; formMaterials[item][forme] = matériau d'une forme qui en diffère.
    skins, form_materials, slot_materials, materials, texture_paths = {}, {}, {}, [], []
    mat_index, tile_index = {}, {}
    def tile_of_path(path, tint=None, metal=None):
        key = (path, tint, metal) if tint or metal else path
        if key not in tile_index: tile_index[key] = len(texture_paths); texture_paths.append(key)
        return tile_index[key]
    metal_max = {}
    def metal_of(mat_path, prop):
        # Map de métallicité du canal (shader métallique) : (chemin, facteur), None si absente ou noire (bois, pierre).
        path = index.get(unity_yaml.material_texture_guid(mat_path, [prop])[1]) if prop and shader_guid(mat_path) in METAL_SHADERS else None
        if not path: return None
        if path not in metal_max:
            from PIL import Image
            metal_max[path] = Image.open(path).convert('RGB').getchannel('R').getextrema()[1]
        return (path, METAL_DARKEN) if metal_max[path] else None
    def tile_of(mat_path, props, tint_prop=None, metal_prop=None):
        # Première propriété dont la texture est résolue (certaines pointent hors de Content/Art, ex. _SideTex du gravier).
        path = next((index[g] for g in (unity_yaml.material_texture_guid(mat_path, [p])[1] for p in props) if g in index), None)
        # Teinte du canal cuite dans la tuile (acier ondulé) ; blanche : tuile partagée, inchangée. Verre : teinte à part (glass_tint).
        tint = tuple(mat_color(open(mat_path, encoding='utf-8', errors='replace').read(), tint_prop)[:3]) if tint_prop and not is_glass(mat_path) else None
        return tile_of_path(path, tint if tint and tint != (1.0, 1.0, 1.0) else None, metal_of(mat_path, metal_prop)) if path else None
    def material_of(mat_path):
        if mat_path in mat_index: return mat_index[mat_path]
        if is_uv_mapped(mat_path):
            main = tile_of(mat_path, ['_MainTex'])
            if main is None: return None
            mat_index[mat_path] = len(materials)
            materials.append({'side': main, 'top': None, 'detail': main, 'scale': 1.0, 'offset': 0.0})
            log('  material %d %-36s uv-mapped _MainTex tile %s' % (mat_index[mat_path], os.path.basename(mat_path), main))
            return mat_index[mat_path]
        side = tile_of(mat_path, ['_SideTex', '_MainTex', '_SideTexture'], '_SideTint', '_SideMetalness')
        if side is None: return None
        top, detail = tile_of(mat_path, ['_TopTex', '_TopTexture'], '_TopTint', '_TopMetalness'), tile_of(mat_path, ['_DetailTex'], '_DetailTint', '_DetailMetalness')
        if detail is None and args.set in DETAIL_FALLBACK:
            detail = tile_of_path(os.path.join(blocks_root, DETAIL_FALLBACK[args.set])); notes.append('%s: detail texture missing from Content/Art, using %s' % (os.path.basename(mat_path), os.path.basename(DETAIL_FALLBACK[args.set])))
        text = open(mat_path, encoding='utf-8', errors='replace').read()
        floats = {k: float(re.search(r'- %s:\s*([\d.eE+-]+)' % k, text).group(1)) if re.search(r'- %s:\s*([\d.eE+-]+)' % k, text) else d for k, d in (('_TextureScale', 1.0), ('_TextureOffset', 0.0))}
        mat_index[mat_path] = len(materials)
        materials.append({'side': side, 'top': None if top == side else top, 'detail': detail, 'scale': floats['_TextureScale'], 'offset': floats['_TextureOffset']})
        log('  material %d %-36s side %s top %s detail %s scale %s offset %s' % (mat_index[mat_path], os.path.basename(mat_path), side, top, detail, floats['_TextureScale'], floats['_TextureOffset']))
        return mat_index[mat_path]
    for bs in blocksets:
        item = bs['prefix'] + 'Item'
        # Matériau de la skin (formes sans surcharge, formes absentes rendues en cube) : celui du mur, sinon du cube (FarEast).
        by_name = {form_name(b['Name'], bs['prefix'])[0]: b for b in reversed(bs['blocks'])}
        block = by_name.get('Wall') or by_name.get('Cube') or bs['blocks'][0]
        # Premier matériau non-verre du bloc (mur du verre encadré : le verre en Material, l'acier en Materials[0]).
        mat_path = next((p for p in (index.get((ref or {}).get('guid')) for ref in [block.get('Material')] + (block.get('Materials') or [])) if p and not is_glass(p)), None)
        default = material_of(mat_path) if mat_path else None
        if default is None:
            notes.append('%s: wall material or texture not resolved' % item); continue
        skins[item] = default
        for b in bs['blocks']:
            form = form_name(b['Name'], bs['prefix'])[0]
            if form not in form_out: continue
            ref = b.get('Material')
            # Fenêtre dont le matériau principal est le verre (slot 0, Lumber) : la partie opaque porte le sous-matériau suivant.
            if form == 'WindowGrilles' and glass_slot_of_window == 0 and (b.get('Materials') or []): ref = b['Materials'][0]
            path = index.get((ref or {}).get('guid'))
            mi = material_of(path) if path else None
            if mi is not None and mi != default: form_materials.setdefault(item, {})[form] = mi
            # Matériaux des slots FBX 1… (Materials[]) des blocs à plusieurs matériaux : meshes découpés en `slots`.
            subs = b.get('Materials') or []
            if len(subs) and form not in slot_materials.get(item, {}) and not (form == 'WindowGrilles' and glass_slot_of_window == 0):
                slot_materials.setdefault(item, {})[form] = [material_of(index[g]) if g in index else None for g in ((r or {}).get('guid') for r in subs)]
        log('  skin %-26s material %d%s' % (item, default, '' if item not in form_materials else '  overrides ' + ', '.join('%s→%d' % kv for kv in sorted(form_materials[item].items()))))
        if item in slot_materials: log('      slot materials: %s' % ', '.join('%s→%s' % kv for kv in sorted(slot_materials[item].items())))

    # Verre des fenêtres : teinte du matériau du bloc WindowGrilles (blockset de référence) au slot « glass » de son mesh ;
    # sans fenêtre extraite (set Glass, béton), celle du premier bloc dont un matériau est le verre.
    window = next((b for b in reference['blocks'] if form_name(b['Name'], reference['prefix'])[0] == 'WindowGrilles'), None) if window_form else None
    slot = glass_slot_of_window
    if window is None and glass_block: window, slot = glass_block
    # Slot du mesh qui ne porte pas le verre sur le bloc (fenêtre du verre encadré : l'ordre des sous-matériaux change d'un
    # mesh à l'autre) : slot du bloc dont le matériau a le shader du verre.
    if window is not None and slot is not None:
        mats = [index.get((ref or {}).get('guid')) for ref in [window.get('Material')] + (window.get('Materials') or [])]
        if not (slot < len(mats) and is_glass(mats[slot])): slot = next((k for k, p in enumerate(mats) if is_glass(p)), slot)
    tint_index = dict(index, **unity_yaml.build_guid_index(os.path.join(args.content, 'Avatar', 'Mannequin')))   # masque du verre (PureRedMask)
    glass = glass_tint(tint_index, window, slot, notes) if window and slot is not None else None
    if glass: log('  glass tint %s (%s slot %d)' % (glass, window['Name'], slot))
    elif window: notes.append('WindowGrilles: no glass sub-material in its meshes, drawn opaque')

    # Meshes de décor seuls : fondus dans les meshes composés, retirés s'ils ne servent pas tels quels.
    used = {e['mesh'] for f in form_out.values() for e in (f if isinstance(f, list) else [c for g in f.get('rots', [f]) for c in g['cases']])}
    for n in decor_parts - used: del meshes[n]

    # Sorties.
    bundle = {
        'set': set_id, 'version': BUNDLE_VERSION,
        'calibration': {
            'axis': args.axis, 'unityToPlanner': m, 'rotSign': rot_sign, 'baseOffsets': base_offset, 'mirrorX': True,
            'rotation': 'degrees around +z applied to the centred mesh: x\' = x·cos − y·sin, y\' = x·sin + y·cos',
            'note': 'explicit forms: rot = planner(importRotation.y + 90·r + baseOffset); contextual forms: first case whose conds [dx,dy,dz,expected(,predicate)] all hold wins, a case without conds is the fallback',
        },
        'categories': categories,
        'meshes': meshes,
        'forms': form_out,
        'materials': materials,
        'skins': skins,
        'formMaterials': form_materials,
        'slotMaterials': slot_materials,
    }
    if glass: bundle['glass'] = glass
    atlas, bundle['tile'] = textures.build_atlas(texture_paths) if texture_paths else (None, 512)   # côté d'une tuile de l'atlas (px)
    json_path = os.path.join(args.out, set_id + '.json')
    with open(json_path, 'w', encoding='utf-8') as f:
        json.dump(bundle, f, separators=(',', ':'))
    webp_path = os.path.join(args.out, set_id + '.webp')
    preview_path = os.path.join(args.out, set_id + '.preview.png')
    if atlas is not None:
        textures.save_atlas(atlas, webp_path, preview_path)

    index_path = os.path.join(args.out, 'index.json')
    index_doc = {'sets': {}, 'materials': {}}
    if os.path.exists(index_path):
        with open(index_path, encoding='utf-8') as f: index_doc = json.load(f)
    index_doc.setdefault('sets', {})[set_id] = {'json': set_id + '.json', 'atlas': set_id + '.webp'}
    index_doc.setdefault('materials', {})
    for item in skins: index_doc['materials'][item] = set_id
    with open(index_path, 'w', encoding='utf-8') as f:
        json.dump(index_doc, f, indent=2, sort_keys=True)

    log()
    log('== summary')
    log('meshes: %d, vertices: %d, triangles: %d' % (len(meshes), sum(len(v['pos']) // 3 for v in meshes.values()), sum(len(v['idx']) // 3 for v in meshes.values())))
    log('forms: %s' % ', '.join('%s(%s)' % (k, '%d cases × 4 rots' % sum(len(g['cases']) for g in v['rots']) if isinstance(v, dict) and 'rots' in v
                                              else '%d cases' % len(v['cases']) if isinstance(v, dict) else len(v)) for k, v in form_out.items()))
    log('ignored forms: %s' % ', '.join(ignored))
    log('skins: %s' % ', '.join('%s→%d' % kv for kv in skins.items()))
    log('unsupported rules: %s' % (', '.join('%s %r' % u for u in sorted(unsupported)) or 'none'))
    for n in notes: log('note: ' + n)
    for path in (json_path, webp_path, preview_path, index_path):
        if os.path.exists(path): log('wrote %s (%d KB)' % (path, os.path.getsize(path) // 1024))
    log('done in %.1fs' % (time.time() - started))


if __name__ == '__main__':
    main()
