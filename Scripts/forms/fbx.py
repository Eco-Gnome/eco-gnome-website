"""Lecture des FBX binaires (7.4) d'Eco : nœuds, tableaux zlib, géométrie + normales.

Un fichier contient un ou plusieurs `Model` (le second est en général un « Collider ») reliés à leur `Geometry`
par les `Connections`. Les meshes d'Eco sont décalés en X/Z dans leurs fichiers, avec le `RotationPivot` du modèle
au centre du bloc : Unity recale les sommets sur ce pivot à l'import (le reste du transform va sur le GameObject,
que le jeu ignore), on fait pareil : sommets − pivot.
"""
import struct
import zlib


def _read_prop(f):
    t = f.read(1).decode()
    if t == 'Y': return struct.unpack('<h', f.read(2))[0]
    if t == 'C': return struct.unpack('<b', f.read(1))[0] != 0
    if t == 'I': return struct.unpack('<i', f.read(4))[0]
    if t == 'F': return struct.unpack('<f', f.read(4))[0]
    if t == 'D': return struct.unpack('<d', f.read(8))[0]
    if t == 'L': return struct.unpack('<q', f.read(8))[0]
    if t in 'SR':
        n = struct.unpack('<I', f.read(4))[0]
        d = f.read(n)
        return d.decode('utf-8', 'replace') if t == 'S' else d
    if t in 'fdlicb':
        cnt, enc, clen = struct.unpack('<III', f.read(12))
        raw = f.read(clen)
        if enc == 1: raw = zlib.decompress(raw)
        fmt = {'f': 'f', 'd': 'd', 'l': 'q', 'i': 'i', 'c': 'B', 'b': 'B'}[t]
        return list(struct.unpack('<%d%s' % (cnt, fmt), raw))
    raise ValueError('prop type %r' % t)


def _read_node(f, ver):
    if ver >= 7500:
        end, nprops, _ = struct.unpack('<QQQ', f.read(24))
    else:
        end, nprops, _ = struct.unpack('<III', f.read(12))
    nlen = struct.unpack('<B', f.read(1))[0]
    if end == 0: return None
    name = f.read(nlen).decode()
    props = [_read_prop(f) for _ in range(nprops)]
    children = []
    while f.tell() < end:
        c = _read_node(f, ver)
        if c is None: break
        children.append(c)
    f.seek(end)
    return {'name': name, 'props': props, 'children': children}


def parse(path):
    with open(path, 'rb') as f:
        assert f.read(21) == b'Kaydara FBX Binary  \x00', path
        f.read(2)
        ver = struct.unpack('<I', f.read(4))[0]
        nodes = []
        while True:
            n = _read_node(f, ver)
            if n is None: break
            nodes.append(n)
        return ver, nodes


def find(nodes, name):
    for n in nodes:
        if n['name'] == name: yield n
        yield from find(n['children'], name)


def _first(nodes, name):
    return next(find(nodes, name), None)


def _node_name(node):
    return node['props'][1].split('\x00')[0]


def _pivot(model):
    for p in find(model['children'], 'P'):
        if p['props'][0] == 'RotationPivot': return tuple(float(v) for v in p['props'][4:7])
    return (0.0, 0.0, 0.0)


def _polygons(index_list):
    polys, cur = [], []
    for i in index_list:
        if i < 0: cur.append(-i - 1); polys.append(cur); cur = []
        else: cur.append(i)
    return polys


def _normals(geo, polys):
    """Normale par sommet de polygone, aplatie dans l'ordre des polygones ; None si absente."""
    layer = _first(geo['children'], 'LayerElementNormal')
    if layer is None: return None
    mapping = _first(layer['children'], 'MappingInformationType')['props'][0]
    reference = _first(layer['children'], 'ReferenceInformationType')['props'][0]
    values = _first(layer['children'], 'Normals')['props'][0]
    vecs = [tuple(values[i:i + 3]) for i in range(0, len(values), 3)]
    if reference == 'IndexToDirect':
        idx = _first(layer['children'], 'NormalsIndex')['props'][0]
        vecs = [vecs[i] for i in idx]
    if mapping == 'ByPolygonVertex': return vecs
    if mapping == 'ByVertice': return [vecs[v] for poly in polys for v in poly]
    if mapping == 'ByPolygon': return [vecs[p] for p, poly in enumerate(polys) for _ in poly]
    if mapping == 'AllSame': return [vecs[0] for poly in polys for _ in poly]
    raise ValueError('normal mapping %s' % mapping)


