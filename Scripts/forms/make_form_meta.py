"""Écrit dans index.json les formes du marteau de chaque matériau (palette de formes du planner).

    python Scripts/forms/make_form_meta.py [--eco ../Eco] [--out ecocraft/wwwroot/assets/forms]

Sources : `Eco/Server/TechTree/data/BlockFormType/*.json` (groupe, ordre et nom anglais de chaque forme),
`Eco/Server/Mods/__core__/AutoGen/BlockFormGroup/*.cs` (ordre des groupes), les `[IsForm(typeof(<Forme>FormType),
typeof(<Matériau>Item))]` de `Eco/Server/Mods/__core__/AutoGen/Forms/*.cs` et les formes des bundles (`<Set>.json`).
Liste d'un matériau = ses IsForm ∩ les formes du bundle de son set, triée par groupe puis par forme, sans Stacked1-4 ;
tournable = entrée du bundle en liste de 4 ou `rots` (le joueur choisit la rotation), sinon automatique (`cases`, Cube).
Ajoute à index.json `formGroups`, `formMeta: {Forme: {group, order}}`, `materialForms: {MatériauItem: [{name,
rotatable}]}` ; les autres clés restent telles quelles. À relancer après chaque extraction d'un set.
"""
import argparse
import glob
import json
import os
import re

ROOT = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..'))
NOT_HAMMER = {'Stacked1', 'Stacked2', 'Stacked3', 'Stacked4'}
IS_FORM = re.compile(r'\[IsForm\(typeof\((\w+)FormType\),\s*typeof\((\w+)\)\)\]')


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument('--eco', default=os.path.join(ROOT, '..', 'Eco'), help='checkout Eco (Server/…)')
    ap.add_argument('--out', default=os.path.join(ROOT, 'ecocraft', 'wwwroot', 'assets', 'forms'), help='dossier des bundles')
    args = ap.parse_args()
    server = os.path.join(args.eco, 'Server')

    forms = {}   # Forme → {group, order, description}
    for path in glob.glob(os.path.join(server, 'TechTree', 'data', 'BlockFormType', '*.json')):
        with open(path, encoding='utf-8-sig') as f: d = json.load(f)
        forms[d['Name']] = {'group': d['Group'], 'order': d['SortOrder'], 'description': d['Description']}
    group_order = {}
    for path in glob.glob(os.path.join(server, 'Mods', '__core__', 'AutoGen', 'BlockFormGroup', '*.cs')):
        text = open(path, encoding='utf-8-sig').read()
        group_order[re.search(r'Name => "(\w+)"', text).group(1)] = int(re.search(r'SortOrder => (\d+)', text).group(1))
    is_form = {}   # MatériauItem → {Forme}
    for path in glob.glob(os.path.join(server, 'Mods', '__core__', 'AutoGen', 'Forms', '*.cs')):
        for form, item in IS_FORM.findall(open(path, encoding='utf-8-sig').read()):
            is_form.setdefault(item, set()).add(form)

    index_path = os.path.join(args.out, 'index.json')
    with open(index_path, encoding='utf-8') as f: index_doc = json.load(f)
    bundles = {}
    for set_id in set(index_doc.get('materials', {}).values()):
        with open(os.path.join(args.out, index_doc['sets'][set_id]['json']), encoding='utf-8') as f:
            bundles[set_id] = json.load(f).get('forms', {})

    def rotatable(entry):
        return (isinstance(entry, list) and len(entry) == 4) or (isinstance(entry, dict) and 'rots' in entry)

    material_forms, warnings = {}, []
    for item, set_id in sorted(index_doc.get('materials', {}).items()):
        in_bundle = bundles[set_id]
        wanted = (is_form.get(item, set()) | ({'Cube'} if 'Cube' in in_bundle else set())) - NOT_HAMMER
        missing = sorted(n for n in wanted if n not in in_bundle)
        if missing: warnings.append('%s (%s): IsForm without bundle form: %s' % (item, set_id, ', '.join(missing)))
        unknown = sorted(n for n in wanted if n in in_bundle and n not in forms)
        if unknown: warnings.append('%s: no BlockFormType for %s' % (item, ', '.join(unknown)))
        names = [n for n in wanted if n in in_bundle and n in forms]
        if not names: continue
        names.sort(key=lambda n: (group_order[forms[n]['group']], forms[n]['order'], n))
        material_forms[item] = [{'name': n, 'rotatable': rotatable(in_bundle[n])} for n in names]

    used = {e['name'] for lst in material_forms.values() for e in lst}
    index_doc['formGroups'] = sorted({forms[n]['group'] for n in used}, key=lambda g: group_order[g])
    index_doc['formMeta'] = {n: {'group': forms[n]['group'], 'order': forms[n]['order']} for n in sorted(used)}
    index_doc['materialForms'] = material_forms
    with open(index_path, 'w', encoding='utf-8') as f:
        json.dump(index_doc, f, indent=2, sort_keys=True)

    print('groups: %s' % ', '.join(index_doc['formGroups']))
    print('forms used: %d of %d, materials: %d' % (len(used), len(forms), len(material_forms)))
    for item, lst in material_forms.items():
        print('  %-28s %3d forms, %2d rotatable' % (item, len(lst), sum(e['rotatable'] for e in lst)))
    for w in warnings: print('warning: ' + w)
    print('wrote %s' % index_path)


if __name__ == '__main__':
    main()
