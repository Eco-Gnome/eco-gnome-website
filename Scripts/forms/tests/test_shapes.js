// Tests Node des fonctions pures des formes de blocs de ecocraft/wwwroot/js/building-planner.js (+ non-régression
// d'evalOps contre une copie building-planner.orig.js posée ici, facultative). Voir Scripts/forms/README.md.
// Usage : python make_stub.py && node test_shapes.js   (Node 18+ ; sans Node local : docker run --rm -v "$PWD:/w" -w /w node:20-alpine node test_shapes.js)
'use strict';
const fs = require('fs'), path = require('path'), vm = require('vm');

function load(file) {
    const ctx = vm.createContext({ window: {}, console: console });
    vm.runInContext(fs.readFileSync(file, 'utf8'), ctx, { filename: path.basename(file) });
    return ctx.window.ecoBuildingPlanner;
}
const JS = path.join(__dirname, '..', '..', '..', 'ecocraft', 'wwwroot', 'js', 'building-planner.js');
const api = load(JS), orig = fs.existsSync(path.join(__dirname, 'building-planner.orig.js')) ? load(path.join(__dirname, 'building-planner.orig.js')) : null;
const stub = JSON.parse(fs.readFileSync(path.join(__dirname, 'stub', 'MortaredStone.json'), 'utf8'));
const index = JSON.parse(fs.readFileSync(path.join(__dirname, 'stub', 'index.json'), 'utf8'));

let pass = 0, fail = 0;
function check(name, ok, detail) { if (ok) pass++; else { fail++; console.log('FAIL', name, detail === undefined ? '' : JSON.stringify(detail)); } }
function eq(name, got, want) { check(name, JSON.stringify(got) === JSON.stringify(want), { got: got, want: want }); }

function plan(W, D, H, ops) {
    return api.normalizePlan({ mode: 'architecture', grid: { width: W, depth: D }, architecture: { height: H, ops: ops } });
}
const at = function (vox, arr, x, y, z) { return arr[x + vox.W * (y + vox.D * z)]; };

// ---- 1. evalOps : shapes cellule par cellule --------------------------------------------------------------
{
    const p = plan(4, 2, 3, [
        { id: 'a', kind: 'box', a: [0, 0, 0], b: [2, 0, 0], material: 'M', form: 'Wall', rot: 1 },
        { id: 'b', kind: 'box', a: [1, 0, 0], b: [1, 0, 1], material: 'M' },              // sans form : cube, écrase (1,0,0)
        { id: 'c', kind: 'box', a: [2, 0, 0], b: [2, 0, 0], subtract: true, form: 'Wall' },   // soustractif : efface
        { id: 'd', kind: 'box', a: [3, 0, 0], b: [3, 0, 0], material: 'N', form: 'Cube', rot: 6 },   // rot & 3 = 2
        { id: 'e', kind: 'box', a: [3, 1, 0], b: [3, 1, 0], material: 'N', form: 'Foo' },   // forme inconnue : 0
        { id: 'f', kind: 'cells', cells: [0, 1, 2], material: 'M', form: 'Stairs', rot: 3 },
    ]);
    const v = api.evalOps(p, 3);
    check('shapes est un Uint16Array de W·D·H', Object.prototype.toString.call(v.shapes) === '[object Uint16Array]' && v.shapes.length === 4 * 2 * 3);
    eq('Wall rot1 → 5', at(v, v.shapes, 0, 0, 0), 1 * 4 + 1);
    eq('écrasé par un cube → 0', at(v, v.shapes, 1, 0, 0), 0);
    eq('cube au-dessus → 0', at(v, v.shapes, 1, 0, 1), 0);
    eq('soustrait : cells 0', at(v, v.cells, 2, 0, 0), 0);
    eq('soustrait : shapes 0', at(v, v.shapes, 2, 0, 0), 0);
    eq('Cube rot 6 → 9·4+2', at(v, v.shapes, 3, 0, 0), 9 * 4 + 2);
    eq('forme inconnue → 0', at(v, v.shapes, 3, 1, 0), 0);
    eq('cells Stairs rot3 → 7·4+3', at(v, v.shapes, 0, 1, 2), 7 * 4 + 3);
    eq('shapeCode subtract → 0', api.shapeCode({ subtract: true, form: 'Wall', rot: 1 }), 0);
    eq('shapeCode sans form → 0', api.shapeCode({ material: 'M' }), 0);
    eq('shapeCode rot absent → rot 0', api.shapeCode({ form: 'RoofPeak' }), 6 * 4);
    eq('FORMS', api.FORMS, ['Wall', 'Floor', 'RoofSide', 'RoofCorner', 'RoofTurn', 'RoofPeak', 'Stairs', 'Column', 'Cube', 'WindowGrilles',
        'DocksPlatform', 'DocksPlatformFill', 'DocksColumn', 'DocksPillar', 'DocksPillarBeam', 'DocksPillarBeamCorner', 'DocksPillarBeamEnd', 'DocksPillarBeamEndAlt',
        'DocksPillarBeamJunction', 'DocksPillarBeamT', 'DocksPillarBeamX', 'DocksFenceMid', 'DocksFenceCorner', 'DocksFenceT', 'DocksFenceX', 'DocksFenceEndCap',
        'DocksFenceEndCapDouble', 'DocksFenceSolo', 'DocksRamps', 'DocksRampsCorner', 'DocksRampsCornerInverted', 'DocksRampA', 'DocksRampB', 'DocksRampC', 'DocksRampD',
        'DocksBarrelPlatform', 'Ladder', 'Stacked1', 'Stacked2', 'Stacked3', 'Chimney', 'CopperPipe', 'IronPipe',
        'Wall_01', 'Wall_02', 'Wall_03', 'Wall_04', 'Wall_05', 'Wall_06', 'Wall_07', 'Wall_08', 'Wall_09', 'Wall_10', 'Wall_11', 'Wall_12', 'Wall_13', 'Wall_14',
        'Wall_15', 'Wall_16', 'Wall_17', 'Wall_18', 'Wall_19', 'WallCorner_01', 'WallCorner_02', 'WallCorner_03', 'WallCorner_04', 'WallT_01', 'WallT_02', 'WallT_03',
        'WallT_04', 'WallX_01', 'WallX_02', 'WallX_03', 'WallX_04', 'Column_01', 'Column_02', 'Column_03', 'Ceiling', 'Window', 'FenceMid', 'FenceCorner', 'FenceT',
        'FenceX', 'FenceSolo', 'FenceEnd', 'StairsMid', 'StairsEndLeft', 'StairsEndRight', 'StairsTurn', 'StairsCorner', 'RoofEdgeSide', 'RoofEdgeCorner', 'RoofEdgeTurn',
        'RoofPeakCorner', 'RoofPeakT', 'RoofPeakX', 'RoofUnderslopeSide', 'RoofUnderslopeTurn', 'RoofUnderslopeCorner', 'UnderInnerPeak', 'RoofCube', 'Roof', 'RoofPeakSet',
        'FlatRoof', 'ThinFloorTop', 'ThinFloorBottom', 'ThinWallStraight', 'ThinWallCorner', 'EdgeWall', 'EdgeWallTurn', 'Stacked4',
        'Aqueduct', 'BasicSlopePoint', 'BasicSlopeSide', 'UnderSlopePeak', 'UnderSlopeSide', 'Brace', 'BraceCorner', 'BraceTurn', 'SideBrace',
        'SmallCornerBrace', 'UnderBrace', 'UnderBraceCorner', 'UnderBraceTurn', 'ThinWallEdge', 'WindowEdge', 'WindowGrillesEdge', 'RampA', 'RampB',
        'RampC', 'RampD',
        'RoadBarrier', 'Fence', 'DoubleWindow', 'FloatStairs', 'FloatStairsTurn', 'FloatStairsCorner', 'ThinColumn', 'UnderPeakSet', 'PeakSet',
        'UnderStairs', 'BasicSlopeCorner', 'BasicSlopeTurn', 'UnderSlopeCorner', 'UnderSlopeTurn', 'HalfSlopeA', 'HalfSlopeB',
        'FullWall', 'WindowWall', 'CladWall', 'WallTrim', 'SideFence', 'WindowCorners',
        'WallSolo', 'WallMid', 'WallEnd', 'WallT', 'WallCorner', 'WallX', 'RoofSolo', 'RoofMid', 'RoofEnd', 'RoofT', 'RoofX', 'RoofFill', 'StairsSolo',
        'WhiteCube', 'WhiteLine', 'WhiteDashLine', 'WhiteEdge', 'WhiteEdgeRotate', 'TwoWhiteEdgeRotate', 'WhiteRampLineA', 'WhiteRampLineB',
        'WhiteRampLineC', 'WhiteRampLineD', 'WhiteRampDashLineA', 'WhiteRampDashLineB', 'WhiteRampDashLineC', 'WhiteRampDashLineD', 'WhiteRampEdgeA',
        'WhiteRampEdgeB', 'WhiteRampEdgeC', 'WhiteRampEdgeD', 'SimpleFloor', 'CanopyWindow', 'SteelPipe']);
    eq('shapeCode Wall_12 rot 3 → 55·4+3', api.shapeCode({ form: 'Wall_12', rot: 3 }), 55 * 4 + 3);
    eq('shapeCode DocksFenceMid rot 1 → 22·4+1', api.shapeCode({ form: 'DocksFenceMid', rot: 1 }), 22 * 4 + 1);
    // Une op qui dépasse architecture.height est rognée : rien au-dessus, même si le tableau a plus de couches.
    const v2 = api.evalOps(plan(4, 2, 2, [{ id: 'a', kind: 'box', a: [0, 0, 0], b: [0, 1, 2], material: 'M', form: 'Wall' }]), 3);
    eq('op au-dessus de architecture.height rognée', [at(v2, v2.cells, 0, 1, 1), at(v2, v2.cells, 0, 1, 2), at(v2, v2.shapes, 0, 1, 2)], [1, 0, 0]);
}

// ---- 2. Non-régression : cells / owner / palette identiques à l'original sur des plans sans form ------------
if (orig) {
    const ops = [
        { id: 'a', kind: 'box', a: [1, 1, 0], b: [8, 6, 4], material: 'A', hollow: true, thickness: 1 },
        { id: 'b', kind: 'sphere', a: [2, 2, 2], b: [7, 7, 7], material: 'B' },
        { id: 'c', kind: 'cylinder', a: [0, 0, 0], b: [3, 9, 3], material: 'C', axis: 'y', hollow: true, thickness: 1 },
        { id: 'd', kind: 'line', a: [0, 0, 0], b: [9, 9, 9], material: 'D' },
        { id: 'e', kind: 'curve', a: [0, 9, 0], c: [5, 0, 9], b: [9, 9, 0], material: 'E' },
        { id: 'f', kind: 'cells', cells: [0, 0, 9, 1, 1, 9, 12, 12, 12], material: 'F' },
        { id: 'g', kind: 'box', a: [3, 3, 3], b: [5, 5, 5], subtract: true },
        { id: 'h', kind: 'box', a: [-2, -2, -2], b: [20, 20, 20], material: 'A', hollow: true, thickness: 2 },
    ];
    [[plan(10, 10, 10, ops), undefined], [plan(10, 10, 10, ops), 12], [plan(10, 10, 6, ops), 12], [plan(5, 5, 5, []), 5]].forEach(function (c, i) {
        const a = api.evalOps(c[0], c[1]), b = orig.evalOps(c[0], c[1]);
        check('non-régression cells #' + i, a.cells.length === b.cells.length && a.cells.every(function (x, j) { return x === b.cells[j]; }));
        check('non-régression owner #' + i, a.owner.every(function (x, j) { return x === b.owner[j]; }));
        eq('non-régression palette #' + i, a.palette, b.palette);
        eq('non-régression W/D/H #' + i, [a.W, a.D, a.H], [b.W, b.D, b.H]);
        check('shapes nul sans form #' + i, a.shapes.every(function (x) { return x === 0; }));
        eq('non-régression countByMaterial #' + i, api.countByMaterial(a), orig.countByMaterial(b));
        const ea = api.elevation(a, 'y', -1), eb = orig.elevation(b, 'y', -1);
        check('non-régression elevation #' + i, ea.beyond.every(function (x, j) { return x === eb.beyond[j]; }) && ea.depth.every(function (x, j) { return x === eb.depth[j]; }));
    });
}