def _uvs(geo, polys, index=0):
    """UV de la couche `index` (0 = texture, 1 = 2ᵉ couche) par sommet de polygone, aplatis dans l'ordre des polygones ;
    None si absents."""
    layer = next((l for l in geo['children'] if l['name'] == 'LayerElementUV' and l['props'] and l['props'][0] == index), None)
    if layer is None: return None
    mapping = _first(layer['children'], 'MappingInformationType')['props'][0]
    reference = _first(layer['children'], 'ReferenceInformationType')['props'][0]
    values = _first(layer['children'], 'UV')['props'][0]
    uvs = [tuple(values[i:i + 2]) for i in range(0, len(values), 2)]
    if reference == 'IndexToDirect':
        idx = _first(layer['children'], 'UVIndex')['props'][0]
        uvs = [uvs[i] for i in idx]
    if mapping == 'ByPolygonVertex': return uvs
    if mapping == 'ByVertice': return [uvs[v] for poly in polys for v in poly]
    return None


def _alphas(geo, polys):
    """Alpha de la couleur de sommet (première couche) par sommet de polygone ; None si absente."""
    layer = _first(geo['children'], 'LayerElementColor')
    if layer is None: return None
    mapping = _first(layer['children'], 'MappingInformationType')['props'][0]
    reference = _first(layer['children'], 'ReferenceInformationType')['props'][0]
    values = _first(layer['children'], 'Colors')['props'][0]
    alphas = values[3::4]
    if reference == 'IndexToDirect':
        alphas = [alphas[i] for i in _first(layer['children'], 'ColorIndex')['props'][0]]
    if mapping == 'ByPolygonVertex': return alphas
    if mapping == 'ByVertice': return [alphas[v] for poly in polys for v in poly]
    return None


def _linked_models(nodes):
    """[(geometry, model, noms des matériaux dans l'ordre des slots)] dans l'ordre du fichier."""
    geos = {g['props'][0]: g for g in find(nodes, 'Geometry')}
    models = {m['props'][0]: m for m in find(nodes, 'Model')}
    materials = {m['props'][0]: _node_name(m) for m in find(nodes, 'Material')}
    links, model_materials = {}, {}                        # geometry id → model id ; model id → noms des matériaux (ordre des slots)
    for conn in find(nodes, 'Connections'):
        for c in conn['children']:
            if len(c['props']) >= 3 and c['props'][1] in geos and c['props'][2] in models: links[c['props'][1]] = c['props'][2]
            if len(c['props']) >= 3 and c['props'][1] in materials and c['props'][2] in models: model_materials.setdefault(c['props'][2], []).append(materials[c['props'][1]])
    return [(geos[gid], models[mid], model_materials.get(mid, [])) for gid, mid in links.items()]


def _slot_order(geo, n_materials):
    """Indices de matériau FBX dans l'ordre des sous-meshes Unity : ordre de première apparition dans les polygones (bouée :
    polygones du flotteur, index 1, d'abord → slot 0 du renderer), puis les index jamais utilisés."""
    layer = _first(geo['children'], 'LayerElementMaterial')
    values = _first(layer['children'], 'Materials')['props'][0] if layer is not None else []
    order = []
    for v in values:
        if v not in order: order.append(v)
    return order + [i for i in range(n_materials) if i not in order]


def models(path):
    """[(nom du modèle, noms des matériaux par slot Unity)] des modèles maillés qui ne sont pas des colliders."""
    _, nodes = parse(path)
    out = []
    for geo, model, mats in _linked_models(nodes):
        if 'collider' in _node_name(model).lower(): continue
        out.append((_node_name(model), [mats[i] for i in _slot_order(geo, len(mats)) if i < len(mats)]))
    return out


