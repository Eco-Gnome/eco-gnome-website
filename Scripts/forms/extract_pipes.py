"""Extrait le bundle de formes des tuyaux (cuivre, fer, acier) d'Eco.

    python Scripts/forms/extract_pipes.py --content "<checkout>/Eco/Content/Art" --out ecocraft/wwwroot/assets/forms

Les tuyaux ne suivent pas le modèle des sets de construction (un builder partagé, un blockset par skin) : un seul
blockset `Blocks/Blocksets/Pipe Blocks.asset`, un builder et des meshes par métal
(`Player Built Blocks/Pipes/Pipe Builders/<Métal>Pipe.asset`), un matériau commun `PipeBlocks.mat` plaqué par les UV
(chaque métal a sa région de `Pipes_Albedo.png`). Un métal = une forme (`CopperPipe`) et un skin (`CopperPipeItem`).

Sélection : 6 voisins de face, règles EqualsType / NotEqualsType sur « <Métal>Pipe,PipeSlot » → prédicat
`type:CopperPipe,PipeSlot` (voisin du même métal ou prise d'un objet). Les meshes des tuyaux sont chiraux : on applique
le miroir X de l'import FBX d'Unity (x → −x, sens des triangles inversé), comme extract_objects.py.
"""
import argparse
import json
import os
import sys
import time

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import extract_forms as ef   # noqa: E402
import fbx                   # noqa: E402
import textures              # noqa: E402
import unity_yaml            # noqa: E402

SET_ID = 'Pipes'
METALS = ['CopperPipe', 'IronPipe', 'SteelPipe']
RULE_EQUALS_TYPE, RULE_NOT_EQUALS_TYPE = 0, 1


def compile_pipe_cases(cases):
    """Comme ef.compile_cases, pour les seules règles de type : prédicat 'type:<ruleString>', attendu 1 pour EqualsType."""
    compiled = []
    for case in cases:
        for extra in ([0, 90, 180, 270] if case['all_rotations'] else [0]):
            conds = []
            for offset, rules in case['conditions']:
                off = ef.rot_y(extra, offset)
                for rule_type, rule_string in rules:
                    if rule_type not in (RULE_EQUALS_TYPE, RULE_NOT_EQUALS_TYPE):
                        sys.exit('unexpected rule %s in a pipe builder' % rule_type)
                    conds.append((off, int(rule_type == RULE_EQUALS_TYPE), 'type:' + rule_string.replace(' ', '')))
            compiled.append({'mesh_guid': case['mesh_guid'], 'file_id': case['mesh_file_id'],
                             'rot_unity': (case['import_rot'] + (0 if case['dont_rotate'] else extra)) % 360, 'conds': conds})
    return compiled