// ---- 3. Prédicats de voisinage (formsNeighbor) sur un anneau de murs ------------------------------------------
const set = api.formsPrepareSet(stub);
{
    const p = plan(5, 5, 2, [
        { id: 'r', kind: 'box', a: [0, 0, 0], b: [4, 4, 0], material: 'M', hollow: true, thickness: 1, form: 'Wall' },
        { id: 't', kind: 'box', a: [2, 1, 0], b: [2, 1, 0], material: 'M', form: 'Wall', rot: 2 },
        { id: 'c', kind: 'box', a: [2, 2, 0], b: [2, 2, 0], material: 'M' },                       // cube sans forme
        { id: 'n', kind: 'box', a: [1, 2, 0], b: [1, 2, 0], material: 'N', form: 'Wall' },         // autre matériau
    ]);
    const v = api.evalOps(p, 2);
    const nb = function (x, y, z, cond, means) { return api.formsNeighbor(set, means || 'category:Building', v, x, y, z, cond); };
    eq('E bâti (Wall = Building)', nb(0, 0, 0, [1, 0, 0]), true);
    eq('S bâti', nb(0, 0, 0, [0, 1, 0]), true);
    eq('W hors grille', nb(0, 0, 0, [-1, 0, 0]), false);
    eq('N hors grille', nb(0, 0, 0, [0, -1, 0]), false);
    eq('au-dessus vide', nb(0, 0, 0, [0, 0, 1]), false);
    eq('dessous depuis la couche 1 : cube sans forme = Terrain', [nb(2, 2, 1, [0, 0, -1]), nb(2, 2, 1, [0, 0, -1], 'category:Terrain')], [false, true]);
    eq('Wall n\'est pas Terrain', nb(0, 0, 0, [1, 0, 0], 'category:Terrain'), false);
    eq('bitMeans propre à la condition (5e élément) prime', nb(0, 0, 0, [1, 0, 0, 1, 'category:Terrain']), false);
    eq('cube sans forme = catégorie Cube du set (Terrain)', [nb(2, 1, 0, [0, 1, 0]), nb(2, 1, 0, [0, 1, 0], 'category:Terrain')], [false, true]);
    eq('sameType : même forme à un bloc (rotation ignorée) et même matériau', nb(2, 1, 0, [0, -1, 0], 'sameType'), true);
    eq('sameType : autre matériau', nb(2, 1, 0, [-1, 1, 0], 'sameType'), false);
    eq('sameType : vers un cube sans forme', nb(2, 1, 0, [0, 1, 0], 'sameType'), false);
    eq('sameType : mur (1,2) matériau N vs cube (2,2) matériau M', nb(1, 2, 0, [1, 0, 0], 'sameType'), false);
    eq('prédicat inconnu', nb(0, 0, 0, [1, 0, 0], 'foo'), false);
    // Cube implicite (crayon, code 0) et forme Cube explicite (EmptyEdgeFiller) : même type ; autre matériau : non.
    const vc = api.evalOps(plan(3, 1, 1, [{ id: 'a', kind: 'cells', cells: [0, 0, 0], material: 'M' }, { id: 'b', kind: 'box', a: [1, 0, 0], b: [1, 0, 0], material: 'M', form: 'Cube' },
        { id: 'c', kind: 'box', a: [2, 0, 0], b: [2, 0, 0], material: 'N', form: 'Cube' }]), 1);
    eq('sameType : cube implicite ↔ Cube explicite, dans les deux sens ; autre matériau', [api.formsNeighbor(set, 'sameType', vc, 0, 0, 0, [1, 0, 0]), api.formsNeighbor(set, 'sameType', vc, 1, 0, 0, [-1, 0, 0]), api.formsNeighbor(set, 'sameType', vc, 1, 0, 0, [1, 0, 0])], [true, true, false]);
    // Forme à 4 blocs tournés (liste de 4) : chaque rotation est un autre bloc du jeu.
    const vr = api.evalOps(plan(3, 1, 1, [{ id: 'a', kind: 'box', a: [0, 0, 0], b: [1, 0, 0], material: 'M', form: 'RoofSide', rot: 1 },
        { id: 'b', kind: 'box', a: [2, 0, 0], b: [2, 0, 0], material: 'M', form: 'RoofSide', rot: 2 }]), 1);
    eq('sameType : forme tournée, même rotation / autre rotation', [api.formsNeighbor(set, 'sameType', vr, 1, 0, 0, [-1, 0, 0]), api.formsNeighbor(set, 'sameType', vr, 1, 0, 0, [1, 0, 0])], [true, false]);
    // v4 : 'solid' = voisin non vide ; catégorie par rotation du voisin (tableau dans categories).
    eq('solid : mur, vide', [nb(2, 1, 0, [0, -1, 0], 'solid'), nb(2, 1, 0, [0, 0, 1], 'solid')], [true, false]);
    const vd = api.evalOps(plan(3, 1, 1, [{ id: 'a', kind: 'box', a: [0, 0, 0], b: [0, 0, 0], material: 'M', form: 'DocksFenceMid', rot: 1 },
        { id: 'b', kind: 'box', a: [2, 0, 0], b: [2, 0, 0], material: 'M', form: 'DocksFenceMid', rot: 2 }]), 1);
    eq('catégorie par rotation : rot 1 → DocksFenceRope2', [api.formsNeighbor(set, 'category:DocksFenceRope2', vd, 1, 0, 0, [-1, 0, 0]), api.formsNeighbor(set, 'category:DocksFenceRope1', vd, 1, 0, 0, [-1, 0, 0])], [true, false]);
    eq('catégorie par rotation : rot 2 → DocksFenceRope1', api.formsNeighbor(set, 'category:DocksFenceRope1', vd, 1, 0, 0, [1, 0, 0]), true);
}

// ---- 4. Rotation d'un mesh ---------------------------------------------------------------------------------
{
    const m = { pos: [1, 0, 0, 0, 1, 0, 0, 0, 1], nrm: [1, 0, 0, 0, 1, 0, 0, 0, 1], idx: [0, 1, 2] };
    const r = function (deg) { const o = api.formsRotateMesh(m, deg); return [Array.from(o.pos), Array.from(o.nrm)]; };
    eq('rot 0', r(0), [[1, 0, 0, 0, 1, 0, 0, 0, 1], [1, 0, 0, 0, 1, 0, 0, 0, 1]]);
    eq('rot 90 : (x,y) → (−y,x)', r(90), [[0, 1, 0, -1, 0, 0, 0, 0, 1], [0, 1, 0, -1, 0, 0, 0, 0, 1]]);
    eq('rot 180', r(180), [[-1, 0, 0, 0, -1, 0, 0, 0, 1], [-1, 0, 0, 0, -1, 0, 0, 0, 1]]);
    eq('rot 270', r(270), [[0, -1, 0, 1, 0, 0, 0, 0, 1], [0, -1, 0, 1, 0, 0, 0, 0, 1]]);
    eq('rot −90 = 270', r(-90), r(270));
    eq('rot 450 = 90', r(450), r(90));
    check('source intacte', m.pos[0] === 1 && m.pos[1] === 0);
    // Classification des faces : cube → chaque face deux fois ; pente du prisme → −1.
    const cube = api.formsRotateMesh(stub.meshes.Cube, 0);
    eq('faces du cube', Array.from(cube.faces), [0, 0, 1, 1, 2, 2, 3, 3, 4, 4, 5, 5]);
    const cube90 = api.formsRotateMesh(stub.meshes.Cube, 90);
    eq('faces du cube tourné : +x → +y, −x → −y, +y → −x, −y → +x', Array.from(cube90.faces), [2, 2, 3, 3, 1, 1, 0, 0, 4, 4, 5, 5]);
    const wedge = api.formsRotateMesh(stub.meshes.RoofSide, 0);
    eq('faces du prisme : dessous, dos, pente ×2 = −1, côtés = −1 (triangles non pleins mais sur le plan)', Array.from(wedge.faces), [5, 5, 1, 1, -1, -1, 3, 2]);
    const wall = api.formsRotateMesh(stub.meshes.Wall, 0);
    eq('faces du mur mince : bouts ±x et dessus/dessous sur le cube, flancs non', Array.from(wall.faces), [0, 0, 1, 1, -1, -1, -1, -1, 4, 4, 5, 5]);
    // Verre : la liste d'indices suit le mesh tourné telle quelle (typée), les faces ne classent que idx ; sans clé → null.
    const win = api.formsRotateMesh(stub.meshes.Window, 90);
    check('mesh tourné : glass conservé (Uint32Array, mêmes indices)', Object.prototype.toString.call(win.glass) === '[object Uint32Array]' && Array.from(win.glass).join() === stub.meshes.Window.glass.join());
    eq('mesh tourné : faces sur idx seulement', win.faces.length, stub.meshes.Window.idx.length / 3);
    check('vitre tournée de 90° : normale ±y → ∓x', Math.abs(win.nrm[win.glass[0] * 3]) === 1 && win.nrm[win.glass[0] * 3 + 1] === 0);
    check('mesh sans glass → null', wall.glass === null);
    // v3 : uv et chan suivent le mesh tourné tels quels (typés) ; absents → null.
    const w90 = api.formsRotateMesh(stub.meshes.RoofSide, 90);
    check('mesh tourné : uv conservés (Float32Array, mêmes valeurs)', Object.prototype.toString.call(w90.uv) === '[object Float32Array]' && Array.from(w90.uv).join() === stub.meshes.RoofSide.uv.join());
    check('mesh tourné : chan conservé (Uint8Array, mêmes valeurs)', Object.prototype.toString.call(w90.chan) === '[object Uint8Array]' && Array.from(w90.chan).join() === stub.meshes.RoofSide.chan.join());
    check('mesh sans uv/chan → null', cube.uv === null && cube.chan === null);
    // Slots FBX (bloc à plusieurs matériaux) : un sous-mesh par slot, sommets partagés, faces classées sur ses indices.
    const sl = api.formsRotateMesh({ pos: stub.meshes.Cube.pos, nrm: stub.meshes.Cube.nrm, idx: stub.meshes.Cube.idx.slice(0, 6), slots: [stub.meshes.Cube.idx.slice(6)] }, 90);
    check('slots → subs (Uint32Array, sommets partagés)', sl.subs.length === 1 && Object.prototype.toString.call(sl.subs[0].idx) === '[object Uint32Array]' && sl.subs[0].pos === sl.pos);
    eq('faces du sous-mesh tourné', Array.from(sl.subs[0].faces), Array.from(cube90.faces).slice(2));
    eq('mesh sans slots → subs vide', cube.subs.length, 0);
}

