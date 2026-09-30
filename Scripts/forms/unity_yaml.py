"""Lecture minimale des assets Unity en YAML texte (.asset, .prefab, .mat, .meta).

Sous-ensemble suffisant pour les fichiers d'Eco : mappings et séquences par indentation, mappings en accolades
(éventuellement continués sur la ligne suivante), scalaires. Pas une implémentation YAML complète.
"""
import os
import re

_DOC_HEADER = re.compile(r'^--- !u!(\d+) &(-?\d+)')
_FLOW_ITEM = re.compile(r'\s*([^,:{}]+?)\s*:\s*([^,{}]*?)\s*(?:,|$)')


def _scalar(text):
    text = text.strip()
    if text == '' or text == '~': return None
    if text == '[]': return []
    if text == '{}': return {}
    if text.startswith('{'):
        return {k.strip(): _scalar(v) for k, v in _FLOW_ITEM.findall(text[1:text.rindex('}')])}
    if text.startswith('[') and text.endswith(']'):
        inner = text[1:-1].strip()
        return [_scalar(p) for p in inner.split(',')] if inner else []
    if re.fullmatch(r'-?\d+', text): return int(text)
    if re.fullmatch(r'-?(\d+\.\d*|\.\d+|\d+)([eE][-+]?\d+)?', text): return float(text)
    if text in ('inf', '-inf', 'nan'): return float(text)
    if len(text) >= 2 and text[0] == text[-1] and text[0] in '"\'': return text[1:-1]
    return text


def _join_flow(lines):
    """Recolle les `{...}` coupés sur plusieurs lignes (Unity le fait pour les références longues)."""
    out, buf = [], None
    for line in lines:
        if buf is not None:
            buf += ' ' + line.strip()
            if buf.count('{') <= buf.count('}'): out.append(buf); buf = None
        elif line.count('{') > line.count('}'):
            buf = line
        else:
            out.append(line)
    if buf is not None: out.append(buf)
    return out


def _indent(line):
    return len(line) - len(line.lstrip(' '))


def _parse_block(lines, i, indent):
    """Parse un bloc (mapping ou séquence) dont les lignes sont à `indent`. Retourne (valeur, index suivant)."""
    if i >= len(lines): return None, i
    if lines[i].lstrip().startswith('- '):
        seq = []
        while i < len(lines) and _indent(lines[i]) == indent and lines[i].lstrip().startswith('- '):
            body = lines[i][indent + 2:]
            if ':' in body and not body.lstrip().startswith('{') and not body.lstrip().startswith('['):
                # Élément-mapping : la première clé est sur la ligne du tiret, les suivantes à indent + 2.
                lines[i] = ' ' * (indent + 2) + body
                item, i = _parse_block(lines, i, indent + 2)
            else:
                item, i = _scalar(body), i + 1
            seq.append(item)
        return seq, i
    mapping = {}
    while i < len(lines) and _indent(lines[i]) == indent and not lines[i].lstrip().startswith('- '):
        key, _, rest = lines[i].strip().partition(':')
        i += 1
        if rest.strip() != '':
            mapping[key] = _scalar(rest)
            continue
        if i < len(lines):
            nxt = _indent(lines[i])
            if nxt > indent:
                mapping[key], i = _parse_block(lines, i, nxt); continue
            if nxt == indent and lines[i].lstrip().startswith('- '):
                mapping[key], i = _parse_block(lines, i, indent); continue
        mapping[key] = None
    return mapping, i


def parse_documents(path):
    """Retourne [(class_id, file_id, {NomDeClasse: mapping})] pour chaque document du fichier."""
    with open(path, encoding='utf-8', errors='replace') as f:
        raw = [l.rstrip('\n').rstrip('\r') for l in f]
    docs, cur = [], None
    for line in raw:
        m = _DOC_HEADER.match(line)
        if m:
            cur = (int(m.group(1)), int(m.group(2)), [])
            docs.append(cur)
        elif cur is not None and line.strip() != '':
            cur[2].append(line)
    out = []
    for class_id, file_id, lines in docs:
        lines = _join_flow(lines)
        value, _ = _parse_block(lines, 0, 0)
        out.append((class_id, file_id, value))
    return out


def parse_plain(path):
    """Un fichier YAML sans en-têtes de document (.meta) → mapping racine."""
    with open(path, encoding='utf-8', errors='replace') as f:
        lines = [l.rstrip('\n').rstrip('\r') for l in f if l.strip() != '' and not l.startswith('%')]
    value, _ = _parse_block(_join_flow(lines), 0, 0)
    return value


def first_document(path):
    """Le corps du premier document (ex. le MonoBehaviour d'un .asset)."""
    _, _, value = parse_documents(path)[0]
    return next(iter(value.values()))


def meta_guid(meta_path):
    with open(meta_path, encoding='utf-8', errors='replace') as f:
        for line in f:
            if line.startswith('guid:'): return line.split(':', 1)[1].strip()
    return None


def build_guid_index(root):
    """guid → chemin de l'asset (sans .meta) pour tous les .meta sous root."""
    index = {}
    for dirpath, _, filenames in os.walk(root):
        for name in filenames:
            if not name.endswith('.meta'): continue
            guid = meta_guid(os.path.join(dirpath, name))
            if guid: index[guid] = os.path.join(dirpath, name[:-5])
    return index


def prefab_mesh_guid(prefab_path, root_game_object_id):
    """guid du mesh du MeshFilter porté par le GameObject `root_game_object_id` (celui référencé par le builder)."""
    fallback = None
    for class_id, _, value in parse_documents(prefab_path):
        if class_id != 33: continue                       # 33 = MeshFilter
        mf = value['MeshFilter']
        mesh = mf.get('m_Mesh') or {}
        if not mesh.get('guid'): continue
        if mf.get('m_GameObject', {}).get('fileID') == root_game_object_id: return mesh['guid']
        fallback = fallback or mesh['guid']
    return fallback


def material_document(mat_path):
    """Le corps du Material (classe 21) d'un .mat ; certains commencent par un MonoBehaviour de version URP (bateaux)."""
    docs = parse_documents(mat_path)
    return next((next(iter(v.values())) for c, _, v in docs if c == 21 and v), next(iter(docs[0][2].values())))


def material_texture_guid(mat_path, properties):
    """guid de la première propriété texture renseignée parmi `properties` (ordre de préférence)."""
    mat = material_document(mat_path)
    envs = {}
    for entry in mat.get('m_SavedProperties', {}).get('m_TexEnvs', []) or []:
        for name, spec in entry.items():
            guid = (spec or {}).get('m_Texture', {}).get('guid')
            if guid: envs[name] = guid
    for prop in properties:
        if prop in envs: return prop, envs[prop]
    return None, None
