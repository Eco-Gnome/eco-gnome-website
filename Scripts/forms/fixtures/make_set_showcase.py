"""Vitrine d'un set de formes (tous sauf FarEast, qui a make_fareast_showcase.py) : plan JSON importable dans le planner.

    python Scripts/forms/fixtures/make_set_showcase.py ecocraft/wwwroot/assets/forms/HewnLogs.json Scripts/forms/fixtures/hewn-showcase.json

Première skin du set : formes orientées (4 blocs tournés) en 4 rotations, puis les motifs des formes que le set possède
(murs en L et en T, fenêtres dans un mur, dalle, piles, toit à quatre pans RoofSide / RoofCorner / RoofPeak aux rotations
du générateur, toit intelligent en pyramide sur une couronne de RoofCube, faîtes RoofPeakSet, quais, tuyaux, gravier).
Enfin une rangée de toutes les formes (rotation 0) par autre skin. Le planner n'y ajoute ni sol ni plafond ; seul le
comble du toit à quatre pans (poche d'air fermée) apparaît comme une pièce.
"""
import json, sys

bundle = json.load(open(sys.argv[1], encoding='utf-8'))
F = bundle['forms']
skins = list(bundle['skins'])
M = skins[0]
ops = []
Z = 1


def op(form, a, b=None, rot=0, name=None, material=None):
    ops.append({'id': 'g%d' % (len(ops) + 1), 'kind': 'box', 'subtract': False, 'material': material or M, 'a': list(a), 'b': list(b or a),
                'form': form, 'rot': rot, 'name': name or '%s rot %d' % (form, rot)})


def has(*forms): return all(f in F for f in forms)


def rotated(form):
    f = F[form]
    return (isinstance(f, list) and len(f) == 4) or (isinstance(f, dict) and 'rots' in f)