// ---- 5. Préparation d'un set ---------------------------------------------------------------------------
{
    eq('formes préparées', Object.keys(set.forms).sort(), ['Broken', 'Column', 'Cube', 'DocksFenceMid', 'Floor', 'RoofPeak', 'RoofSide', 'Stacked1', 'Stairs', 'Wall', 'WindowGrilles']);
    check('RoofSide : 4 entrées', set.forms.RoofSide.list.length === 4 && set.forms.RoofSide.list.every(Boolean));
    check('RoofSide : rot 1 est tourné', set.forms.RoofSide.list[1].pos[0] !== set.forms.RoofSide.list[0].pos[0] || set.forms.RoofSide.list[1].pos[1] !== set.forms.RoofSide.list[0].pos[1]);
    check('Stairs rot 0 = même mesh que RoofSide rot 1 (cache par angle)', set.forms.Stairs.list[0] === set.forms.RoofSide.list[1]);
    check('Stairs rot 3 (360°) = RoofSide rot 0', set.forms.Stairs.list[3] === set.forms.RoofSide.list[0]);
    check('Wall : 16 cas Building, meshes résolus', set.forms.Wall.cases.length === 16 && set.forms.Wall.bitMeans === 'category:Building' && set.forms.Wall.cases.every(function (c) { return c.mesh && Array.isArray(c.conds); }));
    check('Column : 4 cas sameType, repli sans conds', set.forms.Column.cases.length === 4 && set.forms.Column.bitMeans === 'sameType' && set.forms.Column.cases[3].conds.length === 0);
    check('Floor : condition avec prédicat propre conservée', set.forms.Floor.cases[0].conds[0][4] === 'category:Building');
    check('Broken : entrées nulles', set.forms.Broken.list.every(function (m) { return m === null; }));
    check('cube du set = mesh Cube', set.cube === set.forms.Cube.list[0]);
    eq('skins', set.skins, stub.skins);
    check('mesh préparé : Float32Array + faces', Object.prototype.toString.call(set.cube.pos) === '[object Float32Array]' && set.cube.faces.length === 12);
    const noCube = api.formsPrepareSet({ meshes: stub.meshes, forms: { Wall: stub.forms.Wall } });
    check('set sans Cube : cube de repli 12 triangles', noCube.cube.idx.length === 36 && Array.from(noCube.cube.faces).join() === '0,0,1,1,2,2,3,3,4,4,5,5');
    check('set vide', api.formsPrepareSet({}).cube.idx.length === 36);
    eq('teinte du verre du set', set.glass, [0.1, 0.1, 0.3, 0.5]);
    eq('teinte du verre par défaut', api.formsPrepareSet({ meshes: stub.meshes, forms: {} }).glass, [0, 0, 0, 0.26]);
    check('WindowGrilles : mesh avec glass', set.forms.WindowGrilles.cases[0].mesh.glass.length === 12);
    // v3 : materials / skins / formMaterials / tilePx exposés ; sans materials (v1/v2) : un matériau côté seul par skin, skins renumérotées.
    eq('materials du set', set.materials, stub.materials);
    eq('formMaterials du set', set.formMaterials, { MortaredSandstoneItem: { RoofSide: 1 } });
    eq('tilePx du set (json.tile)', set.tilePx, 512);
    eq('tilePx par défaut', api.formsPrepareSet({}).tilePx, 512);
    const v2 = api.formsPrepareSet({ meshes: stub.meshes, forms: {}, skins: { A: 3, B: 1 } });
    eq('set sans materials : matériaux construits par skin', v2.materials, [{ side: 3, top: null, detail: null, scale: 1, offset: 0 }, { side: 1, top: null, detail: null, scale: 1, offset: 0 }]);
    eq('set sans materials : skins → index de matériau', [v2.skins, v2.formMaterials], [{ A: 0, B: 1 }, {}]);
    eq('set vide : materials vide', api.formsPrepareSet({}).materials, []);
}

// ---- 6. Choix de l'entrée (formsPick) ------------------------------------------------------------------
{
    const p = plan(5, 5, 2, [
        { id: 'r', kind: 'box', a: [0, 0, 0], b: [4, 4, 0], material: 'M', hollow: true, thickness: 1, form: 'Wall' },
        { id: 't', kind: 'box', a: [2, 1, 0], b: [2, 1, 0], material: 'M', form: 'Wall' },
    ]);
    const v = api.evalOps(p, 2);
    const pick = function (x, y, z) { return api.formsPick(set, at(v, v.shapes, x, y, z), v, x, y, z); };
    const W = set.forms.Wall.cases;
    check('coin NW (E+S) → cas 5 (coin ES)', pick(0, 0, 0).mesh === W[5].mesh && pick(0, 0, 0).cube === false);
    check('coin NE (S+W) → cas 6', pick(4, 0, 0).mesh === W[6].mesh);
    check('T (E+S+W, N hors grille) → cas 4 (T sans N)', pick(2, 0, 0).mesh === W[4].mesh);
    check('droit E+W → cas 9', pick(1, 0, 0).mesh === W[9].mesh);
    check('droit S+N → cas 10', pick(0, 1, 0).mesh === W[10].mesh);
    check('bout seul (2,1) : voisin N seulement → cas 14', pick(2, 1, 0).mesh === W[14].mesh);
    check('premier cas satisfait gagne : X (croix) → cas 0', (function () {
        const px = plan(3, 3, 1, [{ id: 'x', kind: 'cells', cells: [1, 1, 0, 0, 1, 0, 2, 1, 0, 1, 0, 0, 1, 2, 0], material: 'M', form: 'Wall' }]);
        const vx = api.evalOps(px, 1); return api.formsPick(set, at(vx, vx.shapes, 1, 1, 0), vx, 1, 1, 0).mesh === W[0].mesh; })());
    check('code 0 → cube du set, cube = true', api.formsPick(set, 0, v, 0, 0, 0).mesh === set.cube && api.formsPick(set, 0, v, 0, 0, 0).cube === true);
    check('RoofSide rot 2 → list[2], non plein', api.formsPick(set, api.shapeCode({ form: 'RoofSide', rot: 2 }), v, 0, 0, 0).mesh === set.forms.RoofSide.list[2] && !api.formsPick(set, api.shapeCode({ form: 'RoofSide', rot: 2 }), v, 0, 0, 0).cube);
    check('forme absente du set (RoofCorner) → cube du set, plein', api.formsPick(set, api.shapeCode({ form: 'RoofCorner' }), v, 0, 0, 0).mesh === set.cube && api.formsPick(set, api.shapeCode({ form: 'RoofCorner' }), v, 0, 0, 0).cube);
    check('forme Cube explicite → mesh Cube, plein', api.formsPick(set, api.shapeCode({ form: 'Cube', rot: 3 }), v, 0, 0, 0).mesh === set.cube && api.formsPick(set, api.shapeCode({ form: 'Cube', rot: 3 }), v, 0, 0, 0).cube);
    check('Column seule : aucun cas conditionnel → repli (cas 3), non pleine', api.formsPick(set, api.shapeCode({ form: 'Column' }), v, 0, 0, 0).mesh === set.forms.Column.cases[3].mesh && !api.formsPick(set, api.shapeCode({ form: 'Column' }), v, 0, 0, 0).cube);
    const pc = plan(1, 1, 3, [{ id: 'c', kind: 'box', a: [0, 0, 0], b: [0, 0, 2], material: 'M', form: 'Column' }]);
    const vc = api.evalOps(pc, 3), pickc = function (z) { return api.formsPick(set, at(vc, vc.shapes, 0, 0, z), vc, 0, 0, z).mesh; };
    check('pile de colonnes : bas / milieu / haut', pickc(0) === set.forms.Column.cases[1].mesh && pickc(1) === set.forms.Column.cases[0].mesh && pickc(2) === set.forms.Column.cases[2].mesh);
    const pf = plan(1, 1, 2, [{ id: 'f', kind: 'box', a: [0, 0, 0], b: [0, 0, 0], material: 'M', form: 'Floor' }, { id: 'w', kind: 'box', a: [0, 0, 1], b: [0, 0, 1], material: 'M', form: 'Wall' }]);
    const vf = api.evalOps(pf, 2);
    check('Floor sous un mur : prédicat propre Building satisfait → cube (cas 0), non plein', api.formsPick(set, at(vf, vf.shapes, 0, 0, 0), vf, 0, 0, 0).mesh === set.forms.Floor.cases[0].mesh && !api.formsPick(set, at(vf, vf.shapes, 0, 0, 0), vf, 0, 0, 0).cube);
    check('RoofPeak : aucun cas satisfiable → cube du set, plein', api.formsPick(set, api.shapeCode({ form: 'RoofPeak' }), v, 0, 0, 0).mesh === set.cube && api.formsPick(set, api.shapeCode({ form: 'RoofPeak' }), v, 0, 0, 0).cube);
    // v4 : forme « rots » : les cas de la rotation de l'op ; jamais pleine.
    const F = set.forms.DocksFenceMid;
    check('rots : 4 jeux, r2 absent → aucun cas, r3 sans bitMeans', F.rots.length === 4 && F.rots[2].cases.length === 0 && F.rots[3].bitMeans === '' && F.rots[1].bitMeans === 'solid');
    const pd = plan(1, 1, 3, [{ id: 'a', kind: 'box', a: [0, 0, 0], b: [0, 0, 0], material: 'M', form: 'DocksFenceMid', rot: 0 },
        { id: 'b', kind: 'box', a: [0, 0, 1], b: [0, 0, 1], material: 'M', form: 'DocksFenceMid', rot: 0 },
        { id: 'c', kind: 'box', a: [0, 0, 2], b: [0, 0, 2], material: 'M', form: 'DocksFenceMid', rot: 1 }]);
    const vd = api.evalOps(pd, 3), pickd = function (z) { return api.formsPick(set, at(vd, vd.shapes, 0, 0, z), vd, 0, 0, z); };
    check('r0 sur r0 (dessous DocksFenceRope1) → cas 0 de r0, non plein', pickd(1).mesh === F.rots[0].cases[0].mesh && pickd(1).cube === false);
    check('r0 sans dessous → repli de r0', pickd(0).mesh === F.rots[0].cases[1].mesh);
    check('r1 sans dessus plein → repli de r1 (Wall à 90°)', pickd(2).mesh === F.rots[1].cases[1].mesh && pickd(2).mesh !== F.rots[0].cases[1].mesh);
    const pe = plan(1, 1, 2, [{ id: 'a', kind: 'box', a: [0, 0, 0], b: [0, 0, 0], material: 'M', form: 'DocksFenceMid', rot: 1 }, { id: 'b', kind: 'box', a: [0, 0, 1], b: [0, 0, 1], material: 'M' }]);
    const ve = api.evalOps(pe, 2);
    check('r1 sous un cube (solid) → cas 0 de r1', api.formsPick(set, at(ve, ve.shapes, 0, 0, 0), ve, 0, 0, 0).mesh === F.rots[1].cases[0].mesh);
    check('r2 : aucun cas → cube du set, plein', api.formsPick(set, api.shapeCode({ form: 'DocksFenceMid', rot: 2 }), v, 0, 0, 0).mesh === set.cube);
    check('Stacked1 sous un cube → cube (cas 0), seul → pile', api.formsPick(set, api.shapeCode({ form: 'Stacked1' }), ve, 0, 0, 0).mesh === set.forms.Stacked1.cases[0].mesh
        && api.formsPick(set, api.shapeCode({ form: 'Stacked1' }), v, 0, 0, 0).mesh === set.forms.Stacked1.cases[1].mesh);
}

// ---- 7. Résolution matériau → set ---------------------------------------------------------------------
{
    const forms = { index: index, sets: { MortaredStone: set } };
    eq('matériau à bundle (v3 : clé Set:index de matériau, pas Set:tuile)', api.formsMaterial(forms, 'MortaredStoneItem').mats.Cube.key, 'MortaredStone:1');
    const sand = api.formsMaterial(forms, 'MortaredSandstoneItem');
    eq('clé par forme : skin par défaut, surcharge formMaterials sur RoofSide', [sand.mats.Cube.key, sand.mats.Wall.key, sand.mats.RoofSide.key], ['MortaredStone:0', 'MortaredStone:0', 'MortaredStone:1']);
    check('mat par forme = objet materials du set', sand.mats.Wall.mat === set.materials[0] && sand.mats.RoofSide.mat === set.materials[1]);
    eq('skin sans surcharge : toutes les formes sur son matériau', api.formsMaterial(forms, 'MortaredGraniteItem').mats.RoofSide.key, 'MortaredStone:0');
    eq('slotMaterials : matériau par slot, null → celui de la forme, forme sans slots → []', [sand.mats.Wall.slots.map(function (m) { return m.key; }), sand.mats.RoofSide.slots.length],
        [['MortaredStone:1', 'MortaredStone:0'], 0]);
    check('matériau hors index', api.formsMaterial(forms, 'HewnLogItem') === null);
    check('skin absente du set', api.formsMaterial({ index: { materials: { X: 'MortaredStone' }, sets: {} }, sets: { MortaredStone: set } }, 'X') === null);
    check('set en cours', api.formsMaterial({ index: index, sets: { MortaredStone: null } }, 'MortaredSandstoneItem') === null);
    check('set en échec', api.formsMaterial({ index: index, sets: { MortaredStone: false } }, 'MortaredSandstoneItem') === null);
    check('index en cours', api.formsMaterial({ index: 'pending', sets: {} }, 'MortaredSandstoneItem') === null);
    check('index absent', api.formsMaterial({ index: false, sets: {} }, 'MortaredSandstoneItem') === null);
    check('index jamais demandé', api.formsMaterial({ index: null, sets: {} }, 'MortaredSandstoneItem') === null);
}