def take_pose(path, take):
    """Première image de l'animation `take` (AnimationStack, nom sans le suffixe « AnimStack ») : {nœud: {'Lcl Translation' |
    'Lcl Rotation' | 'Lcl Scaling': (x, y, z)}}, valeur de la première clé de chaque courbe, repère du fichier."""
    _, nodes = parse(path)
    models = {m['props'][0]: _node_name(m) for m in find(nodes, 'Model')}
    cnodes = {c['props'][0] for c in find(nodes, 'AnimationCurveNode')}
    curves = {c['props'][0]: c for c in find(nodes, 'AnimationCurve')}
    conns = [c['props'] for conn in find(nodes, 'Connections') for c in conn['children'] if len(c['props']) >= 3]
    stack = next((st['props'][0] for st in find(nodes, 'AnimationStack') if st['props'][1].split('\x00')[0] == take), None)
    layers = {c[1] for c in conns if c[2] == stack}
    out = {}
    for c in conns:
        if c[1] in cnodes and c[2] in models and len(c) > 3 and any(l[1] == c[1] and l[2] in layers for l in conns):
            keys = {}
            for k in conns:
                if k[2] == c[1] and k[1] in curves and len(k) > 3:
                    times, values = (_first(curves[k[1]]['children'], n)['props'][0] for n in ('KeyTime', 'KeyValueFloat'))
                    keys[k[3]] = float(values[min(range(len(times)), key=lambda i: times[i])])
            if len(keys) == 3: out.setdefault(models[c[2]], {})[c[3]] = tuple(keys['d|' + a] for a in 'XYZ')
    return out


def shared_models(path):
    """Modèle de models() → les autres modèles qui instancient sa géométrie (pales du moulin à vent : une géométrie,
    quatre nœuds ; models() et load_mesh n'en gardent qu'un, le dernier relié)."""
    _, nodes = parse(path)
    geos = {g['props'][0] for g in find(nodes, 'Geometry')}
    models = {m['props'][0]: _node_name(m) for m in find(nodes, 'Model')}
    users = {}
    for conn in find(nodes, 'Connections'):
        for c in conn['children']:
            if len(c['props']) >= 3 and c['props'][1] in geos and c['props'][2] in models: users.setdefault(c['props'][1], []).append(models[c['props'][2]])
    return {names[-1]: names[:-1] for names in users.values() if len(names) > 1}


def load_mesh(path, model=None):
    """Retourne {'name', 'pos': [(x,y,z)] (unités FBX, recalés sur le pivot), 'polys': [[i,...]],
    'nrm': normales par sommet de polygone ou None, 'pivot'} du modèle nommé `model` s'il existe, sinon du premier
    modèle qui n'est pas un collider."""
    _, nodes = parse(path)
    linked = _linked_models(nodes)
    if not linked: raise ValueError('no geometry in %s' % path)
    chosen = next((l for l in linked if model and _node_name(l[1]) == model), None)
    if chosen is None: chosen = next((l for l in linked if 'collider' not in _node_name(l[1]).lower()), linked[0])
    geo, model_node, mat_names = chosen
    # Pivot dans le repère des sommets : le RotationPivot s'applique après le Lcl Scaling du nœud (établi de charpentier,
    # évier : Lcl Scaling 0,01, pivot 0,44 = ScalingPivot 44 × 0,01).
    scaling = next((tuple(float(c) for c in p['props'][4:7]) for p in find(model_node['children'], 'P') if p['props'][0] == 'Lcl Scaling'), (1.0, 1.0, 1.0))
    pv = tuple(c / (scaling[i] or 1.0) for i, c in enumerate(_pivot(model_node)))
    v = _first(geo['children'], 'Vertices')['props'][0]
    pos = [(v[i] - pv[0], v[i + 1] - pv[1], v[i + 2] - pv[2]) for i in range(0, len(v), 3)]
    polys = _polygons(_first(geo['children'], 'PolygonVertexIndex')['props'][0])
    return {'name': _node_name(model_node), 'pos': pos, 'polys': polys, 'nrm': _normals(geo, polys), 'uv': _uvs(geo, polys), 'uv1': _uvs(geo, polys, 1), 'alpha': _alphas(geo, polys), 'pivot': pv,
            'mat': _materials(geo, polys), 'mat_names': mat_names, 'unit_scale': _unit_scale(nodes)}