# 1. Formes orientées, 4 rotations chacune, 4 formes par rangée.
oriented = [f for f in F if rotated(f)]
for n, f in enumerate(oriented):
    x, y = 1 + (n % 4) * 9, 1 + (n // 4) * 3
    for r in range(4): op(f, (x + 2 * r, y, Z), None, r)
Y = 1 + (len(oriented) + 3) // 4 * 3 + 1 if oriented else 1

# 2. Murs en L et en T, fenêtres dans un mur (WindowGrilles puis Window), dalle, piles, cube, empilés.
x = 1
if has('Wall'):
    op('Wall', (1, Y, Z), (5, Y, Z + 1), 0, 'Wall L along x')
    op('Wall', (1, Y + 1, Z), (1, Y + 3, Z + 1), 0, 'Wall L along y')
    op('Wall', (8, Y, Z), (12, Y, Z + 1), 0, 'Wall T bar')
    op('Wall', (10, Y + 1, Z), (10, Y + 3, Z + 1), 0, 'Wall T stem')
    x = 15
    for win in ('WindowGrilles', 'Window', 'DoubleWindow'):
        if not has(win): continue
        # Fenêtre posée tournée (liste de 4) : rot 1 dans un mur le long de x (convention du générateur pour Brick).
        rot = 1 if isinstance(F[win], list) else 0
        op('Wall', (x, Y, Z), (x, Y, Z + 2), 0, 'Wall left of %s' % win)
        op(win, (x + 1, Y, Z), (x + 3, Y, Z + 1), rot, '%s 3x2 in a wall' % win)
        op('Wall', (x + 1, Y, Z + 2), (x + 3, Y, Z + 2), 0, 'Wall above %s' % win)
        op('Wall', (x + 4, Y, Z), (x + 4, Y, Z + 2), 0, 'Wall right of %s' % win)
        x += 7
if has('Floor'):
    op('Floor', (x, Y, Z), (x + 4, Y + 3, Z), 0, 'Floor 5x4'); x += 7
if has('SimpleFloor'):   # tapis sans bordure
    op('SimpleFloor', (x, Y, Z), (x + 4, Y + 3, Z), 0, 'SimpleFloor 5x4'); x += 7
if has('FlatRoof', 'Wall'):   # T4 : toit plat, rebord sur les bords libres (le verre a son motif plus bas)
    op('FlatRoof', (x, Y, Z), (x + 4, Y + 3, Z), 0, 'FlatRoof 5x4'); x += 7
if has('Aqueduct'):   # Brick : rigole qui se raccorde au même bloc, comme la dalle
    op('Aqueduct', (x, Y, Z), (x + 4, Y, Z), 0, 'Aqueduct run along x')
    op('Aqueduct', (x + 4, Y + 1, Z), (x + 4, Y + 3, Z), 0, 'Aqueduct turn along y'); x += 7
if has('RampA', 'RampB', 'RampC', 'RampD'):   # Brick : volée de 3 de large (bords par les voisins du même bloc), rot 2 = monte vers l'est
    for k, f in enumerate(('RampA', 'RampB', 'RampC', 'RampD')): op(f, (x + k, Y, Z), (x + k, Y + 2, Z), 2, f + ' 3 wide, rising east')
    x += 6
for f, h in (('Column', 3), ('ThinColumn', 3), ('Chimney', 3), ('DocksColumn', 3), ('DocksPillarBeamX', 1), ('Cube', 1), ('Stacked1', 1), ('Stacked2', 1), ('Stacked3', 1), ('Stacked4', 1),
             ('WallX', 3), ('RoofX', 1), ('RoofFill', 1), ('FenceX', 1)):
    if has(f) and not rotated(f): op(f, (x, Y, Z), (x, Y, Z + h - 1), 0, '%s x%d' % (f, h)); x += 2
W = x
Y += 6

# 2b. Clôture (T4 : se raccorde à ses voisins) en L et en T, barrière de route en croix, en L et seule.
x = 1
if has('Fence'):
    op('Fence', (1, Y, Z), (5, Y, Z), 0, 'Fence L along x')
    op('Fence', (1, Y + 1, Z), (1, Y + 3, Z), 0, 'Fence L along y')
    op('Fence', (8, Y, Z), (12, Y, Z), 0, 'Fence T bar')
    op('Fence', (10, Y + 1, Z), (10, Y + 3, Z), 0, 'Fence T stem')
    x = 15
if has('RoadBarrier'):
    cx, cy = x + 2, Y + 2
    op('RoadBarrier', (cx - 2, cy, Z), (cx + 2, cy, Z), 0, 'RoadBarrier cross along x')
    op('RoadBarrier', (cx, cy - 2, Z), (cx, cy - 1, Z), 0, 'RoadBarrier cross north arm')
    op('RoadBarrier', (cx, cy + 1, Z), (cx, cy + 2, Z), 0, 'RoadBarrier cross south arm')
    op('RoadBarrier', (x + 7, Y, Z), (x + 9, Y, Z), 0, 'RoadBarrier L along x')
    op('RoadBarrier', (x + 7, Y + 1, Z), (x + 7, Y + 3, Z), 0, 'RoadBarrier L along y')
    op('RoadBarrier', (x + 11, Y + 2, Z), None, 0, 'RoadBarrier alone')
    x += 14
if x > 1: W = max(W, x); Y += 6

# 2c. T5 : murs pleins, à bandeau, à clins, vitrés (raccords par les voisins) en L, garde-corps latéral en L, et poteaux
#     d'angle de l'acier plat entre des vitres Window en L et en T (2 de haut).
x = 1
for f in ('FullWall', 'WallTrim', 'CladWall', 'WindowWall'):
    if not has(f): continue
    op(f, (x, Y, Z), (x + 4, Y, Z + 1), 0, f + ' L along x')
    op(f, (x, Y + 1, Z), (x, Y + 3, Z + 1), 0, f + ' L along y')
    x += 7
if has('SideFence'):
    op('SideFence', (x, Y, Z), (x + 4, Y, Z), 0, 'SideFence L along x')
    op('SideFence', (x, Y + 1, Z), (x, Y + 3, Z), 0, 'SideFence L along y')
    x += 7
if has('WindowCorners', 'Window'):
    op('Window', (x + 1, Y, Z), (x + 4, Y, Z + 1), 0, 'Window L along x')
    op('Window', (x, Y + 1, Z), (x, Y + 3, Z + 1), 0, 'Window L along y')
    op('WindowCorners', (x, Y, Z), (x, Y, Z + 1), 0, 'WindowCorners at the L')
    op('Window', (x + 7, Y, Z), (x + 8, Y, Z + 1), 0, 'Window T bar west')
    op('Window', (x + 10, Y, Z), (x + 11, Y, Z + 1), 0, 'Window T bar east')
    op('Window', (x + 9, Y + 1, Z), (x + 9, Y + 3, Z + 1), 0, 'Window T stem')
    op('WindowCorners', (x + 9, Y, Z), (x + 9, Y, Z + 1), 0, 'WindowCorners at the T')
    x += 14
if x > 1: W = max(W, x); Y += 6

# 2d. Adobe : anneau 6 x 5 (ouvert d'une case au sud) de murs tournés sur 3 couches (WallMid le long de x en rot 0, de y en rot 1 ; WallCorner, bras
#     r0 ouest+nord, r1 ouest+sud, r2 est+sud, r3 est+nord, comme BuildingGenerator.CornerWallRot), mur libre accroché à
#     l'est par un WallT (branche est = r3) et fini par un WallEnd (bout arrondi à l'est = r2) ; toit plat du jeu : parapet
#     RoofMid / RoofCorner autour d'une dalle RoofFill au même niveau (poutres du décor côté vide).
if has('WallMid', 'WallCorner', 'WallT', 'WallEnd'):
    x0, y0, x1, y1 = 1, Y, 6, Y + 4
    op('WallMid', (x0 + 1, y0, Z), (x1 - 1, y0, Z + 2), 0, 'WallMid north')
    op('WallMid', (x0 + 1, y1, Z), (x1 - 3, y1, Z + 2), 0, 'WallMid south, open at x1 - 2 (a closed ring gets an auto ceiling)')
    op('WallMid', (x1 - 1, y1, Z), (x1 - 1, y1, Z + 2), 0, 'WallMid south')
    op('WallMid', (x0, y0 + 1, Z), (x0, y1 - 1, Z + 2), 1, 'WallMid west')
    op('WallMid', (x1, y0 + 1, Z), (x1, y1 - 1, Z + 2), 1, 'WallMid east')
    for (cx, cy), r, n in (((x0, y0), 2, 'NW'), ((x1, y0), 1, 'NE'), ((x1, y1), 0, 'SE'), ((x0, y1), 3, 'SW')):
        op('WallCorner', (cx, cy, Z), (cx, cy, Z + 2), r, 'WallCorner ' + n)
    op('WallT', (x1, y0 + 2, Z), (x1, y0 + 2, Z + 2), 3, 'WallT branch east')
    op('WallMid', (x1 + 1, y0 + 2, Z), (x1 + 3, y0 + 2, Z + 2), 0, 'WallMid free wall')
    op('WallEnd', (x1 + 4, y0 + 2, Z), (x1 + 4, y0 + 2, Z + 2), 2, 'WallEnd rounded east')
    x = x1 + 7
    if has('RoofMid', 'RoofCorner'):
        x0, x1 = x, x + 5
        op('RoofFill', (x0 + 1, y0 + 1, Z), (x1 - 1, y1 - 1, Z), 0, 'RoofFill deck inside the parapet')
        op('RoofMid', (x0 + 1, y0, Z), (x1 - 1, y0, Z), 0, 'RoofMid north')
        op('RoofMid', (x0 + 1, y1, Z), (x1 - 1, y1, Z), 0, 'RoofMid south')
        op('RoofMid', (x0, y0 + 1, Z), (x0, y1 - 1, Z), 1, 'RoofMid west')
        op('RoofMid', (x1, y0 + 1, Z), (x1, y1 - 1, Z), 1, 'RoofMid east')
        for (cx, cy), r, n in (((x0, y0), 2, 'NW'), ((x1, y0), 1, 'NE'), ((x1, y1), 0, 'SE'), ((x0, y1), 3, 'SW')):
            op('RoofCorner', (cx, cy, Z), None, r, 'RoofCorner ' + n)
        x = x1 + 3
    W = max(W, x); Y += 7

# 3. Toits : quatre pans sur cubes (côtés : le bas regarde N r1, S r3, W r2, E r0 ; coins MS_RoofCorner : NW r3, NE r2,
#    SE r1, SW r0, un quart de tour de moins pour le coin CL du béton et des T5 — BuildingGenerator.CornerRot ; faîte le long de x
#    r0), toit intelligent, faîtes RoofPeakSet.
x = 1
corner_shift = 3 if bundle['set'] in ('ReinforcedConcrete', 'AshlarStone', 'FlatSteel', 'FramedGlass') or bundle['set'].startswith('Composite') else 0
if has('RoofSide', 'RoofCorner', 'RoofPeak'):
    x0, y0, x1, y1 = 1, Y, 7, Y + 4
    op('Cube', (x0, y0, Z), (x1, y1, Z), 0, 'Cube base of the hip roof')
    for k in range(2):
        z, a0, b0, a1, b1 = Z + 1 + k, x0 + k, y0 + k, x1 - k, y1 - k
        op('RoofSide', (a0 + 1, b0, z), (a1 - 1, b0, z), 1, 'hip side N')
        op('RoofSide', (a0 + 1, b1, z), (a1 - 1, b1, z), 3, 'hip side S')
        op('RoofSide', (a0, b0 + 1, z), (a0, b1 - 1, z), 2, 'hip side W')
        op('RoofSide', (a1, b0 + 1, z), (a1, b1 - 1, z), 0, 'hip side E')
        for (cx, cy), r, n in (((a0, b0), 3, 'NW'), ((a1, b0), 2, 'NE'), ((a1, b1), 1, 'SE'), ((a0, b1), 0, 'SW')):
            op('RoofCorner', (cx, cy, z), None, (r + corner_shift) % 4, 'hip corner ' + n)
    op('RoofPeak', (x0 + 2, y0 + 2, Z + 3), (x1 - 2, y0 + 2, Z + 3), 0, 'hip ridge along x')
    x = 10
if has('Roof'):
    if has('RoofCube'):
        op('RoofCube', (x, Y, Z), (x + 6, Y + 4, Z), 0, 'RoofCube ring')
        op('Cube', (x + 1, Y + 1, Z), (x + 5, Y + 3, Z), 0, 'Cube core')
    op('Roof', (x, Y, Z + 1), (x + 6, Y + 4, Z + 1), 0, 'Roof course 1')
    op('Roof', (x + 1, Y + 1, Z + 2), (x + 5, Y + 3, Z + 2), 0, 'Roof course 2')
    op('Roof', (x + 2, Y + 2, Z + 3), (x + 4, Y + 2, Z + 3), 0, 'Roof ridge')
    x += 9
elif has('RoofCube') and not rotated('RoofCube'):   # T5 : avant-toit sans toit intelligent (Ashlar, Composite)
    op('RoofCube', (x, Y, Z), (x + 6, Y + 4, Z), 0, 'RoofCube ring')
    op('Cube', (x + 1, Y + 1, Z), (x + 5, Y + 3, Z), 0, 'Cube core')
    x += 9
for ridge in ('RoofPeakSet', 'PeakSet', 'UnderPeakSet'):   # PeakSet, UnderPeakSet : faîtes des pentes du béton
    if not has(ridge): continue
    cx, cy = x + 2, Y + 2
    op(ridge, (cx - 2, cy, Z), (cx + 2, cy, Z), 0, ridge + ' cross along x')
    op(ridge, (cx, cy - 2, Z), (cx, cy - 1, Z), 0, ridge + ' cross north arm')
    op(ridge, (cx, cy + 1, Z), (cx, cy + 2, Z), 0, ridge + ' cross south arm')
    op(ridge, (x + 7, Y, Z), (x + 9, Y, Z), 0, ridge + ' L along x')
    op(ridge, (x + 7, Y + 1, Z), (x + 7, Y + 3, Z), 0, ridge + ' L along y')
    x += 12
if x > 1: W = max(W, x); Y += 7

# 4. Quais : barrière en U ouvert au nord sur un tablier, plateforme, rampe A→D, portique relevé en jeu (deux colonnes
#    voisines, jonctions poteau + poutre à deux couches d'écart, bouts de poutre).
if has('DocksFenceMid', 'DocksFenceCorner', 'DocksFenceEndCap', 'DocksPlatformFill'):
    x0, x1, y1 = 1, 7, Y + 4
    op('DocksFenceMid', (x0 + 1, y1, Z), (x1 - 1, y1, Z), 1, 'DocksFenceMid along x')
    op('DocksFenceMid', (x0, Y + 1, Z), (x0, y1 - 1, Z), 0, 'DocksFenceMid along y')
    op('DocksFenceMid', (x1, Y + 1, Z), (x1, y1 - 1, Z), 0, 'DocksFenceMid along y')
    op('DocksFenceCorner', (x0, y1, Z), None, 1, 'DocksFenceCorner south-west')
    op('DocksFenceCorner', (x1, y1, Z), None, 2, 'DocksFenceCorner south-east')
    op('DocksFenceEndCap', (x0, Y, Z), None, 0, 'DocksFenceEndCap north')
    op('DocksFenceEndCap', (x1, Y, Z), None, 0, 'DocksFenceEndCap north')
    op('DocksPlatformFill', (x0 + 1, Y, Z - 1), (x1 - 1, y1 - 1, Z - 1), 0, 'DocksPlatformFill deck')
if has('DocksPlatform', 'DocksRamps'):
    op('DocksPlatform', (10, Y, Z), (14, Y + 2, Z), 0, 'DocksPlatform 5x3')
    op('DocksRamps', (10, Y + 4, Z), (14, Y + 4, Z), 0, 'DocksRamps row')
if has('DocksRampA', 'DocksRampB', 'DocksRampC', 'DocksRampD'):
    for k, f in enumerate(('DocksRampA', 'DocksRampB', 'DocksRampC', 'DocksRampD')): op(f, (17 + k, Y, Z), None, 2, f + ' rising east')
if has('DocksPillar', 'DocksPillarBeamJunction', 'DocksPillarBeam', 'DocksPillarBeamEnd', 'DocksPillarBeamEndAlt'):
    px, py = 26, Y + 2
    for c, r in ((px, 0), (px + 1, 2)):
        op('DocksPillar', (c, py, Z), (c, py, Z + 1), r, 'DocksPillar')
        op('DocksPillarBeamJunction', (c, py, Z + 2), None, r, 'DocksPillarBeamJunction lower')
        op('DocksPillar', (c, py, Z + 3), None, r, 'DocksPillar')
        op('DocksPillarBeamJunction', (c, py, Z + 4), None, r, 'DocksPillarBeamJunction upper')
    op('DocksPillarBeam', (px - 1, py, Z + 2), None, 0, 'DocksPillarBeam'); op('DocksPillarBeam', (px + 2, py, Z + 2), None, 0, 'DocksPillarBeam')
    op('DocksPillarBeamEnd', (px - 2, py, Z + 2), None, 0, 'beam end west'); op('DocksPillarBeamEndAlt', (px + 3, py, Z + 2), None, 0, 'beam end east')
    op('DocksPillarBeamEnd', (px - 1, py, Z + 4), None, 0, 'beam end west'); op('DocksPillarBeamEndAlt', (px + 2, py, Z + 4), None, 0, 'beam end east')
if any(o['form'].startswith('Docks') and o['a'][1] >= Y for o in ops): W = max(W, 31); Y += 7

# 5. Tuyaux (une forme par skin, raccords par les voisins) : ligne, coude, T, croix et montée, par métal.
pipes = [(s, bundle['skinForms'][s]) for s in skins if s in bundle.get('skinForms', {}) and bundle['skinForms'][s] in F]
for n, (skin, form) in enumerate(pipes):
    x0 = 1 + n * 12
    op(form, (x0, Y, Z), (x0 + 6, Y, Z), 0, form + ' run along x', skin)
    op(form, (x0 + 6, Y + 1, Z), (x0 + 6, Y + 4, Z), 0, form + ' elbow then run along y', skin)
    op(form, (x0 + 3, Y + 1, Z), (x0 + 3, Y + 3, Z), 0, form + ' T branch through the cross', skin)
    op(form, (x0 + 2, Y + 2, Z), (x0 + 4, Y + 2, Z), 0, form + ' cross', skin)
    op(form, (x0, Y + 1, Z), (x0, Y + 1, Z + 3), 0, form + ' riser', skin)
if pipes: W = max(W, 1 + len(pipes) * 12); Y += 6

# 5b. Tuyaux et machines (prises = cellules Solid de l'occupancy du catalogue, rotation 0) : une pompe mécanique puise au sud
#     et refoule vers une cuve en bois, la cuve alimente une machine à vapeur à travers un mur, la cheminée de la machine
#     monte à travers une dalle ; la cheminée d'un poêle traverse un mur puis monte. Un tuyau prend la case du mur ou de la
#     dalle qu'il traverse (en jeu, le bloc reste autour) ; il se raccorde à toute prise voisine.
objects = []
if len(pipes) >= 2:
    (cu, water), (fe, smoke) = pipes[:2]
    wall = 'MortaredStoneItem'
    def obj(item, x, y): objects.append({'id': 'm%d' % (len(objects) + 1), 'type': item, 'x': x, 'y': y, 'rotation': 0})
    obj('MechanicalWaterPumpItem', 2, Y + 3)            # prises : refoulement (2, Y+2), aspiration (2, Y+4)
    op(water, (2, Y + 5, Z), (2, Y + 6, Z), 0, 'pump intake', cu)
    obj('WoodenLiquidTankItem', 6, Y + 2)               # cases x 6..8, y Y..Y+2 ; entrée (6, Y+1), sortie (8, Y+1)
    op(water, (2, Y + 1, Z), (5, Y + 1, Z), 0, 'pump to tank', cu)
    obj('SteamEngineItem', 12, Y + 2)                   # cases x 12..14, y Y+2..Y+4 ; eau (14, Y+4), cheminée (13, Y+3) en haut
    op('Wall', (10, Y, Z), (10, Y + 5, Z + 2), 0, 'wall crossed by the water pipe', wall)
    op(water, (9, Y + 1, Z), (15, Y + 1, Z), 0, 'tank to engine, through the wall', cu)
    op(water, (15, Y + 2, Z), (15, Y + 4, Z), 0, 'into the engine water port', cu)
    op('Floor', (11, Y + 1, Z + 4), (16, Y + 5, Z + 4), 0, 'slab crossed by the chimney', wall)
    op(smoke, (13, Y + 3, Z + 3), (13, Y + 3, Z + 6), 0, 'engine chimney, through the slab', fe)
    obj('StoveItem', 20, Y + 2)                          # cases x 20, y Y+1..Y+2 ; cheminée (20, Y+2) couche 1
    op('Wall', (22, Y, Z), (22, Y + 4, Z + 2), 0, 'wall crossed by the stove pipe', wall)
    op(smoke, (21, Y + 2, Z + 1), (24, Y + 2, Z + 1), 0, 'stove pipe, through the wall', fe)
    op(smoke, (24, Y + 2, Z + 2), (24, Y + 2, Z + 4), 0, 'stove pipe riser', fe)
    W = max(W, 26); Y += 8

# 6. Gravier (cube à bords par les voisins) : carré 5 x 4 et chemin en L.
if list(F) == ['Cube'] and not rotated('Cube'):
    op('Cube', (1, Y, Z), (5, Y + 3, Z), 0, 'Cube patch 5x4')
    op('Cube', (8, Y, Z), (14, Y, Z), 0, 'Cube path along x')
    op('Cube', (14, Y + 1, Z), (14, Y + 4, Z), 0, 'Cube path along y')
    W = max(W, 16); Y += 6

# 6a. Routes : chaussée 10 x 5 en Cube (bords par les voisins de même bloc) ; asphalte : ligne axiale discontinue, lignes de
#     rive le long du bord nord (WhiteEdgeRotate r0) et sud (r2), coins de lignes (TwoWhiteEdgeRotate), puis une chaussée à
#     ligne continue et bande de passage piéton (WhiteCube).
if bundle['set'] in ('AsphaltRoad', 'StoneRoad'):
    op('Cube', (1, Y, Z), (10, Y + 4, Z), 0, 'road 10x5')
    if has('WhiteDashLine', 'WhiteEdgeRotate', 'TwoWhiteEdgeRotate', 'WhiteLine', 'WhiteCube'):
        op('WhiteDashLine', (1, Y + 2, Z), (10, Y + 2, Z), 0, 'WhiteDashLine centre')
        op('WhiteEdgeRotate', (2, Y, Z), (9, Y, Z), 0, 'WhiteEdgeRotate north edge')
        op('WhiteEdgeRotate', (2, Y + 4, Z), (9, Y + 4, Z), 2, 'WhiteEdgeRotate south edge')
        for (cx, cy), r, n in (((1, Y), 0, 'NW'), ((1, Y + 4), 1, 'SW'), ((10, Y + 4), 2, 'SE'), ((10, Y), 3, 'NE')):
            op('TwoWhiteEdgeRotate', (cx, cy, Z), None, r, 'TwoWhiteEdgeRotate ' + n)
        op('Cube', (13, Y, Z), (22, Y + 4, Z), 0, 'road 10x5')
        op('WhiteLine', (13, Y + 2, Z), (22, Y + 2, Z), 0, 'WhiteLine centre')
        for k in range(0, 5, 2): op('WhiteCube', (17, Y + k, Z), (18, Y + k, Z), 0, 'WhiteCube crossing')
        op('WhiteEdge', (25, Y, Z), (29, Y + 3, Z), 0, 'WhiteEdge patch 5x4')
        W = max(W, 31)
    Y += 7

# 6c. Terrain (bundle sans forme : cube généré, texture du bloc) : un bloc 2 x 2 x 2 par matériau.
if not F:
    for n, skin in enumerate(skins):
        op('Cube', (1 + (n % 10) * 4, Y + (n // 10) * 4, Z), (2 + (n % 10) * 4, Y + 1 + (n // 10) * 4, Z + 1), 0, skin, skin)
    W = max(W, 1 + min(len(skins), 10) * 4); Y += 4 * ((len(skins) + 9) // 10) + 1

# 6b. Verre (vitre Window qui se raccorde à ses voisins, sans mur) : croix, T et L de 2 de haut, mur de cubes 5 x 3 (pas de
#     vitre entre deux cubes), vitres horizontales 3 x 3 au milieu (FlatRoof), en haut et en bas de la case.
if has('Window', 'FlatRoof') and not has('Wall') and not isinstance(F['Window'], list):
    op('Window', (1, Y + 2, Z), (5, Y + 2, Z + 1), 0, 'Window cross along x')
    op('Window', (3, Y, Z), (3, Y + 4, Z + 1), 0, 'Window cross along y')
    op('Window', (8, Y, Z), (12, Y, Z + 1), 0, 'Window T bar')
    op('Window', (10, Y + 1, Z), (10, Y + 3, Z + 1), 0, 'Window T stem')
    op('Window', (15, Y, Z), (19, Y, Z + 1), 0, 'Window L along x')
    op('Window', (15, Y + 1, Z), (15, Y + 3, Z + 1), 0, 'Window L along y')
    op('Cube', (22, Y, Z), (26, Y, Z + 2), 0, 'Cube wall 5x3')
    Y += 6
    op('FlatRoof', (1, Y, Z + 1), (3, Y + 2, Z + 1), 0, 'FlatRoof 3x3')
    op('ThinFloorTop', (6, Y, Z), (8, Y + 2, Z), 0, 'ThinFloorTop 3x3')
    op('ThinFloorBottom', (11, Y, Z), (13, Y + 2, Z), 0, 'ThinFloorBottom 3x3')
    W = max(W, 28); Y += 4

# 7. Une rangée de toutes les formes (rotation 0) par autre skin (pas les tuyaux : une forme par métal, déjà posée).
for skin in (skins[1:] if not pipes else []):
    for i, f in enumerate(F):
        op(f, (1 + (i % 22) * 2, Y + (i // 22) * 2, Z), None, 0, '%s %s' % (skin, f), skin)
    W = max(W, 1 + min(len(F), 22) * 2); Y += 2 * ((len(F) + 21) // 22) + 1

W, D = max(W, 37) + 1, Y + 1
plan = {'schemaVersion': 4, 'name': 'Formes %s' % bundle['set'], 'mode': 'architecture', 'grid': {'width': W, 'depth': D},
        'architecture': {'height': 8, 'ops': ops},
        'defaults': {'wallHeight': 3},
        'levels': [{'name': 'RDC', 'height': 3, 'rooms': [], 'objects': objects}],
        'groundIndex': 0, 'analysis': {'residents': 1, 'propertyType': 'Residence'}, 'prices': {}}
used = {o['form'] for o in ops}
missing = [f for f in F if f not in used]
assert all(0 <= o['a'][i] and o['b'][i] < (W, D)[i] for o in ops for i in (0, 1)) and all(o['a'][2] >= 0 for o in ops), 'op outside the grid'
print('%s: %d ops, forms used %d / %d, missing %s' % (bundle['set'], len(ops), len(used & set(F)), len(F), missing))
json.dump(plan, open(sys.argv[2], 'w', encoding='utf-8'), indent=1, ensure_ascii=False)