// ---- 8. view3dBuild : mesh avec set, faces sans ; culling ------------------------------------------------
function fakeSt(p, forms) {
    const st = { plan: p, selection: null, palette: { secondary: '#ffb74d', text: '#ffffff', primary: '#64b5f6' }, matRgb: {}, materialsByName: {}, icons: {}, objectsByName: {}, layer: 0,
        view3d: { cap: false, forms: forms, atlas: { pending: {}, slots: {}, next: 0 } } };
    st.vox = api.evalOps(p, p.architecture.height);
    api.view3dBuild(st);
    return st;
}
{
    const loaded = { index: index, sets: { MortaredStone: set } }, none = { index: false, sets: {} };
    const one = [{ id: 'a', kind: 'box', a: [1, 1, 0], b: [1, 1, 0], material: 'MortaredSandstoneItem' }];
    let st = fakeSt(plan(4, 4, 2, one), loaded);
    eq('cube avec set : 12 triangles dans forms, rien dans mesh', [st.view3d.data.forms['MortaredStone:0'].n, st.view3d.data.mesh.n, st.view3d.faces], [36, 0, 12]);
    check('sommets triplanaires : uv −2 et normale', st.view3d.data.forms['MortaredStone:0'].uv[0] === -2 && Math.abs(st.view3d.data.forms['MortaredStone:0'].nrm.subarray(0, 3).reduce(function (a, b) { return a + Math.abs(b); }, 0) - 1) < 1e-6);
    check('mesh centré sur la cellule (1,1,0) : x ∈ [1,2]', Array.from(st.view3d.data.forms['MortaredStone:0'].pos.subarray(0, st.view3d.data.forms['MortaredStone:0'].n * 3)).filter(function (_, i) { return i % 3 === 0; }).every(function (x) { return x >= 1 && x <= 2; }));
    st = fakeSt(plan(4, 4, 2, one), none);
    eq('cube sans set : 6 faces dans mesh, forms vide', [st.view3d.data.mesh.n, Object.keys(st.view3d.data.forms).length, st.view3d.faces], [36, 0, 6]);
    st = fakeSt(plan(4, 4, 2, [{ id: 'a', kind: 'box', a: [1, 1, 0], b: [1, 1, 0], material: 'Other' }]), loaded);
    eq('matériau hors bundle : faces', [st.view3d.data.mesh.n, Object.keys(st.view3d.data.forms).length], [36, 0]);
    // Deux cubes à bundle côte à côte : la face commune est masquée des deux côtés (5 faces chacun).
    st = fakeSt(plan(4, 4, 2, [{ id: 'a', kind: 'box', a: [1, 1, 0], b: [2, 1, 0], material: 'MortaredSandstoneItem' }]), loaded);
    eq('culling entre cubes texturés', st.view3d.faces, 20);
    // Cube plein + mur mince à côté : le cube dessine sa face vers le mur (mur = non plein) ; le mur est dessiné entier
    // sauf ses bouts contre un cube (bout −x du mur collé au cube).
    st = fakeSt(plan(4, 4, 2, [{ id: 'a', kind: 'box', a: [1, 1, 0], b: [1, 1, 0], material: 'MortaredSandstoneItem' },
        { id: 'w', kind: 'box', a: [2, 1, 0], b: [2, 1, 0], material: 'MortaredSandstoneItem', form: 'Wall' }]), loaded);
    // mur (2,1) : voisin W = cube sans forme (catégorie Cube = Terrain) → pas Building → repli pilier, 12 triangles tous dessinés
    eq('cube + mur mince : 12 + 12 triangles', st.view3d.faces, 24);
    // Mur mince entre deux cubes de couleur (sans bundle) : les cubes gardent leurs faces vers le mur.
    st = fakeSt(plan(4, 4, 2, [{ id: 'a', kind: 'box', a: [0, 1, 0], b: [0, 1, 0], material: 'Other' },
        { id: 'w', kind: 'box', a: [1, 1, 0], b: [1, 1, 0], material: 'MortaredSandstoneItem', form: 'Wall' },
        { id: 'b', kind: 'box', a: [2, 1, 0], b: [2, 1, 0], material: 'Other' }]), loaded);
    eq('cubes colorés autour d\'un mur : 6 + 6 faces, mur = pilier de repli 12 triangles', [st.view3d.data.mesh.n / 6, st.view3d.data.forms['MortaredStone:0'].n / 3], [12, 12]);
    // Sélection : les sommets du mesh sélectionné portent la couleur secondaire, les autres blanc.
    const p = plan(4, 4, 2, [{ id: 'a', kind: 'box', a: [0, 0, 0], b: [0, 0, 0], material: 'MortaredSandstoneItem' }, { id: 'b', kind: 'box', a: [2, 2, 0], b: [2, 2, 0], material: 'MortaredStoneItem' }]);
    st = { plan: p, selection: { kind: 'op', id: 'b' }, palette: { secondary: '#ffb74d', text: '#ffffff', primary: '#64b5f6' }, matRgb: {}, materialsByName: {}, icons: {}, objectsByName: {}, layer: 0,
        view3d: { cap: false, forms: loaded, atlas: { pending: {}, slots: {}, next: 0 } } };
    st.vox = api.evalOps(p, 2); api.view3dBuild(st);
    eq('couleur de sélection sur le tampon du matériau 1 (v3 : MortaredStoneItem), blanc sur le matériau 0', [Array.from(st.view3d.data.forms['MortaredStone:1'].col.subarray(0, 4)), Array.from(st.view3d.data.forms['MortaredStone:0'].col.subarray(0, 4))], [[255, 183, 77, 0], [255, 255, 255, 0]]);
    // v3 : tampon par matériau de rendu (surcharge par forme), .mat = matériau du set ; encodage des uv selon chan.
    st = fakeSt(plan(4, 4, 2, [{ id: 'a', kind: 'box', a: [0, 0, 0], b: [0, 0, 0], material: 'MortaredSandstoneItem', form: 'Wall' },
        { id: 'r', kind: 'box', a: [2, 2, 0], b: [2, 2, 0], material: 'MortaredSandstoneItem', form: 'RoofSide' }]), loaded);
    eq('grès : mur → matériau 0, toit → matériau 1 (formMaterials)', [Object.keys(st.view3d.data.forms).sort(), st.view3d.data.forms['MortaredStone:0'].n, st.view3d.data.forms['MortaredStone:1'].n], [['MortaredStone:0', 'MortaredStone:1'], 36, 24]);
    check('tampon : mat = matériau du set (scale 0.2 pour le cube, 1 pour le toit)', st.view3d.data.forms['MortaredStone:0'].mat === set.materials[0] && st.view3d.data.forms['MortaredStone:0'].mat.scale === 0.2 && st.view3d.data.forms['MortaredStone:1'].mat.scale === 1);
    const roof = st.view3d.data.forms['MortaredStone:1'], rm = stub.meshes.RoofSide, seen = { 0: 0, 1: 0, 2: 0 };
    let okUv = true;
    for (let t = 0; t < rm.idx.length; t++) {   // toit seul dans ce tampon, aucune face masquée : sommet k du tampon = idx[k] du mesh
        const i = rm.idx[t], ch = rm.chan[i], u = roof.uv[t * 2], w = roof.uv[t * 2 + 1], nl = Math.abs(roof.nrm[t * 3]) + Math.abs(roof.nrm[t * 3 + 1]) + Math.abs(roof.nrm[t * 3 + 2]);
        seen[ch]++;
        if (ch === 2) okUv = okUv && Math.abs(u - rm.uv[i * 2]) < 1e-6 && Math.abs(w - (1 - rm.uv[i * 2 + 1])) < 1e-6 && nl > 0.5;
        else okUv = okUv && u === (ch === 1 ? -3 : -2) && w === u && nl > 0.5;
    }
    check('toit : chan 2 → uv (u, 1 − v) + normale, chan 1 → (−3, −3), chan 0 → (−2, −2)', okUv && seen[0] > 0 && seen[1] > 0 && seen[2] > 0, seen);
    check('cube sans chan : tous les sommets en (−2, −2)', Array.from(st.view3d.data.forms['MortaredStone:0'].uv.subarray(0, 72)).every(function (x) { return x === -2; }));
    // Coupe au-dessus de la couche : rien au-dessus de cap.
    st.view3d.cap = true; st.layer = 0; api.view3dBuild(st);
    const st2 = fakeSt(plan(2, 2, 3, [{ id: 'a', kind: 'box', a: [0, 0, 0], b: [0, 0, 2], material: 'MortaredSandstoneItem' }]), loaded);
    st2.view3d.cap = true; st2.layer = 0; api.view3dBuild(st2);
    eq('cap : seule la couche 0, 6 faces visibles', st2.view3d.faces, 12);
    // Verre : la fenêtre met son cadre (12 triangles) dans forms et sa vitre (4 triangles) dans glass, couleur = teinte ombrée
    // par face (normale ±y → ny / py), couche = z, alpha de la teinte posé sur le tampon ; pas comptée dans faces.
    const win = [{ id: 'w', kind: 'box', a: [1, 1, 0], b: [1, 1, 0], material: 'MortaredSandstoneItem', form: 'WindowGrilles' }];
    st = fakeSt(plan(4, 4, 2, win), loaded);
    const g = st.view3d.data.glass['MortaredStone:0'];
    eq('fenêtre : cadre dans forms, vitre dans glass, faces = cadre seul', [st.view3d.data.forms['MortaredStone:0'].n, g.n, st.view3d.faces, g.alpha], [36, 12, 12, 0.5]);
    const tint = function (s) { return [Math.round(0.1 * 255 * s), Math.round(0.1 * 255 * s), Math.round(0.3 * 255 * s), 0]; };
    eq('vitre : teinte ombrée −y puis +y, couche 0', [Array.from(g.col.subarray(0, 4)), Array.from(g.col.subarray(24, 28))], [tint(0.85), tint(0.7)]);
    check('vitre : sommets unis (uv −1) centrés sur la cellule (1,1,0)', g.uv[0] === -1 && Math.abs(g.pos[0] - 1.1) < 1e-6 && g.pos[1] === 1.5 && Math.abs(g.pos[2] - 0.1) < 1e-6);
    st = fakeSt(plan(4, 4, 2, one), loaded);
    eq('cube sans glass : aucun tampon de verre', Object.keys(st.view3d.data.glass), []);
    const st3 = fakeSt(plan(1, 1, 2, [{ id: 'w', kind: 'box', a: [0, 0, 0], b: [0, 0, 1], material: 'MortaredSandstoneItem', form: 'WindowGrilles' }]), loaded);
    eq('deux fenêtres empilées : 8 triangles de verre', st3.view3d.data.glass['MortaredStone:0'].n, 24);
    st3.view3d.cap = true; st3.layer = 0; api.view3dBuild(st3);
    eq('cap : le verre au-dessus de la couche disparaît', st3.view3d.data.glass['MortaredStone:0'].n, 12);
    // Cube de verre (set Glass : vitres sur les faces du cube) : deux cubes accolés perdent la vitre entre eux, comme dans le jeu.
    const cubeMesh = stub.meshes.Cube;
    const glassSet = api.formsPrepareSet({ meshes: { G: { pos: cubeMesh.pos, nrm: cubeMesh.nrm, idx: [], glass: cubeMesh.idx } }, forms: { Cube: [{ mesh: 'G', rot: 0 }] },
        skins: { GlassItem: 0 }, materials: [{ side: 0, top: null, detail: null, scale: 1, offset: 0 }], glass: [0.3, 0.3, 0.4, 0.3] });
    const glassForms = { index: { sets: { Glass: {} }, materials: { GlassItem: 'Glass' } }, sets: { Glass: glassSet } };
    st = fakeSt(plan(3, 1, 1, [{ id: 'g', kind: 'box', a: [0, 0, 0], b: [0, 0, 0], material: 'GlassItem' }]), glassForms);
    eq('cube de verre seul : 6 faces de vitre (36 sommets)', st.view3d.data.glass['Glass:0'].n, 36);
    st = fakeSt(plan(3, 1, 1, [{ id: 'g', kind: 'box', a: [0, 0, 0], b: [1, 0, 0], material: 'GlassItem' }]), glassForms);
    eq('deux cubes de verre accolés : 10 faces de vitre (60 sommets)', st.view3d.data.glass['Glass:0'].n, 60);
}