def _euler_quat(deg):
    """Quaternion {x, y, z, w} d'une rotation Euler XYZ FBX (X appliqué d'abord), repère du fichier."""
    import math
    q = {'x': 0.0, 'y': 0.0, 'z': 0.0, 'w': 1.0}
    for axis, a in enumerate(deg):
        h = math.radians(a) / 2
        r = {'x': 0.0, 'y': 0.0, 'z': 0.0, 'w': math.cos(h)}; r['xyz'[axis]] = math.sin(h)
        q = _qmul(r, q)
    return q


def _qmul(a, b):
    return {'x': a['w'] * b['x'] + a['x'] * b['w'] + a['y'] * b['z'] - a['z'] * b['y'],
            'y': a['w'] * b['y'] - a['x'] * b['z'] + a['y'] * b['w'] + a['z'] * b['x'],
            'z': a['w'] * b['z'] + a['x'] * b['y'] - a['y'] * b['x'] + a['z'] * b['w'],
            'w': a['w'] * b['w'] - a['x'] * b['x'] - a['y'] * b['y'] - a['z'] * b['z']}


def _qrot(q, v):
    qx, qy, qz, qw = q['x'], q['y'], q['z'], q['w']
    x, y, z = v
    tx, ty, tz = 2 * (qy * z - qz * y), 2 * (qz * x - qx * z), 2 * (qx * y - qy * x)
    return (x + qw * tx + (qy * tz - qz * ty), y + qw * ty + (qz * tx - qx * tz), z + qw * tz + (qx * ty - qy * tx))


def geometric_transform(path, model_name):
    """(translation, rotation Euler XYZ, scaling) « geometric » du nœud FBX nommé model_name ; identité si absents.
    Unity le cuit dans le mesh importé (offset d'objet 3ds Max)."""
    _, nodes = parse(path)
    for model in find(nodes, 'Model'):
        if _node_name(model) != model_name: continue
        props = {p['props'][0]: tuple(float(c) for c in p['props'][4:7]) for p in find(model['children'], 'P') if p['props'][0].startswith('Geometric')}
        return props.get('GeometricTranslation', (0.0, 0.0, 0.0)), props.get('GeometricRotation', (0.0, 0.0, 0.0)), props.get('GeometricScaling', (1.0, 1.0, 1.0))
    return (0.0, 0.0, 0.0), (0.0, 0.0, 0.0), (1.0, 1.0, 1.0)


def _axis_conversion(axes):
    """Quaternion qui envoie le côté, le haut et l'avant du fichier sur +X, +Y, +Z (lignes de la matrice), None si identité."""
    rows = []
    for axis, default in (('Coord', 0), ('Up', 1), ('Front', 2)):
        row = [0.0, 0.0, 0.0]; row[int(axes.get(axis + 'Axis', default))] = float(axes.get(axis + 'AxisSign', 1)); rows.append(row)
    if rows == [[1.0, 0.0, 0.0], [0.0, 1.0, 0.0], [0.0, 0.0, 1.0]]: return None
    (a, b, c), (d, e, f), (g, h, i) = rows
    w = max(0.0, 1 + a + e + i) ** 0.5 / 2
    x, y, z = (max(0.0, 1 + a - e - i) ** 0.5 / 2, max(0.0, 1 - a + e - i) ** 0.5 / 2, max(0.0, 1 - a - e + i) ** 0.5 / 2)
    x, y, z = (x if h - f >= 0 else -x), (y if c - g >= 0 else -y), (z if d - b >= 0 else -z)
    return {'x': x, 'y': y, 'z': z, 'w': w}


