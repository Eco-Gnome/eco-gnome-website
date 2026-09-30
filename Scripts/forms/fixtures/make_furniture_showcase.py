"""Plan d'import « vitrine du mobilier » : tous les objets des bundles Objects* posés en rangées sur une dalle, plus
quatre rotations de quelques meubles, pour contrôler le rendu 3D (import JSON → vue 3D).

    python Scripts/forms/fixtures/make_furniture_showcase.py ecocraft/wwwroot/assets/forms > Scripts/forms/fixtures/furniture-showcase.json
"""
import json
import os
import sys

PITCH, MARGIN, PER_ROW = 6, 3, 32         # 32 par rangée : la dalle reste sous 200 cases de côté (PlanValidator.MaxGridSide)
ROTATED = ['HewnBenchItem', 'HewnChairItem', 'HewnTableItem', 'MortaredStoneFireplaceItem', 'LumberTableItem', 'AdobeDoorItem']

forms = sys.argv[1] if len(sys.argv) > 1 else 'ecocraft/wwwroot/assets/forms'
with open(os.path.join(forms, 'index.json'), encoding='utf-8') as f: index = json.load(f)
items = []
for set_name in sorted(s for s in index['sets'] if s.startswith('Objects')):
    with open(os.path.join(forms, set_name + '.json'), encoding='utf-8') as f: bundle = json.load(f)
    items += [(item, 0) for item in sorted(bundle['objects'])]
items += [(item, r) for item in ROTATED for r in range(4)]

rows = (len(items) + PER_ROW - 1) // PER_ROW
width, depth = 2 * MARGIN + PER_ROW * PITCH, 2 * MARGIN + rows * PITCH
objects = []
for i, (item, rot) in enumerate(items):
    # Ancre au centre de la maille : les emprises vont de −2 à +3 cases autour de l'ancre (cheminée : x −2..0).
    x, y = MARGIN + (i % PER_ROW) * PITCH + 2, MARGIN + (i // PER_ROW) * PITCH + 2
    objects.append({'id': 'f%d' % (i + 1), 'type': item, 'x': x, 'y': y, 'rotation': rot})

plan = {
    'schemaVersion': 4, 'name': 'Furniture showcase', 'mode': 'house',
    'grid': {'width': width, 'depth': depth},
    'architecture': {'height': 8, 'ops': [
        {'id': 'g1', 'kind': 'box', 'subtract': False, 'material': 'HewnLogItem', 'a': [0, 0, 0], 'b': [width - 1, depth - 1, 0], 'hollow': False, 'thickness': 1, 'form': 'Floor', 'rot': 0}]},
    'defaults': {'wallHeight': 4},
    'levels': [{'name': '', 'height': 4, 'rooms': [], 'objects': objects}],
    'groundIndex': 0, 'analysis': {'residents': 1, 'propertyType': 'Residence'}, 'prices': {},
}
json.dump(plan, sys.stdout, ensure_ascii=False)