// ---- 9. objets : bundle d'objets, formsObject, dessin une fois à la case d'ancrage ------------------------
{
    const objStub = JSON.parse(fs.readFileSync(path.join(__dirname, 'stub', 'Objects.json'), 'utf8'));
    const objSet = api.formsPrepareSet(objStub);
    check('objet préparé : 1 part, 4 meshes tournés', objSet.objects.HewnDoorItem.length === 1 && objSet.objects.HewnDoorItem[0].meshes.length === 4 && objSet.objects.HewnDoorItem[0].meshes.every(Boolean));
    const m0 = objSet.objects.HewnDoorItem[0].meshes[0], m1 = objSet.objects.HewnDoorItem[0].meshes[1];
    const extent = function (m, ax) { let lo = Infinity, hi = -Infinity; for (let i = ax; i < m.pos.length; i += 3) { lo = Math.min(lo, m.pos[i]); hi = Math.max(hi, m.pos[i]); } return [lo, hi]; };
    check('rotation 0 : porte le long de x', Math.abs(extent(m0, 0)[1] - 0.4) < 1e-6 && Math.abs(extent(m0, 1)[1] - 0.1) < 1e-6, [extent(m0, 0), extent(m0, 1)]);
    check('rotation 1 (270°) : porte le long de y', Math.abs(extent(m1, 0)[1] - 0.1) < 1e-6 && Math.abs(extent(m1, 1)[1] - 0.4) < 1e-6);
    check('part au mesh invalide : objet absent', !('Broken' in objSet.objects));
    const forms = { index: index, sets: { MortaredStone: set, Objects: objSet } };
    const parts = api.formsObject(forms, 'HewnDoorItem');
    check('formsObject : clé Set:matIdx, matériau du set, 4 meshes', parts && parts.length === 1 && parts[0].key === 'Objects:0' && parts[0].mat === objSet.materials[0] && parts[0].meshes === objSet.objects.HewnDoorItem[0].meshes);
    check('formsObject : type inconnu → null', api.formsObject(forms, 'Nope') === null);
    check('formsObject : set non chargé → null', api.formsObject({ index: index, sets: {} }, 'HewnDoorItem') === null);
    check('formsObject : index absent → null', api.formsObject({ index: false, sets: {} }, 'HewnDoorItem') === null);
    // Plan maison : une porte HewnDoorItem en (1,1) tournée d'un quart ; occupancy catalogue = deux cases Wall empilées.
    const house = function () {
        return api.normalizePlan({ mode: 'house', grid: { width: 4, depth: 4 }, architecture: { height: 4, ops: [] },
            levels: [{ height: 3, objects: [{ id: 'd1', type: 'HewnDoorItem', x: 1, y: 1, rotation: 1 }] }] });
    };
    const objSt = function (fm) {
        const st = { plan: house(), selection: null, palette: { secondary: '#ffb74d', text: '#ffffff', primary: '#64b5f6' }, matRgb: {}, materialsByName: {}, icons: { HewnDoorItem: false },   // icône « en échec » : pas de new Image() sous Node
            objectsByName: { HewnDoorItem: { cells: [[0, 0, 0, 1], [0, 1, 0, 1]], size: [1, 1, 2] } }, layer: 0, view3d: { cap: false, forms: fm, atlas: { pending: {}, slots: {}, next: 0 } } };
        st.vox = api.evalOps(st.plan, 4);
        api.view3dBuild(st);
        return st;
    };
    const cells = api.v3dObjectCells(objSt({ index: false, sets: {} }));
    check('v3dObjectCells : deux cases (z 1 et 2) vers le même objet, rot 1', cells.size === 2 && cells.get(1 + 4 * (1 + 4 * 1)).id === 'd1' && cells.get(1 + 4 * (1 + 4 * 2)) === cells.get(1 + 4 * (1 + 4 * 1)) && cells.get(1 + 4 * (1 + 4 * 1)).rot === 1 && cells.get(1 + 4 * (1 + 4 * 1)).z === 1);
    let st = objSt({ index: false, sets: {} });
    eq('sans bundle : deux cubes gris empilés, face commune masquée (10 faces)', [st.view3d.faces, st.view3d.data.mesh.n, Object.keys(st.view3d.data.forms).length], [10, 60, 0]);
    st = objSt(forms);
    eq('avec bundle : mesh dessiné une fois (12 triangles), aucun cube gris', [st.view3d.faces, st.view3d.data.mesh.n, st.view3d.data.forms['Objects:0'].n], [12, 0, 36]);
    const buf = st.view3d.data.forms['Objects:0'], zs = [], xs = [];
    for (let i = 0; i < buf.n * 3; i += 3) { xs.push(buf.pos[i]); zs.push(buf.pos[i + 2]); }
    check('mesh tourné (rot 1) centré sur la case (1,1) à z = 1 : x ∈ [1.4, 1.6], z ∈ [1, 3]', Math.min.apply(null, xs) >= 1.4 - 1e-6 && Math.max.apply(null, xs) <= 1.6 + 1e-6 && Math.abs(Math.min.apply(null, zs) - 1) < 1e-6 && Math.abs(Math.max.apply(null, zs) - 3) < 1e-6);
    check('tampon objet : mat = matériau du set d\'objets', buf.mat === objSet.materials[0]);
    st.selection = { kind: 'object', id: 'd1' }; api.view3dBuild(st);
    eq('objet sélectionné : couleur secondaire', Array.from(st.view3d.data.forms['Objects:0'].col.subarray(0, 3)), [255, 183, 77]);
    // Ombrage par sommet (shader peinture des meubles) : couleur × shade / 255, gardé par la rotation.
    const shaded = JSON.parse(JSON.stringify(objStub)), doorMesh = shaded.meshes[shaded.objects.HewnDoorItem.parts[0].mesh];
    doorMesh.shade = doorMesh.pos.filter(function (_, i) { return i % 3 === 0; }).map(function () { return 102; });
    const shadedSet = api.formsPrepareSet(shaded);
    check('formsRotateMesh : shade gardé sur les 4 rotations', shadedSet.objects.HewnDoorItem[0].meshes.every(function (m) { return m.shade && m.shade.length === doorMesh.shade.length && m.shade[0] === 102; }));
    st = objSt({ index: index, sets: { MortaredStone: set, Objects: shadedSet } });
    eq('objet ombré : blanc × 102 / 255', Array.from(st.view3d.data.forms['Objects:0'].col.subarray(0, 3)), [102, 102, 102]);
    // Partie de verre (vitre de porte) : tampon de verre par teinte, alpha de la teinte, rien dans les formes ; alpha ≥ 0.95 = opaque.
    const glazed = JSON.parse(JSON.stringify(objStub));
    glazed.objects.HewnDoorItem.parts[0].glass = [0.5, 0.6, 0.7, 0.2];
    st = objSt({ index: index, sets: { MortaredStone: set, Objects: api.formsPrepareSet(glazed) } });
    const gb = st.view3d.data.glass['glass:0.5,0.6,0.7,0.2'];
    eq('objet vitré : 36 sommets de verre, alpha 0.2, aucune forme', [gb && gb.n, gb && gb.alpha, Object.keys(st.view3d.data.forms).length], [36, 0.2, 0]);
    glazed.objects.HewnDoorItem.parts[0].glass = [0.3, 0.3, 0.3, 1];
    st = objSt({ index: index, sets: { MortaredStone: set, Objects: api.formsPrepareSet(glazed) } });
    eq('verre opaque (alpha 1) : dessiné texturé', [Object.keys(st.view3d.data.glass).length, st.view3d.data.forms['Objects:0'].n], [0, 36]);
}

// ---- 10. tuyaux : prédicat « type: », prises d'objets, forme par défaut du skin --------------------------------
{
    const pipes = api.formsPrepareSet(JSON.parse(fs.readFileSync(path.join(__dirname, 'stub', 'Pipes.json'), 'utf8')));
    const loaded = { index: index, sets: { Pipes: pipes } };
    // Voisinage : cuivre (1,0,0) et (1,1,0), fer (0,1,0), vide ailleurs ; prise d'objet en (2,1,0).
    const v = api.evalOps(plan(3, 3, 1, [
        { id: 'a', kind: 'cells', cells: [1, 0, 0, 1, 1, 0], material: 'CopperPipeItem', form: 'CopperPipe' },
        { id: 'b', kind: 'cells', cells: [0, 1, 0], material: 'IronPipeItem', form: 'IronPipe' },
    ]), 1);
    v.pipeSlots = new Set([2 + 3 * 1]);
    const nb = function (dx, dy, means) { return api.formsNeighbor(pipes, means || 'type:CopperPipe,PipeSlot', v, 1, 1, 0, [dx, dy, 0, 1]); };
    eq('type: même métal, autre métal, vide, prise', [nb(0, -1), nb(-1, 0), nb(0, 1), nb(1, 0)], [true, false, false, true]);
    eq('type: sans PipeSlot dans la liste, la prise ne compte pas', nb(1, 0, 'type:CopperPipe'), false);
    eq('type: le fer voit le fer', api.formsNeighbor(pipes, 'type:IronPipe,PipeSlot', v, 1, 1, 0, [-1, 0, 0, 1]), true);
    v.pipeSlots = undefined;
    eq('type: sans pipeSlots, pas de prise', nb(1, 0), false);
    // Rendu : trois tuyaux sans forme le long de y → forme par défaut, droit au milieu, bouchon au bout (voisin −y seul), tuyau mince.
    const st = fakeSt(plan(3, 4, 1, [{ id: 'p', kind: 'box', a: [1, 0, 0], b: [1, 2, 0], material: 'CopperPipeItem' }]), loaded);
    eq('formsMaterial : code de la forme par défaut', api.formsMaterial(loaded, 'CopperPipeItem').shape, api.shapeCode({ form: 'CopperPipe' }));
    eq('tuyau sans forme : dessiné par le set, aucun cube de couleur', [st.view3d.data.mesh.n, !!st.view3d.data.forms['Pipes:0']], [0, true]);
    const pick = function (s, x, y) { return api.formsPick(pipes, api.shapeCode({ form: 'CopperPipe' }), s.vox, x, y, 0).mesh; };
    eq('droit au milieu, bouchon en bout', [pick(st, 1, 1) === pipes.forms.CopperPipe.cases[0].mesh, pick(st, 1, 2) === pipes.forms.CopperPipe.cases[2].mesh], [true, true]);
    eq('tuyau non plein : 3 × 12 triangles, faces communes gardées', st.view3d.data.forms['Pipes:0'].n, 3 * 36);
    // Prise d'objet : une pompe (cellule Solid) en (1,0) raccorde le tuyau posé en (1,1) → bouchon côté −y.
    const hp = api.normalizePlan({ mode: 'house', grid: { width: 3, depth: 3 }, architecture: { height: 3, ops: [{ id: 'p', kind: 'cells', cells: [1, 1, 1], material: 'CopperPipeItem', form: 'CopperPipe' }] },
        levels: [{ height: 2, objects: [{ id: 'o', type: 'Pump', x: 1, y: 0, rotation: 0 }] }] });
    const hs = { plan: hp, selection: null, palette: { secondary: '#ffb74d', text: '#ffffff', primary: '#64b5f6' }, matRgb: {}, materialsByName: {}, icons: { Pump: false },
        objectsByName: { Pump: { cells: [[0, 0, 0, 2]], size: [1, 1, 1] } }, layer: 0, view3d: { cap: false, forms: loaded, atlas: { pending: {}, slots: {}, next: 0 } } };
    hs.vox = api.evalOps(hp, 3);
    api.view3dBuild(hs);
    eq('prise Solid collectée par v3dObjectCells', Array.from(api.v3dObjectCells(hs).slots), [1 + 3 * (0 + 3 * 1)]);
    eq('tuyau contre la prise : bouchon', api.formsPick(pipes, api.shapeCode({ form: 'CopperPipe' }), hs.vox, 1, 1, 1).mesh === pipes.forms.CopperPipe.cases[2].mesh, true);
}