def node_transforms(path, override=None, pose=None, bake=False):
    """Transform global (translation en unités du fichier, quaternion, échelle) de chaque Model dans le repère du fichier,
    avec Lcl Translation, PreRotation, Lcl Rotation et Lcl Scaling (échelle composante par composante : exacte sans
    rotation entre deux échelles non uniformes ; sablier : verre sous l'armature à 0,118), et la liste des modèles racine.
    Un seul modèle racine : Unity le fusionne avec l'objet racine du FBX (sa transform est celle de l'instance).
    Conversion d'axes : un fichier dont l'avant et le côté sont inversés (Blender, FrontAxisSign −1 / CoordAxisSign −1)
    reçoit un demi-tour autour de l'axe vertical sur ses racines, comme FbxAxisSystem::ConvertScene à l'import Unity. bake
    (« Bake Axis Conversion » du .meta) : conversion complète, haut, avant et côté du fichier (UpAxis, FrontAxis, CoordAxis
    et signes) ramenés sur +Y, +Z, +X (cerf-volant, haut +X : quart de tour autour de Z ; sans bake, les fichiers au haut
    +X, dessalinisateur et filtre à eau, sont justes sans conversion : surface de l'eau horizontale).
    override : fonction (nom, (t, q, s)) → (t, q, s) qui remplace le transform local d'un nœud (overrides d'une instance).
    pose : {nom: {propriété Lcl: (x, y, z)}} qui remplace les Lcl d'un nœud (première image d'un clip : take_pose)."""
    _, nodes = parse(path)
    settings = _first(nodes, 'GlobalSettings')
    axes = {p['props'][0]: p['props'][4] for p in find(settings['children'], 'P') if len(p['props']) > 4} if settings else {}
    conv = _axis_conversion(axes) if bake else ({'x': 0.0, 'y': 1.0, 'z': 0.0, 'w': 0.0} if axes.get('FrontAxisSign', 1) == -1 and axes.get('CoordAxisSign', 1) == -1 else None)
    models = {m['props'][0]: m for m in find(nodes, 'Model')}
    parent = {}
    for conn in find(nodes, 'Connections'):
        for c in conn['children']:
            if len(c['props']) >= 3 and c['props'][0] == 'OO' and c['props'][1] in models and (c['props'][2] in models or c['props'][2] == 0):
                parent[c['props'][1]] = c['props'][2]
    local = {}
    for mid, m in models.items():
        props = {p['props'][0]: tuple(float(v) for v in p['props'][4:7]) for p in find(m['children'], 'P') if p['props'][0] in ('Lcl Translation', 'Lcl Rotation', 'Lcl Scaling', 'PreRotation', 'RotationOffset') and len(p['props']) >= 7}
        if pose: props.update(pose.get(_node_name(m)) or {})
        q = _qmul(_euler_quat(props.get('PreRotation', (0.0, 0.0, 0.0))), _euler_quat(props.get('Lcl Rotation', (0.0, 0.0, 0.0))))
        # Sommets recalés sur le RotationPivot (load_mesh) : l'origine du nœud importé est T + RotationOffset + pivot, et
        # ses enfants sont exprimés depuis le pivot du parent (portes de garage : panneau et poignée à leur place fermée).
        pid = parent.get(mid, 0)
        ppv = _pivot(models[pid]) if pid in models else (0.0, 0.0, 0.0)
        t, off, pv = props.get('Lcl Translation', (0.0, 0.0, 0.0)), props.get('RotationOffset', (0.0, 0.0, 0.0)), _pivot(m)
        local[mid] = (tuple(t[i] + off[i] + pv[i] - ppv[i] for i in range(3)), q, props.get('Lcl Scaling', (1.0, 1.0, 1.0)))
        if override: local[mid] = override(_node_name(m), local[mid])
    out = {}

    def world(mid):
        if mid in out: return out[mid]
        t, q, s = local[mid]
        pid = parent.get(mid, 0)
        if pid and pid in models:
            pt, pq, ps = world(pid)
            t = tuple(pt[i] + _qrot(pq, tuple(ps[j] * t[j] for j in range(3)))[i] for i in range(3)); q = _qmul(pq, q); s = tuple(ps[i] * s[i] for i in range(3))
        elif conv:
            t, q = _qrot(conv, t), _qmul(conv, q)
        out[mid] = (t, q, s)
        return out[mid]
    for mid in models: world(mid)
    roots = [_node_name(models[mid]) for mid in models if not parent.get(mid)]
    return {_node_name(models[mid]): v for mid, v in out.items()}, roots


