"""Extrait le bundle des blocs de terrain posables du planner (terre, roches concassées, scories concassées).

    python Scripts/forms/extract_terrain.py --content "<checkout>/Eco/Content/Art" --out ecocraft/wwwroot/assets/forms

En jeu ces blocs se lissent avec le terrain (builders « Blending » : 31 cas, LOD, décorateurs) ; le planner n'en garde que
la texture : bundle sans forme, chaque bloc est le cube généré par building-planner.js (dessus et dessous en « top », côtés
en « side »), avec les textures triplanaires du .mat du bloc (teinte cuite dans la tuile).
"""
import argparse
import json
import os
import re
import sys
import time

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import extract_forms as ef   # noqa: E402
import textures              # noqa: E402
import unity_yaml            # noqa: E402

SET_ID = 'Terrain'
# Blockset (sous Blocks/Blocksets/) → blocs ; item = nom du bloc + Item.
BLOCKS = {'Terrain Blocks.asset': ['Dirt', 'CrushedBasalt', 'CrushedCoal', 'CrushedCopperOre', 'CrushedGneiss', 'CrushedGoldOre', 'CrushedGranite',
                                   'CrushedIronOre', 'CrushedLimestone', 'CrushedMixedRock', 'CrushedSandstone', 'CrushedShale', 'CrushedSulfur'],
          'Processed Mineral Blocks.asset': ['CrushedSlag']}


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument('--content', required=True, help='dossier Eco/Content/Art')
    ap.add_argument('--out', required=True, help='dossier de sortie (assets/forms)')
    args = ap.parse_args()

    started = time.time()
    blocks_root = os.path.join(args.content, 'Blocks')
    index = unity_yaml.build_guid_index(blocks_root)
    ef.log('== terrain blocks → %s (guid index %d assets)' % (SET_ID, len(index)))

    materials, skins, texture_paths, tiles = [], {}, [], {}
    def tile(mat_path, text, props, tint_prop):
        path = next((index[g] for g in (unity_yaml.material_texture_guid(mat_path, [p])[1] for p in props) if g in index), None)
        if path is None: sys.exit('%s: no texture for %s' % (os.path.basename(mat_path), props))
        tint = tuple(ef.mat_color(text, tint_prop)[:3])
        key = (path, tint, None) if tint != (1.0, 1.0, 1.0) else path
        if key not in tiles: tiles[key] = len(texture_paths); texture_paths.append(key)
        return tiles[key]
    for blockset, names in BLOCKS.items():
        by_name = {b['Name']: b for b in ef.load_blockset(os.path.join(blocks_root, 'Blocksets', blockset))['blocks']}
        for name in names:
            mat_path = index[by_name[name]['Material']['guid']]
            text = open(mat_path, encoding='utf-8', errors='replace').read()
            side, top = tile(mat_path, text, ['_SideTex', '_MainTex'], '_SideTint'), tile(mat_path, text, ['_TopTex', '_SideTex', '_MainTex'], '_TopTint')
            floats = {k: float(f.group(1)) if (f := re.search(r'- %s:\s*([\d.eE+-]+)' % k, text)) else d for k, d in (('_TextureScale', 1.0), ('_TextureOffset', 0.0))}
            skins[name + 'Item'] = len(materials)
            materials.append({'side': side, 'top': None if top == side else top, 'detail': None, 'scale': floats['_TextureScale'], 'offset': floats['_TextureOffset']})
            ef.log('  %-22s %-24s side %d top %d scale %s' % (name + 'Item', os.path.basename(mat_path), side, top, floats['_TextureScale']))

    atlas, tile_px = textures.build_atlas(texture_paths)
    bundle = {'set': SET_ID, 'version': ef.BUNDLE_VERSION, 'tile': tile_px, 'calibration': {'axis': ef.DEFAULT_AXIS, 'mirrorX': True},
              'categories': {}, 'meshes': {}, 'forms': {}, 'materials': materials, 'skins': skins, 'formMaterials': {}}
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

    ef.log('skins %d, textures %d' % (len(skins), len(texture_paths)))
    for path in (json_path, webp_path, index_path): ef.log('wrote %s (%d KB)' % (path, os.path.getsize(path) // 1024))
    ef.log('done in %.1fs' % (time.time() - started))


if __name__ == '__main__':
    main()