// ---- 11. tuyau à travers un mur : le bloc traversé reste dessous (vox.under) et ses voisins le voient ------------
{
    // Mur le long de x sur (0..2, 0), tuyau le long de y sur (1, 0..1) qui le traverse en (1, 0).
    const v = api.evalOps(plan(3, 2, 1, [
        { id: 'w', kind: 'box', a: [0, 0, 0], b: [2, 0, 0], material: 'M', form: 'Wall' },
        { id: 'p', kind: 'box', a: [1, 0, 0], b: [1, 1, 0], material: 'CopperPipeItem', form: 'CopperPipe' },
    ]), 1);
    const u = v.under.get(1);
    eq('under : le mur sous le tuyau, rien sous le tuyau posé dans le vide', [!!u && v.palette[u[0] - 1], !!u && u[1] === api.shapeCode({ form: 'Wall' }), u && u[2], v.under.has(1 + 3)], ['M', true, 1, false]);
    eq('la case reste un tuyau pour le reste du planner', v.palette[v.cells[1] - 1], 'CopperPipeItem');
    eq('le mur voisin voit le mur (même type), pas le tuyau', api.formsNeighbor({ forms: {}, categories: {} }, 'sameType', v, 0, 0, 0, [1, 0, 0, 1]), true);
    eq('le tuyau voit toujours le tuyau', api.formsNeighbor({ forms: {}, categories: {} }, 'type:CopperPipe', v, 1, 1, 0, [0, -1, 0, 1]), true);
    const v2 = api.evalOps(plan(3, 2, 1, [
        { id: 'w', kind: 'box', a: [0, 0, 0], b: [2, 0, 0], material: 'M', form: 'Wall' },
        { id: 'p', kind: 'box', a: [1, 0, 0], b: [1, 1, 0], material: 'CopperPipeItem', form: 'CopperPipe' },
        { id: 'q', kind: 'box', a: [1, 0, 0], b: [1, 0, 0], material: 'N' },
    ]), 1);
    eq('un bloc posé ensuite efface le souvenir du mur', v2.under.has(1), false);
}

// ---- 12. pavé / cylindre creux fermé aux deux bouts (op.closed, miroir d'ArchShapes.Shrunk) ----------------------------
{
    const count = function (op) { const v = api.evalOps(plan(5, 5, 5, [op]), 5); let n = 0; v.cells.forEach(function (c) { if (c) n++; }); return n; };
    const box = { id: 'b', kind: 'box', a: [0, 0, 0], b: [4, 4, 4], material: 'M', hollow: true, thickness: 1 };
    // 5³ = 125 ; ouvert : creux 3×3 sur les 5 couches (45) → 80 ; fermé : creux 3×3×3 (27) → 98.
    eq('pavé creux 5×5×5 t=1 : ouvert 80, fermé 98', [count(box), count(Object.assign({}, box, { closed: true }))], [80, 98]);
    eq('closed sans hollow : pavé plein', count(Object.assign({}, box, { hollow: false, closed: true })), 125);
    // Disque Ø5 = 21 cases, Ø3 intérieur = 9 : ouvert 12 × 5 = 60 ; fermé 21 × 5 − 9 × 3 = 78.
    const cyl = { id: 'c', kind: 'cylinder', axis: 'z', a: [0, 0, 0], b: [4, 4, 4], material: 'M', hollow: true, thickness: 1 };
    eq('cylindre creux Ø5 h5 t=1 : ouvert 60, fermé 78', [count(cyl), count(Object.assign({}, cyl, { closed: true }))], [60, 78]);
    eq('cylindre couché (axe x) fermé : mêmes comptes', count(Object.assign({}, cyl, { axis: 'x', closed: true })), 78);
}

// ---- 13. solidAt / detectPockets : pièces = poches d'air fermées, 6-connexes ------------------------------------------
{
    const vox = function (W, H, ops) { return api.evalOps(plan(W, W, H, ops), H); };
    const room = function (a, b, extra) { return Object.assign({ id: 'r' + a.join('') + b.join(''), kind: 'box', a: a, b: b, material: 'M', hollow: true, thickness: 1, closed: true }, extra || {}); };
    const v = vox(3, 3, [{ id: 'a', kind: 'cells', cells: [1, 1, 1], material: 'M' }, { id: 's', kind: 'cells', cells: [2, 2, 0], subtract: true }]);
    eq('solidAt : sous la grille, hors grille z 0 / 1, terrain intact, terrain creusé, bloc, air, au-dessus de la grille',
        [api.solidAt(v, 0, 0, -1), api.solidAt(v, -1, 0, 0), api.solidAt(v, -1, 0, 1), api.solidAt(v, 0, 0, 0), api.solidAt(v, 2, 2, 0), api.solidAt(v, 1, 1, 1), api.solidAt(v, 0, 0, 1), api.solidAt(v, 0, 0, 3)],
        [true, true, false, true, false, true, false, false]);
    eq('solidAt : cellule Wall d\'un objet (porte)', api.solidAt(v, 0, 0, 1, new Set([0 + 3 * (0 + 3 * 1)])), true);

    let P = api.detectPockets(vox(7, 8, [room([1, 1, 1], [5, 5, 5])]));
    eq('boîte fermée 5×5×5 : une poche de 27', [P.skipped, P.pockets.length, P.pockets[0] && P.pockets[0].count], [false, 1, 27]);
    eq('graine : couche la plus basse, au centre', [P.pockets[0].minZ, P.pockets[0].seed], [2, { x: 3, y: 3, z: 2 }]);
    eq('labels : poche, plein, extérieur (au-dessus, à côté)', [P.labels[(3 - P.x0) + P.nx * ((3 - P.y0) + P.ny * 3)], P.labels[(1 - P.x0) + P.nx * ((3 - P.y0) + P.ny * 3)], P.labels[(3 - P.x0) + P.nx * ((3 - P.y0) + P.ny * 6)], P.labels[0 + P.nx * (0 + P.ny * 2)]], [1, 0, -1, -1]);
    P = api.detectPockets(vox(7, 8, [room([1, 1, 1], [5, 5, 5], { closed: false })]));
    eq('boîte ouverte (sans dessus ni dessous) : aucune poche', P.pockets.length, 0);
    // Murs creux posés sur le terrain + toit : le terrain ferme la poche par dessous (3×3×4).
    P = api.detectPockets(vox(7, 8, [room([1, 1, 1], [5, 5, 4], { closed: false }), { id: 't', kind: 'box', a: [1, 1, 5], b: [5, 5, 5], material: 'M' }]));
    eq('murs sur le terrain + toit : poche de 36, graine à z 1', [P.pockets.length, P.pockets[0] && P.pockets[0].count, P.pockets[0] && P.pockets[0].seed.z], [1, 36, 1]);
    P = api.detectPockets(vox(7, 12, [room([1, 1, 1], [5, 5, 5]), room([1, 1, 5], [5, 5, 9]), { id: 'h', kind: 'cells', cells: [3, 3, 5], subtract: true }]));
    eq('deux boîtes empilées reliées par un trou de dalle : une poche de 55', [P.pockets.length, P.pockets[0] && P.pockets[0].count, P.pockets[0] && P.pockets[0].minZ], [1, 55, 2]);
    P = api.detectPockets(vox(7, 12, [room([1, 1, 1], [5, 5, 5]), room([1, 1, 5], [5, 5, 9])]));
    eq('les mêmes sans trou : deux poches', P.pockets.map(function (p) { return p.count; }), [27, 27]);
    P = api.detectPockets(vox(6, 6, [room([1, 1, 1], [4, 3, 3])]));
    eq('poche de 2 cases : aucune pièce, étiquetée extérieur', [P.pockets.length, P.labels[(2 - P.x0) + P.nx * ((2 - P.y0) + P.ny * 2)]], [0, -1]);
    // Deux poches de 3 cases qui ne se touchent que par un coin : la diagonale ne les relie pas.
    P = api.detectPockets(vox(8, 7, [{ id: 'b', kind: 'box', a: [1, 1, 1], b: [6, 4, 4], material: 'M' },
        { id: 's', kind: 'cells', cells: [2, 2, 2, 2, 3, 2, 3, 2, 2, 4, 3, 3, 5, 3, 3, 5, 2, 3], subtract: true }]));
    eq('poches reliées par une diagonale seulement : deux poches', P.pockets.map(function (p) { return p.count; }), [3, 3]);
    // Boîte percée d'une embrasure : ouverte ; la cellule Wall d'une porte la referme.
    const pierced = vox(7, 8, [room([1, 1, 1], [5, 5, 5]), { id: 'd', kind: 'cells', cells: [1, 3, 2], subtract: true }]);
    eq('embrasure ouverte / fermée par une porte', [api.detectPockets(pierced).pockets.length, api.detectPockets(pierced, new Set([1 + 7 * (3 + 7 * 2)])).pockets.length], [0, 1]);
    eq('sans op : rien', api.detectPockets(vox(4, 3, [])).pockets.length, 0);
    P = api.detectPockets(api.evalOps(plan(200, 200, 60, [{ id: 'c', kind: 'cells', cells: [0, 0, 1, 199, 199, 59], material: 'M' }]), 60));
    eq('garde : boîte de plus de 2 000 000 cases → skipped, rien de calculé', [P.skipped, P.pockets.length, P.labels], [true, 0, null]);
}

// ---- 14. normalizePlan : un v3 égaré est toléré (le C# migre avant setPlan) ------------------------------------------
{
    const n = api.normalizePlan({ schemaVersion: 3, mode: 'house', grid: { width: 5, depth: 5 }, defaults: { wallHeight: 4, floorMaterial: 'A', ceilingMaterial: 'B' },
        levels: [{ name: 'RdC', height: null, walls: { '1,1': { material: 'A' } }, floors: { '1,1': 'A' }, holes: { '2,2': true },
            rooms: [{ id: 'r1', name: 'R', seed: { x: 2, y: 2 }, height: 3, ceilingMaterial: 'B', lockCategory: null }], objects: [] }],
        architecture: { height: 20, ops: [] } });
    const l = n.levels[0];
    eq('v3 → schéma 4, defaults { wallHeight }', [n.schemaVersion, n.defaults], [4, { wallHeight: 4 }]);
    eq('v3 : niveau sans murs / sols / trous', ['walls' in l, 'floors' in l, 'holes' in l], [false, false, false]);
    eq('v3 : pièce sans hauteur ni plafond, seed.z = 1', l.rooms[0], { id: 'r1', name: 'R', seed: { x: 2, y: 2, z: 1 }, lockCategory: null });
    eq('seed.z présent : conservé', api.normalizePlan({ levels: [{ rooms: [{ id: 'r', seed: { x: 0, y: 0, z: 0 } }], objects: [] }] }).levels[0].rooms[0].seed.z, 0);
    eq('plan vide : schéma 4, niveau { name, height, rooms, objects }', [api.normalizePlan(null).schemaVersion, Object.keys(api.normalizePlan(null).levels[0])], [4, ['name', 'height', 'rooms', 'objects']]);
}