def skin(path, model):
    """Liaison du mesh skinné `model` : {'bind': matrice du mesh à la liaison (pose BindPose), 'clusters': [(os,
    TransformLink, {sommet: poids})]}, matrices 4×4 M[ligne][colonne] (colonne majeure dans le fichier) ; None sans skin ou
    sans pose de liaison."""
    _, nodes = parse(path)
    models = {m['props'][0]: _node_name(m) for m in find(nodes, 'Model')}
    deformers = {d['props'][0]: d for d in find(nodes, 'Deformer')}
    geos = {g['props'][0] for g in find(nodes, 'Geometry')}
    conns = [c['props'] for conn in find(nodes, 'Connections') for c in conn['children'] if len(c['props']) >= 3]
    mids = {k for k, v in models.items() if v == model}
    gid, mid = next(((c[1], c[2]) for c in conns if c[1] in geos and c[2] in mids), (None, None))    # le nœud qui porte la géométrie (un os peut avoir le même nom)
    def mat(a): return [[a[c * 4 + r] for c in range(4)] for r in range(4)]
    bind = None
    for pose in find(nodes, 'Pose'):
        for pn in pose['children']:
            if pn['name'] == 'PoseNode' and _first(pn['children'], 'Node')['props'][0] == mid:
                bind = mat(_first(pn['children'], 'Matrix')['props'][0])
    clusters = []
    for sk in (c[1] for c in conns if c[2] == gid and c[1] in deformers):
        for cl in (c[1] for c in conns if c[2] == sk and c[1] in deformers):
            d = deformers[cl]
            bone = next((models[c[1]] for c in conns if c[2] == cl and c[1] in models), None)
            tl, idx, w = _first(d['children'], 'TransformLink'), _first(d['children'], 'Indexes'), _first(d['children'], 'Weights')
            if bone and tl and idx and w: clusters.append((bone, mat(tl['props'][0]), dict(zip(idx['props'][0], w['props'][0]))))
    return {'bind': bind, 'clusters': clusters} if bind and clusters else None


def node_paths(path):
    """Chemin de chaque Model depuis la racine du fichier (« Parent/Enfant »), par nom."""
    _, nodes = parse(path)
    models = {m['props'][0]: m for m in find(nodes, 'Model')}
    parent = {}
    for conn in find(nodes, 'Connections'):
        for c in conn['children']:
            if len(c['props']) >= 3 and c['props'][0] == 'OO' and c['props'][1] in models and c['props'][2] in models: parent[c['props'][1]] = c['props'][2]
    out = {}
    for mid, m in models.items():
        chain, cur = [], mid
        while cur in models: chain.append(_node_name(models[cur])); cur = parent.get(cur)
        out[_node_name(m)] = '/'.join(reversed(chain))
    return out


def skinned_models(path):
    """Noms des modèles dont la géométrie porte un Deformer « Skin » (mesh animé par des os)."""
    _, nodes = parse(path)
    models = {m['props'][0]: _node_name(m) for m in find(nodes, 'Model')}
    geos = {g['props'][0] for g in find(nodes, 'Geometry')}
    skins = {d['props'][0] for d in find(nodes, 'Deformer') if len(d['props']) > 2 and d['props'][2] == 'Skin'}
    children = {}
    for conn in find(nodes, 'Connections'):
        for c in conn['children']:
            if len(c['props']) >= 3 and c['props'][0] == 'OO': children.setdefault(c['props'][2], []).append(c['props'][1])
    out = set()
    for gid in geos:
        if any(d in skins for d in children.get(gid, [])):
            out.update(models[m] for m in models if gid in children.get(m, []))
    return out


def _unit_scale(nodes):
    """GlobalSettings.UnitScaleFactor : cm par unité du fichier (1 = cm, 100 = m) ; Unity l'applique (useFileScale)."""
    settings = _first(nodes, 'GlobalSettings')
    for p in find(settings['children'], 'P') if settings else []:
        if p['props'][0] == 'UnitScaleFactor': return float(p['props'][4])
    return 1.0


def _materials(geo, polys):
    """Index de sous-matériau par polygone (0 = principal) ; tout à 0 sans LayerElementMaterial ou en AllSame."""
    layer = _first(geo['children'], 'LayerElementMaterial')
    if layer is None: return [0] * len(polys)
    mapping = _first(layer['children'], 'MappingInformationType')['props'][0]
    values = _first(layer['children'], 'Materials')['props'][0]
    if mapping == 'ByPolygon' and len(values) == len(polys): return list(values)
    return [values[0] if values else 0] * len(polys)