def mirrored_mesh(path, m):
    """FBX → mesh planner avec le miroir X d'Unity ; tout le mesh en canal détail (UV)."""
    raw = fbx.load_mesh(path)
    raw['pos'] = [(-p[0], p[1], p[2]) for p in raw['pos']]
    if raw.get('nrm'): raw['nrm'] = [(-n[0], n[1], n[2]) for n in raw['nrm']]
    data, bbox, _ = ef.convert_mesh(raw, m, lambda uv: 2)
    idx = data['idx']
    for t in range(0, len(idx), 3): idx[t + 1], idx[t + 2] = idx[t + 2], idx[t + 1]
    return data, bbox


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument('--content', required=True, help='dossier Eco/Content/Art')
    ap.add_argument('--out', required=True, help='dossier de sortie (assets/forms)')
    ap.add_argument('--axis', default=ef.DEFAULT_AXIS, help='axes Unity pour x,y,z planner (défaut %s)' % ef.DEFAULT_AXIS)
    args = ap.parse_args()

    started = time.time()
    m = ef.parse_axis(args.axis)
    blocks_root = os.path.join(args.content, 'Blocks')
    pipes_dir = os.path.join(blocks_root, 'Player Built Blocks', 'Pipes')
    index = unity_yaml.build_guid_index(pipes_dir)
    blockset = unity_yaml.first_document(os.path.join(blocks_root, 'Blocksets', 'Pipe Blocks.asset'))
    blocks = {b['Name']: b for b in blockset.get('Blocks', []) or []}
    ef.log('== Pipe Blocks → %s (guid index %d assets)' % (SET_ID, len(index)))

    meshes, forms, skins, skin_forms, categories = {}, {}, {}, {}, {}
    mat_guid = None
    for metal in METALS:
        block = blocks[metal]
        mat_guid = mat_guid or block['Material']['guid']
        if block['Material']['guid'] != mat_guid: sys.exit('%s: material differs from the other pipes' % metal)
        builder = ef.load_builder(index[block['Builder']['guid']])
        by_path, cases = {}, []
        for c in compile_pipe_cases(builder['cases']):
            path = ef.resolve_mesh_path(index, c['mesh_guid'], c['file_id'])
            if path is None: sys.exit('%s: mesh %s not resolved' % (metal, c['mesh_guid']))
            if path not in by_path:
                name = metal + '_' + os.path.splitext(os.path.basename(path))[0]
                meshes[name], (lo, hi) = mirrored_mesh(path, m)
                by_path[path] = name
                ef.log('  mesh %-36s %4d tris  bbox %s..%s' % (name, len(meshes[name]['idx']) // 3, ['%.2f' % v for v in lo], ['%.2f' % v for v in hi]))
            conds = [list(ef.apply(m, off)) + [expected] for off, expected, _ in c['conds']]
            cases.append({'mesh': by_path[path], 'rot': ef.planner_angle(m, c['rot_unity']), 'conds': conds})
        means = {p for c in compile_pipe_cases(builder['cases']) for _, _, p in c['conds']}
        if len(means) != 1: sys.exit('%s: several predicates %s' % (metal, means))
        forms[metal] = {'bitMeans': means.pop(), 'cases': cases}
        skins[metal + 'Item'] = 0
        skin_forms[metal + 'Item'] = metal
        categories[metal] = block.get('Category') or 'Pipe'
        ef.log('  form %-10s %d cases, bit = %s' % (metal, len(cases), forms[metal]['bitMeans']))

    mat_path = index[mat_guid]
    tex_guid = unity_yaml.material_texture_guid(mat_path, ['_MainTex'])[1]
    atlas, tile = textures.build_atlas([index[tex_guid]])
    bundle = {
        'set': SET_ID, 'version': ef.BUNDLE_VERSION, 'tile': tile,
        'calibration': {'axis': args.axis, 'unityToPlanner': m, 'mirrorX': True,
                        'note': 'first case whose conds [dx,dy,dz,expected] all hold wins; type:<A>,<B> = neighbour block of type A (material A+Item) or an object pipe slot when B is PipeSlot'},
        'categories': categories, 'meshes': meshes, 'forms': forms,
        'materials': [{'side': 0, 'top': None, 'detail': 0, 'scale': 1.0, 'offset': 0.0}],
        'skins': skins, 'skinForms': skin_forms, 'formMaterials': {},
    }
    os.makedirs(args.out, exist_ok=True)
    json_path, webp_path = os.path.join(args.out, SET_ID + '.json'), os.path.join(args.out, SET_ID + '.webp')
    with open(json_path, 'w', encoding='utf-8') as f: json.dump(bundle, f, separators=(',', ':'))
    textures.save_atlas(atlas, webp_path, os.path.join(args.out, SET_ID + '.preview.png'))

    index_path = os.path.join(args.out, 'index.json')
    index_doc = {'sets': {}, 'materials': {}}
    if os.path.exists(index_path):
        with open(index_path, encoding='utf-8') as f: index_doc = json.load(f)
    index_doc.setdefault('sets', {})[SET_ID] = {'json': SET_ID + '.json', 'atlas': SET_ID + '.webp'}
    for item in skins: index_doc.setdefault('materials', {})[item] = SET_ID
    with open(index_path, 'w', encoding='utf-8') as f: json.dump(index_doc, f, indent=2, sort_keys=True)

    ef.log('meshes %d, triangles %d, texture %s' % (len(meshes), sum(len(v['idx']) // 3 for v in meshes.values()), os.path.basename(index[tex_guid])))
    for path in (json_path, webp_path, index_path): ef.log('wrote %s (%d KB)' % (path, os.path.getsize(path) // 1024))
    ef.log('done in %.1fs' % (time.time() - started))


if __name__ == '__main__':
    main()