// ---- 15. ownerAt (sélection) : jamais une soustraction, rien sur une case vide ----------------------------------------
{
    const p = plan(3, 1, 1, [{ id: 'b', kind: 'box', a: [0, 0, 0], b: [2, 0, 0], material: 'M' }, { id: 's', kind: 'cells', cells: [1, 0, 0], subtract: true },
        { id: 'c', kind: 'cells', cells: [2, 0, 0], material: 'N' }]);
    const st = { plan: p, vox: api.evalOps(p, 1) };
    const id = function (x) { const op = api.ownerAt(st, x, 0, 0); return op ? op.id : null; };
    eq('ownerAt : bloc du pavé, trou creusé → rien, bloc reposé par-dessus, hors grille', [id(0), id(1), id(2), id(3)], ['b', null, 'c', null]);
}

// ---- 16. Palette de Structure : forme de pose des nouveaux volumes, R / Q, repli au changement de matériau ------------
{
    const st = { shape: { height: 1, hollow: false, thickness: 1 }, layer: 1, material: 'M', form: 'Stairs', formRot: 1, plan: plan(6, 6, 4, []) };
    const drag = function (tool, subtract) { return { tool: tool, start: { x: 1, y: 1 }, subtract: !!subtract }; };
    const box = api.shapeOpFromDrag(st, drag('box'), { x: 3, y: 2 });
    eq('pavé : porte form/rot de la palette', [box.form, box.rot], ['Stairs', 1]);
    eq('ligne, sphère : idem', [api.shapeOpFromDrag(st, drag('line'), { x: 3, y: 1 }).form, api.shapeOpFromDrag(st, drag('sphere'), { x: 2, y: 1 }).rot], ['Stairs', 1]);
    const sub = api.shapeOpFromDrag(st, drag('box', true), { x: 3, y: 2 }), room = api.shapeOpFromDrag(st, drag('room'), { x: 4, y: 4 });
    eq('soustraction et pièce : cubes, sans champ form/rot', ['form' in sub, 'rot' in sub, 'form' in room, 'rot' in room], [false, false, false, false]);
    st.form = 'Cube'; st.formRot = 0;
    const cube = api.shapeOpFromDrag(st, drag('box'), { x: 3, y: 2 });
    eq('Cube : champs omis', ['form' in cube, 'rot' in cube], [false, false]);
    eq('shapeCode du volume posé = code de la palette', api.shapeCode(box), 7 * 4 + 1);
    const g = { axis: 'x', dir: 1, index: 2, view: { cols: 6, rows: 4 } };
    st.form = 'RoofSide'; st.formRot = 3;
    const side = api.sideShapeOp(st, { tool: 'box', geom: g, start: { c: 1, z: 0 }, subtract: false }, { c: 2, z: 1 });
    eq('dessin en coupe : porte form/rot', [side.form, side.rot], ['RoofSide', 3]);
    eq('applyForm : cells du crayon', api.applyForm({ kind: 'cells', subtract: false, material: 'M', cells: [0, 0, 0] }, 'Wall', 6), { kind: 'cells', subtract: false, material: 'M', cells: [0, 0, 0], form: 'Wall', rot: 2 });
    eq('applyForm : soustraction nettoyée', api.applyForm({ kind: 'cells', subtract: true, cells: [], form: 'Wall', rot: 1 }, 'Wall', 1), { kind: 'cells', subtract: true, cells: [] });
    // R = rot + 1, Q = rot − 1, en boucle sur 0..3.
    eq('R : 0→1→2→3→0', [0, 1, 2, 3].map(function (r) { return api.rotStep(r, 1); }), [1, 2, 3, 0]);
    eq('Q : 0→3→2→1→0', [0, 3, 2, 1].map(function (r) { return api.rotStep(r, -1); }), [3, 2, 1, 0]);
    eq('rot absent → R donne 1', api.rotStep(undefined, 1), 1);
    // Pipette : décodage du code de vox.shapes.
    eq('decodeShape', [api.decodeShape(0), api.decodeShape(api.shapeCode({ form: 'Stairs', rot: 2 })), api.decodeShape(api.shapeCode({ form: 'Cube', rot: 3 }))],
        [{ form: 'Cube', rot: 0 }, { form: 'Stairs', rot: 2 }, { form: 'Cube', rot: 0 }]);
    // Repli au changement de matériau : mémorisée > même nom > Cube ; rot gardé si la forme retenue se tourne.
    const forms = [{ name: 'Cube', rotatable: false }, { name: 'Wall', rotatable: false }, { name: 'Stairs', rotatable: true }];
    eq('forme mémorisée pour ce matériau', api.formForMaterial(forms, 'Stairs', 'Wall', 2), { form: 'Stairs', rot: 2 });
    eq('même forme si le matériau la possède (non tournable : rot 0)', api.formForMaterial(forms, null, 'Wall', 2), { form: 'Wall', rot: 0 });
    eq('mémorisée absente du matériau : même forme', api.formForMaterial(forms, 'RoofSide', 'Stairs', 3), { form: 'Stairs', rot: 3 });
    eq('forme absente : Cube', api.formForMaterial(forms, null, 'RoofSide', 1), { form: 'Cube', rot: 0 });
    eq('matériau sans formes : Cube', api.formForMaterial([], 'Stairs', 'Stairs', 1), { form: 'Cube', rot: 0 });
}

// ---- Vue de dessus : icône de forme et côté bas (rot 0..3 : est, nord, ouest, sud ; y vers le bas de l'écran) ----------
{
    eq('shapeInfo : 0 et Cube → null', [api.shapeInfo(0), api.shapeInfo(api.shapeCode({ form: 'Cube', rot: 2 }))], [null, null]);
    eq('shapeInfo : Stairs rot 1', api.shapeInfo(api.shapeCode({ form: 'Stairs', rot: 1 })), { form: 'Stairs', rot: 1, icon: 'StairsForm', low: [0, -1] });
    eq('shapeInfo : côtés bas rot 0..3', [0, 1, 2, 3].map(function (r) { return api.shapeInfo(api.shapeCode({ form: 'RoofSide', rot: r })).low; }),
        [[1, 0], [0, -1], [-1, 0], [0, 1]]);
    check('shapeInfo : même objet (cache)', api.shapeInfo(29) === api.shapeInfo(29));
    // Case de 20 px en (100, 200) : la pointe touche le bord du côté bas (2 px de marge), la base est vers le centre.
    const tip = function (r) { return api.lowSideTriangle({ x: 100, y: 200 }, 20, api.shapeInfo(api.shapeCode({ form: 'Stairs', rot: r })).low)[0]; };
    eq('triangle : pointe est, nord, ouest, sud', [0, 1, 2, 3].map(tip), [[118, 210], [110, 202], [102, 210], [110, 218]]);
    const t = api.lowSideTriangle({ x: 0, y: 0 }, 20, [0, -1]);
    eq('triangle nord : base sous la pointe, symétrique', [t[1][1], t[2][1], t[1][0] + t[2][0]], [6, 6, 20]);
}

// ---- Vue 3D : lancer de rayon, pixel → rayon, zoom vers le curseur, pivot sans saut -----------------------------------
{
    // Grille 6×5×4 : bloc de l'op 'a' en (2, 1, 0), bloc de 'b' en (2, 1, 2), objet en (4, 3, 0).
    const p = plan(6, 5, 4, [
        { id: 'a', kind: 'box', a: [2, 1, 0], b: [2, 1, 0], material: 'M' },
        { id: 'b', kind: 'box', a: [2, 1, 2], b: [2, 1, 2], material: 'M' },
    ]);
    const vox = api.evalOps(p, 4), objects = new Map([[4 + 6 * (3 + 5 * 0), { id: 'o1' }]]);
    const down = api.voxRaycast(vox, [2.5, 1.5, 10], [0, 0, -1]);
    eq('rayon vertical : premier bloc touché (haut), face +z', [down.x, down.y, down.z, down.normal, down.t, down.owner], [2, 1, 2, [0, 0, 1], 7, 2]);
    const capped = api.voxRaycast(vox, [2.5, 1.5, 10], [0, 0, -1], { cap: 1 });
    eq('cap = 1 : le bloc du haut est ignoré', [capped.z, capped.owner, capped.t], [0, 1, 9]);
    const clipped = api.voxRaycast(vox, [2.5, 1.5, 10], [0, 0, -1], { clip: function (x, y, z) { return z >= 2; } });
    eq('clip : case masquée traversée', [clipped.z, clipped.owner], [0, 1]);
    eq('manqué → null', api.voxRaycast(vox, [0.5, 0.5, 10], [0, 0, -1]), null);
    eq('rayon hors de la grille → null', api.voxRaycast(vox, [-5, 0.5, 0.5], [0, 1, 0]), null);
    const side = api.voxRaycast(vox, [-3, 1.5, 0.5], [1, 0, 0]);
    eq('rayon horizontal : face −x, t', [side.x, side.normal, side.t], [2, [-1, 0, 0], 5]);
    const back = api.voxRaycast(vox, [9, 1.5, 0.5], [-1, 0, 0]);
    eq('depuis +x : face +x', [back.x, back.normal, back.t], [2, [1, 0, 0], 6]);
    const diag = api.voxRaycast(vox, [0.2, 0.3, 3.5], [2.3, 1.2, -1].map(function (c, i, a) { return c / Math.hypot(a[0], a[1], a[2]); }));
    check('rayon oblique : touche la case du bloc du haut', diag && diag.x === 2 && diag.y === 1 && diag.z === 2, diag);
    const obj = api.voxRaycast(vox, [4.5, 3.5, 3.5], [0, 0, -1], { objects: objects });
    eq('objet touché', [obj.object && obj.object.id, obj.owner, obj.z], ['o1', 0, 0]);
    eq('objet ignoré sans opts.objects', api.voxRaycast(vox, [4.5, 3.5, 3.5], [0, 0, -1]), null);
    const inside = api.voxRaycast(vox, [2.5, 1.5, 0.5], [0, 0, 1]);
    eq('origine dans un bloc : t = 0, normale nulle', [inside.z, inside.t, inside.normal], [0, 0, [0, 0, 0]]);

    // Pixel → rayon → projection : aller-retour (le rendu inverse y, v3dProject passe par la matrice du rendu).
    const W = 800, H = 600, aspect = W / H, px = function (n) { return [(n[0] + 1) / 2 * W, (1 - n[1]) / 2 * H]; };
    const dpx = function (a, b) { const u = px(a), w = px(b); return Math.hypot(u[0] - w[0], u[1] - w[1]); };
    const view = function () { return { target: [10, 7, 2], dist: 30, yaw: -0.6, pitch: 0.55 }; };
    let worst = 0;
    [[0, 0], [0.5, -0.3], [-0.8, 0.7], [0.95, 0.95]].forEach(function (n) {
        const v = view(), r = api.v3dRay(v, aspect, n[0], n[1]);
        const q = [0, 1, 2].map(function (a) { return r.origin[a] + r.dir[a] * 17; });
        worst = Math.max(worst, dpx(api.v3dProject(v, aspect, q), n));
    });
    check('pixel → rayon → pixel (< 0,01 px)', worst < 0.01, worst);
    const c0 = api.v3dProject(view(), aspect, [10, 7, 2]);
    check('la cible est au centre', Math.abs(c0[0]) < 1e-9 && Math.abs(c0[1]) < 1e-9, c0);
    const b0 = api.v3dBasis(view()), r0 = api.v3dProject(view(), aspect, [10 + b0.right[0], 7 + b0.right[1], 2 + b0.right[2]]);
    check('droite de la caméra → droite de l’écran (inversion y prise en compte)', r0[0] > 0 && Math.abs(r0[1]) < 1e-9, r0);

    // Zoom vers le curseur : le point visé reste au même pixel (1 px), la cible et la distance se rapprochent.
    [[0.6, -0.4, 1 / 1.12], [-0.7, 0.5, 1.12], [0.2, 0.9, 1 / 1.12]].forEach(function (c) {
        const v = view(), r = api.v3dRay(v, aspect, c[0], c[1]);
        const P = [0, 1, 2].map(function (a) { return r.origin[a] + r.dir[a] * 22; });
        const before = api.v3dProject(v, aspect, P), t0 = v.target.slice();
        api.v3dZoomAt(v, P, c[2]);
        const d0 = Math.hypot(t0[0] - P[0], t0[1] - P[1], t0[2] - P[2]), d1 = Math.hypot(v.target[0] - P[0], v.target[1] - P[1], v.target[2] - P[2]);
        check('zoom ×' + c[2].toFixed(3) + ' : P au même pixel', dpx(before, api.v3dProject(v, aspect, P)) < 1, dpx(before, api.v3dProject(v, aspect, P)));
        check('zoom ×' + c[2].toFixed(3) + ' : distance et écart cible–P × f', Math.abs(v.dist - 30 * c[2]) < 1e-9 && Math.abs(d1 - d0 * c[2]) < 1e-9, [v.dist, d0, d1]);
    });

    // Orbite autour de P (vraie orbite) : P reste au même pixel (< 1 px) et à la même distance de l'œil ; lacet et tangage
    // avancent du delta demandé, tangage borné à 0,09..1,55 ; sans P distinct (P = cible), la cible ne bouge pas.
    [[0.5, -0.3, 0.2, 0.1], [-0.8, 0.6, -0.4, -0.2], [0.1, 0.1, 1.3, 0.35], [0.7, 0.4, -2.1, 0.9]].forEach(function (c, k) {
        const v = view(), r = api.v3dRay(v, aspect, c[0], c[1]);
        const P = [0, 1, 2].map(function (a) { return r.origin[a] + r.dir[a] * 14; });
        const before = api.v3dProject(v, aspect, P), e0 = api.v3dBasis(v).eye, pitch0 = v.pitch, yaw0 = v.yaw;
        const dist = function (e) { return Math.hypot(e[0] - P[0], e[1] - P[1], e[2] - P[2]); }, r0 = dist(e0);
        api.v3dOrbit(v, P, c[2], c[3]);
        const moved = dpx(before, api.v3dProject(v, aspect, P)), r1 = dist(api.v3dBasis(v).eye);
        const wantPitch = Math.max(0.09, Math.min(1.55, pitch0 + c[3]));
        check('orbite ' + k + ' : P au même pixel (< 1 px)', moved < 1, moved);
        check('orbite ' + k + ' : œil à la même distance de P, dist inchangée', Math.abs(r1 - r0) < 1e-9 && v.dist === 30, [r0, r1, v.dist]);
        check('orbite ' + k + ' : lacet + Δ, tangage + δ borné', Math.abs(v.yaw - yaw0 - c[2]) < 1e-12 && Math.abs(v.pitch - wantPitch) < 1e-12, [v.yaw, v.pitch]);
    });
    {
        const v = view();
        api.v3dOrbit(v, v.target.slice(), 0.4, 0.2);
        eq('orbite autour de la cible : cible fixe', v.target, [10, 7, 2]);
    }

    // Coupe face caméra : plan vertical par cutAt, normale horizontale vers l'œil (lacet) ; côté caméra masqué.
    {
        const v = { target: [10, 7, 2], cutAt: [10, 7, 2], dist: 30, yaw: 0, pitch: 0.55, cutaway: false };   // lacet 0 : œil au sud (y plan croissant)
        check('coupe éteinte : rien de masqué', !api.v3dCutHidden(v, 10.5, 12.5, 2.5) && !api.v3dCutHidden(v, 10.5, 1.5, 2.5));
        eq('coupe éteinte : plan neutre', api.v3dCutPlane(v), [0, 0, 0, -1]);
        v.cutaway = true;
        check('case entre la cible et l’œil : masquée', api.v3dCutHidden(v, 10.5, 12.5, 2.5));
        check('case derrière la cible : visible', !api.v3dCutHidden(v, 10.5, 1.5, 2.5));
        check('masquage indépendant de la hauteur et du côté', api.v3dCutHidden(v, 0.5, 7.5, 40.5) && !api.v3dCutHidden(v, 30.5, 6.5, 0.5));
        const e = api.v3dBasis(v).eye;
        check('l’œil est du côté masqué', api.v3dCutHidden(v, e[0], e[1], e[2]));
        v.yaw = Math.PI / 2;   // œil à l'est (x croissant) : le plan tourne avec le lacet
        check('lacet π/2 : la case sud devient visible, la case est masquée', !api.v3dCutHidden(v, 9.5, 12.5, 2.5) && api.v3dCutHidden(v, 13.5, 7.5, 2.5) && !api.v3dCutHidden(v, 6.5, 7.5, 2.5));
        v.target = [0, 7, 2];   // la cible seule ne déplace pas le plan (orbite autour d'un point visé)
        check('cible déplacée : le plan reste sur cutAt', api.v3dCutHidden(v, 13.5, 7.5, 2.5) && !api.v3dCutHidden(v, 6.5, 7.5, 2.5));
        v.cutAt = [20, 7, 2];
        check('cutAt déplacé : le plan le suit', !api.v3dCutHidden(v, 13.5, 7.5, 2.5) && api.v3dCutHidden(v, 21.5, 7.5, 2.5));
        // Lancer de rayon avec la coupe (comme view3dPick) : le bloc devant le plan est traversé.
        const pv = plan(6, 5, 2, [
            { id: 'a', kind: 'box', a: [2, 4, 0], b: [2, 4, 0], material: 'M' },
            { id: 'b', kind: 'box', a: [2, 1, 0], b: [2, 1, 0], material: 'M' },
        ]);
        const vx = api.evalOps(pv, 2), cv = { target: [2.5, 2.5, 0.5], cutAt: [2.5, 2.5, 0.5], dist: 20, yaw: 0, pitch: 0.3, cutaway: true };
        const clip = function (x, y, z) { return api.v3dCutHidden(cv, x + 0.5, y + 0.5, z + 0.5); };
        const h0 = api.voxRaycast(vx, [2.5, 9, 0.5], [0, -1, 0]), h1 = api.voxRaycast(vx, [2.5, 9, 0.5], [0, -1, 0], { clip: clip });
        eq('rayon sans coupe : bloc avant (y 4)', [h0.y, h0.owner], [4, 1]);
        eq('rayon avec coupe : bloc arrière (y 1)', [h1.y, h1.owner], [1, 2]);
    }
}

// ---- Déplacement d'un objet (glisser, flèches en Aménagement / 3D) ------------------------------------------
{
    const objs = [{ id: 't', x: 2, y: 3 }, { id: 'p', x: 2, y: 3, attachedTo: 't' }, { id: 'o', x: 5, y: 5 }];
    api.shiftObject(objs, 't', 1, 0);
    eq('flèche droite : objet + posé dessus, pas les autres', objs.map(function (o) { return [o.x, o.y]; }), [[3, 3], [3, 3], [5, 5]]);
    api.shiftObject(objs, 'p', 0, -1);
    eq('objet posé : lui seul bouge', objs.map(function (o) { return [o.x, o.y]; }), [[3, 3], [3, 2], [5, 5]]);
    const g = { width: 6, depth: 4 };
    check('pas dans la grille : accepté', api.canShiftObject({ x: 0, y: 0 }, 1, 0, g) && api.canShiftObject({ x: 5, y: 3 }, -1, 0, g));
    check('pas hors de la grille : refusé', !api.canShiftObject({ x: 0, y: 2 }, -1, 0, g) && !api.canShiftObject({ x: 5, y: 2 }, 1, 0, g)
        && !api.canShiftObject({ x: 2, y: 0 }, 0, -1, g) && !api.canShiftObject({ x: 2, y: 3 }, 0, 1, g));
}

// ---- Flèches relatives à la caméra en 3D, objet monté par son Z ------------------------------------------------
{
    const aspect = 4 / 3, view = function (yaw) { return { target: [10, 7, 2], dist: 30, yaw: yaw, pitch: 0.55 }; };
    const keys = ['ArrowUp', 'ArrowDown', 'ArrowRight', 'ArrowLeft'];
    // Lacet par défaut (−0,6) puis orbite de 90°, 180°, 270° : ↑ / → attendus en repère du plan (y vers le sud).
    [[0, [0, -1], [1, 0]], [1, [-1, 0], [0, -1]], [2, [0, 1], [-1, 0]], [3, [1, 0], [0, 1]]].forEach(function (c) {
        const v = view(-0.6);
        api.v3dOrbit(v, v.target.slice(), c[0] * Math.PI / 2, 0);
        const s = {}; keys.forEach(function (k) { s[k] = api.cameraArrowStep(v.yaw, k); });
        eq('lacet −0,6 + ' + (c[0] * 90) + '° : ↑ et →', [s.ArrowUp, s.ArrowRight], [c[1], c[2]]);
        eq('lacet −0,6 + ' + (c[0] * 90) + '° : ↓ et ← opposés', [s.ArrowDown, s.ArrowLeft], [[-c[1][0], -c[1][1]], [-c[2][0], -c[2][1]]].map(function (p) { return p.map(function (n) { return n || 0; }); }));
        // Vérification par le rendu : ↑ éloigne de l'œil et monte à l'écran, → part à droite de l'écran.
        const T = v.target, e = api.v3dBasis(v).eye, d = function (p) { return Math.hypot(p[0] - e[0], p[1] - e[1], p[2] - e[2]); };
        const up = [T[0] + s.ArrowUp[0], T[1] + s.ArrowUp[1], T[2]], right = [T[0] + s.ArrowRight[0], T[1] + s.ArrowRight[1], T[2]];
        const p0 = api.v3dProject(v, aspect, T), pu = api.v3dProject(v, aspect, up), pr = api.v3dProject(v, aspect, right);
        check('lacet −0,6 + ' + (c[0] * 90) + '° : ↑ s’éloigne de l’œil et monte à l’écran', d(up) > d(T) && pu[1] > p0[1], [d(up), d(T), pu, p0]);
        check('lacet −0,6 + ' + (c[0] * 90) + '° : → va à droite de l’écran', pr[0] - p0[0] > Math.abs(pr[1] - p0[1]), [pr, p0]);
    });
    // Près de la diagonale : l'axe le plus proche de la visée l'emporte, → reste perpendiculaire à ↑.
    eq('lacet π/4 + 0,01 : visée −x', [api.cameraArrowStep(Math.PI / 4 + 0.01, 'ArrowUp'), api.cameraArrowStep(Math.PI / 4 + 0.01, 'ArrowRight')], [[-1, 0], [0, -1]]);
    eq('lacet π/4 − 0,01 : visée −y', [api.cameraArrowStep(Math.PI / 4 - 0.01, 'ArrowUp'), api.cameraArrowStep(Math.PI / 4 - 0.01, 'ArrowRight')], [[0, -1], [1, 0]]);
    eq('lacet −3π/4 ± 0,01 : visée +x / +y', [api.cameraArrowStep(-3 * Math.PI / 4 + 0.01, 'ArrowUp'), api.cameraArrowStep(-3 * Math.PI / 4 - 0.01, 'ArrowUp')], [[1, 0], [0, 1]]);
    eq('lacet 5π (tours complets) = π', api.cameraArrowStep(5 * Math.PI, 'ArrowUp'), [0, 1]);
    eq('autre touche → null', api.cameraArrowStep(0, 'PageUp'), null);
    // Objet monté (Z explicite) : l'objet posé dessus suit son support.
    const hp = api.normalizePlan({ mode: 'house', grid: { width: 4, depth: 4 }, architecture: { height: 6, ops: [] },
        levels: [{ height: 4, objects: [{ id: 't', type: 'T', x: 1, y: 1, z: 3 }, { id: 'c', type: 'C', x: 1, y: 1, attachedTo: 't' }] }] });
    const hs = { plan: hp, objectsByName: { T: { cells: [[0, 0, 0, 1]], size: [1, 1, 1] }, C: { cells: [[0, 0, 0, 1]], size: [1, 1, 1] } } };
    hs.vox = api.evalOps(hp, 6);
    const oc = api.v3dObjectCells(hs), zOf = function (id) { let z = null; oc.forEach(function (r) { if (r.id === id) z = r.z; }); return z; };
    eq('support à Z 3 : case 3, objet posé dessus en 4', [zOf('t'), zOf('c')], [3, 4]);
}

console.log(pass + ' passed, ' + fail + ' failed');
process.exit(fail ? 1 : 0);
