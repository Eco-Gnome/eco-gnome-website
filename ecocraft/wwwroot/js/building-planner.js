// Îlot Canvas2D du planificateur de bâtiment. Le JS est la source de vérité du plan pendant l'édition
// (dessin, drag, zoom/pan, historique) et renvoie le document complet à Blazor à chaque commit ;
// Blazor analyse (règles du jeu) et renvoie le résultat, affiché ici en surimpression.
// Axes : x → colonne, y → ligne (Eco Z) ; z = hauteur (Eco Y). Tout le bâti est une liste ordonnée de formes 3D
// (plan.architecture.ops) évaluées ici en blocs unitaires (evalOps, même spécification qu'ArchShapes.cs), sur le
// terrain (couche 0). Mode Structure (plan.mode === 'architecture') : on affiche et édite une couche z à la fois
// (st.layer), avec une bande d'élévation à gauche et, à la demande, une vue en coupe. Mode Aménagement ('house') :
// mobilier seul, sur une pile de niveaux dont un seul est affiché à la fois (st.level, tranche de couches).
// Les pièces sont les poches d'air fermées du bâti (detectPockets, 6-connexes comme le jeu) : chaque poche porte une
// pièce (reconcileRooms), dont la graine reste un détail interne pour le schéma et l'analyse C#. Seule l'analyse C#
// (RoomChecker) connaît les diagonales : ses arêtes vides (tier 0) sont affichées telles quelles.
window.ecoBuildingPlanner = (function () {
    'use strict';

    const instances = {};
    const CELL = 26;
    const MAX_HISTORY = 100;
    const MAX_PLAN_FILE_BYTES = 1536 * 1024;   // import JSON : le document traverse SignalR (limite 2 Mo côté serveur)
    const KIND = { OCCUPIED: 0, WALL: 1, SOLID: 2, WATER: 3, NONE: 4 };
    const TIER_COLORS = ['#7d7d7d', '#c2a26a', '#8fa3b5', '#b0784a', '#6f8f9c', '#d4af37'];   // repli des matériaux absents du catalogue (blocs de mods)
    const MAX_ARCH_HEIGHT = 320;   // = PlanValidator.MaxArchitectureHeight
    const MAX_LEVELS = 20;         // = PlanValidator.MaxLevels
    const MAX_LEVEL_HEIGHT = 50;   // = PlanValidator.MaxHeight
    const ICON_MIN_CELL = 22;      // taille de case à partir de laquelle l'icône du bloc reste lisible
    const ICON_MIN_CELL_HOUSE = 67;   // plan de maison : cinq crans de zoom (x 1.25) plus loin, sinon les icônes noient le plan
    const LOW_MIN_CELL = 10;       // taille de case à partir de laquelle le triangle du côté bas d'une forme tournée se dessine
    const LEGEND_BTN = 16;         // côté du chevron qui replie / rouvre la légende

    function emptyLevel() {
        return { name: '', height: null, rooms: [], objects: [] };
    }

    function emptyPlan(width, depth) {
        return {
            schemaVersion: 4,
            name: '',
            mode: 'house',
            grid: { width: width || 25, depth: depth || 20 },
            defaults: { wallHeight: 3 },
            levels: [emptyLevel()],
            groundIndex: 0,
            analysis: { residents: 1, propertyType: 'Residence' },
            architecture: { height: 20, ops: [], image: null },
        };
    }

    // Schéma 4. Un document plus ancien est migré par le C# (PlanDocumentJson.Parse matérialise murs, sols et plafonds
    // en ops) avant d'arriver ici : un v3 qui passerait quand même est seulement toléré (ses champs retirés sont jetés).
    function normalizePlan(plan) {
        plan = plan || emptyPlan();
        if (!plan.grid) plan.grid = { width: 25, depth: 20 };
        plan.defaults = { wallHeight: (plan.defaults && plan.defaults.wallHeight) || 3 };
        if (!plan.analysis) plan.analysis = { residents: 1, propertyType: 'Residence' };
        if (!plan.prices) plan.prices = {};
        if (!plan.levels || !plan.levels.length) {
            plan.levels = [{ name: '', height: null, rooms: plan.rooms || [], objects: plan.objects || [] }];
        }
        delete plan.walls; delete plan.floors; delete plan.rooms; delete plan.objects;
        delete plan.ai;   // programme IA des plans d'avant la modification de n'importe quel plan : plus lu
        plan.levels.forEach(function (l) {
            l.name = l.name || '';
            if (l.height === undefined) l.height = null;
            delete l.walls; delete l.floors; delete l.holes;
            l.rooms = l.rooms || []; l.objects = l.objects || [];
            // Graine : z relatif à la dalle du niveau (un v3 n'en a pas : première couche d'air, comme avant).
            l.rooms.forEach(function (r) { delete r.height; delete r.ceilingMaterial; if (r.seed.z == null) r.seed.z = 1; });
        });
        plan.groundIndex = Math.max(0, Math.min(plan.levels.length - 1, plan.groundIndex || 0));
        plan.mode = plan.mode === 'architecture' ? 'architecture' : 'house';
        const a = plan.architecture || (plan.architecture = {});
        a.height = Math.max(1, Math.min(MAX_ARCH_HEIGHT, parseInt(a.height, 10) || 20));
        a.ops = Array.isArray(a.ops) ? a.ops.filter(function (o) { return o && typeof o === 'object'; }) : [];
        if (a.image === undefined) a.image = null;
        plan.schemaVersion = 4;
        return plan;
    }

    function uid(prefix) { return prefix + Math.random().toString(36).slice(2, 8); }
    function clone(o) { return JSON.parse(JSON.stringify(o)); }
    function rotate(o, r) {
        switch (r & 3) {
            case 0: return { x: o.x, y: o.y, z: o.z };
            case 1: return { x: o.z, y: o.y, z: -o.x };
            case 2: return { x: -o.x, y: o.y, z: -o.z };
            default: return { x: -o.z, y: o.y, z: o.x };
        }
    }
    function cssVar(name, fallback) {
        const v = getComputedStyle(document.documentElement).getPropertyValue(name).trim();
        return v || fallback;
    }

    // ---- État / historique -------------------------------------------------------------------------

    function create(container, dotnetRef, options) {
        const staticCanvas = document.createElement('canvas');
        const dynamicCanvas = document.createElement('canvas');
        staticCanvas.className = 'bp-layer bp-layer-static';
        dynamicCanvas.className = 'bp-layer bp-layer-dynamic';
        dynamicCanvas.tabIndex = 0;
        container.classList.add('bp-container');
        container.appendChild(staticCanvas);
        // Rendu 3D (WebGL) : entre le statique (bande d'élévation) et le dynamique (événements), masqué hors mode 3D.
        const view3dCanvas = document.createElement('canvas');
        view3dCanvas.className = 'bp-layer bp-layer-3d'; view3dCanvas.style.display = 'none';
        container.appendChild(view3dCanvas);
        container.appendChild(dynamicCanvas);
        // Sélecteur de fichier du fond de plan (mode architecture), déclenché par le bouton Importer des réglages.
        const fileInput = document.createElement('input');
        fileInput.type = 'file'; fileInput.accept = 'image/*'; fileInput.style.display = 'none';
        container.appendChild(fileInput);
        // Sélecteur de fichier pour l'import d'un plan JSON (menu Plan).
        const planInput = document.createElement('input');
        planInput.type = 'file'; planInput.accept = 'application/json,.json'; planInput.style.display = 'none';
        container.appendChild(planInput);

        const st = {
            container, staticCanvas, dynamicCanvas, fileInput, planInput, dotnetRef,
            options: options || {},
            plan: emptyPlan(),
            level: 0,                 // niveau affiché / édité (état de vue, hors historique)
            catalog: { materials: [], objects: [], categories: [], serverId: '' },
            objectsByName: {},
            analysis: null,
            tool: 'select',
            material: null,
            form: 'Cube', formRot: 0,  // forme de pose (palette de Structure) et sa rotation en jeu 0..3
            formByMaterial: {},       // dernière forme posée par matériau, comme au marteau
            objectType: null,
            rotation: 0,
            view: { scale: 1, ox: 20, oy: 20 },
            history: [], future: [],
            dirty: false,
            selection: null,          // { kind: 'object'|'room', id }
            hover: null,              // { x, y }
            pointerOver: false,       // souris réellement au-dessus du canvas (hover peut être posé par focusCell)
            drag: null,
            pending: null,            // courbe tracée, en attente de son point de contrôle : { op, side } (side : née dans la coupe)
            pocketCache: null,        // { vox, sig, pockets } : dernière detectPockets (ensurePockets)
            pocketRooms: {},          // n° de poche → roomId
            roomGrid: null,           // Int32Array(W·D) : poche de la première couche affichée qui en a une (0 aucune)
            roomLabels: {},           // n° de poche → { x, y } : case de son empreinte la plus proche du centre
            icons: {},
            materialsByName: {},
            rotatable: new Map(),     // 'matériau|forme' → forme tournable pour ce matériau (formRotatable, lu par case dessinée)
            matRgb: {},               // nom → [r,g,b] résolu une fois (materialColor est appelé par cellule dessinée)
            legend: true,             // légende des matériaux (préférence d'affichage, pas une donnée du plan)
            legendHit: null,          // zone cliquable du chevron de la légende, posée au rendu
            exporting: false,         // le temps d'un export PNG : la légende s'y dessine sans ses commandes
            // Mode architecture (état de vue, hors historique).
            layer: 0,                 // couche z affichée / éditée
            shape: { subtract: false, hollow: false, thickness: 1, height: 3 },   // options des outils de forme
            vox: null,                // évaluation des formes (evalOps), reconstruite à chaque commit
            cut: null,                // plan de coupe { axis:'x'|'y', index, dir:1|-1 } ou null
            elevSide: 's',            // face regardée par l'élévation : s (depuis le bas du plan), e, n, w — les flèches de son en-tête la tournent
            cutEyes: [],              // zones cliquables des yeux du plan de coupe (écran)
            bgImage: null,            // Image du fond de plan (pixels), placement dans plan.architecture.image
            bgVisible: true,
            houseElev: false,         // mode maison : bande d'élévation affichée (bascule de la barre d'outils, non persistée)
            // Rendu 3D (état de vue) : caméra orbitale, coupe au-dessus de la couche, coupe face caméra, maillage et atlas d'icônes.
            view3d: { on: false, cap: false, cutaway: false, cutAt: [0, 0, 0], yaw: -0.6, pitch: 0.55, dist: 40, target: [0, 0, 0], canvas: view3dCanvas, gl: null, data: null, bbox: null, faces: 0, dirty: true, upload: false, tooMany: false,
                atlas: { canvas: null, ctx: null, slots: {}, pending: {}, next: 0, dirty: false },
                // Bundles de formes : index (null = pas demandé, 'pending', objet, false = absent), sets préparés par nom (null = en cours, false = échec).
                forms: { index: null, sets: {}, scratch: null }, clean: false },
            staticDirty: true,
            raf: 0,
            palette: {
                bg: cssVar('--mud-palette-background', '#1e2429'),
                grid: 'rgba(255,255,255,0.06)',
                gridStrong: 'rgba(255,255,255,0.14)',
                text: cssVar('--mud-palette-text-primary', 'rgba(255,255,255,0.8)'),
                primary: cssVar('--mud-palette-primary', '#64b5f6'),
                secondary: cssVar('--mud-palette-secondary', '#ffb74d'),
                success: cssVar('--mud-palette-success', '#4caf50'),
                warning: cssVar('--mud-palette-warning', '#ff9800'),
                error: cssVar('--mud-palette-error', '#f44336'),
                info: cssVar('--mud-palette-info', '#2196f3'),
            },
        };

        st.view3d.atlas.canvas = document.createElement('canvas');
        st.view3d.atlas.canvas.width = st.view3d.atlas.canvas.height = V3D_ATLAS;
        st.view3d.atlas.ctx = st.view3d.atlas.canvas.getContext('2d');
        view3dCanvas.addEventListener('webglcontextlost', function (e) { e.preventDefault(); st.view3d.gl = null; });
        view3dCanvas.addEventListener('webglcontextrestored', function () { st.view3d.gl = null; st.view3d.dirty = true; requestRender(st); });
        bindEvents(st);
        fileInput.addEventListener('change', function (e) { importBackgroundFile(st, e.target.files && e.target.files[0]); e.target.value = ''; });
        planInput.addEventListener('change', function (e) { importPlanFile(st, e.target.files && e.target.files[0]); e.target.value = ''; });
        resize(st);
        st.resizeObserver = new ResizeObserver(function () { resize(st); });
        st.resizeObserver.observe(container);
        return st;
    }

    function resize(st) {
        const w = st.container.clientWidth, h = st.container.clientHeight;
        if (w === 0 || h === 0) return;
        const dpr = window.devicePixelRatio || 1;
        [st.staticCanvas, st.dynamicCanvas].forEach(function (c) {
            c.width = Math.round(w * dpr); c.height = Math.round(h * dpr);
            c.style.width = w + 'px'; c.style.height = h + 'px';
        });
        st.dpr = dpr;
        st.staticDirty = true;
        if (st.view3d.on) view3dLayout(st);
        requestRender(st);
    }

    function cellSize(st) { return CELL * st.view.scale; }
    function toScreen(st, x, y) { const cs = cellSize(st); return { x: st.view.ox + x * cs, y: st.view.oy + y * cs }; }
    function toCell(st, px, py) {
        const cs = cellSize(st);
        return { x: Math.floor((px - st.view.ox) / cs), y: Math.floor((py - st.view.oy) / cs) };
    }
    function inGrid(st, x, y) { return x >= 0 && y >= 0 && x < st.plan.grid.width && y < st.plan.grid.depth; }

    // ---- Niveaux ----------------------------------------------------------------------------------------

    function cur(st) { return st.plan.levels[st.level]; }
    function levelHeight(st, k) { const l = st.plan.levels[k]; return l.height || st.plan.defaults.wallHeight; }

    function findRoom(st, id) {
        for (let k = 0; k < st.plan.levels.length; k++) {
            const r = st.plan.levels[k].rooms.find(function (x) { return x.id === id; });
            if (r) return { item: r, level: k };
        }
        return null;
    }

    function findObject(st, id) {
        for (let k = 0; k < st.plan.levels.length; k++) {
            const o = st.plan.levels[k].objects.find(function (x) { return x.id === id; });
            if (o) return { item: o, level: k };
        }
        return null;
    }

    function clampLevel(st) {
        const max = st.plan.levels.length - 1;
        const next = Math.max(0, Math.min(max, st.level));
        if (next !== st.level) { st.level = next; notifyLevel(st); }
    }

    function setLevelInternal(st, k) {
        if (k < 0 || k >= st.plan.levels.length || k === st.level) return;
        st.level = k;
        st.drag = null;
        st.staticDirty = true;
        recomputeFootprints(st);
        notifyLevel(st);
        requestRender(st);
    }

    function notifyLevel(st) {
        if (!st.dotnetRef) return;
        st.dotnetRef.invokeMethodAsync('OnLevelChanged', st.level).catch(function () { });
    }

    // ---- Niveaux déduits du bâtiment (formes) -------------------------------------------------------------
    // Une dalle est une couche où au moins SLAB_COVERAGE de l'emprise (colonnes ayant un bloc) est un bloc surmonté
    // d'air, à au moins une couche d'air de la précédente ; le dernier plateau sans rien au-dessus est un toit, pas un
    // niveau. Hauteurs : h_k = z_{k+1} − z_k − 1 (levelBaseY) ; le dernier monte jusqu'au toit ou au plus haut bloc.
    // Une première dalle au-dessus de 1 reçoit un « socle » en dessous (le niveau 0 est toujours à y = 0) ; à 1,
    // elle est ignorée (pas de niveau de hauteur 0) et signalée.
    const SLAB_COVERAGE = 0.3;

    function detectLevels(st) {
        const v = st.vox; if (!v) return null;
        const W = v.W, D = v.D, H = v.H, WD = W * D;
        const column = new Uint8Array(WD), surf = new Int32Array(H), solid = new Int32Array(H);
        for (let z = 0; z < H; z++) for (let i = 0; i < WD; i++) {
            if (!v.cells[i + WD * z]) continue;
            column[i] = 1; solid[z]++;
            if (z + 1 >= H || !v.cells[i + WD * (z + 1)]) surf[z]++;
        }
        let footprint = 0; for (let i = 0; i < WD; i++) footprint += column[i];
        if (!footprint) return null;
        let top = 0; for (let z = 0; z < H; z++) if (solid[z]) top = z;
        const slabs = [];
        for (let z = 0; z <= top; z++) if (surf[z] >= SLAB_COVERAGE * footprint && (!slabs.length || z >= slabs[slabs.length - 1] + 2)) slabs.push(z);
        const roof = slabs.length > 1 && slabs[slabs.length - 1] === top ? slabs.pop() : null;
        const lowSlab = slabs.length > 0 && slabs[0] === 1;
        if (lowSlab) slabs.shift();
        if (!slabs.length) slabs.push(0);
        const levels = [];
        if (slabs[0] > 1) levels.push({ z: 0, coverage: null, height: slabs[0] - 1, ground: false });
        slabs.forEach(function (z, i) {
            let h;
            if (i + 1 < slabs.length) h = slabs[i + 1] - z - 1;
            else if (roof !== null) h = roof - z - 1;
            else if (top > z) h = top - z;
            else h = st.plan.defaults.wallHeight;
            levels.push({ z: z, coverage: surf[z] / footprint, height: Math.max(1, Math.min(MAX_LEVEL_HEIGHT, h)), ground: i === 0 });
        });
        const truncated = levels.length > MAX_LEVELS;
        if (truncated) levels.length = MAX_LEVELS;
        const groundIndex = Math.max(0, levels.findIndex(function (l) { return l.ground; }));
        const plan = st.plan;
        const matches = plan.levels.length === levels.length && plan.groundIndex === groundIndex && levels.every(function (l, k) { return levelHeight(st, k) === l.height; });
        return { levels: levels, groundIndex: groundIndex, lowSlab: lowSlab, truncated: truncated, matches: matches, empty: plan.levels.every(function (l) { return !l.objects.length; }) };
    }

    // Remplace la pile par ces hauteurs : le niveau k existant garde son contenu (sa hauteur est ajustée), les niveaux
    // manquants sont créés, les niveaux en trop sont conservés s'ils ont des objets (les pièces sont dérivées). Annulable.
    function applyLevels(st, heights, groundIndex) {
        if (!heights || !heights.length) return;
        pushHistory(st);
        const plan = st.plan, out = [];
        heights.slice(0, MAX_LEVELS).forEach(function (h, k) {
            const l = plan.levels[k] || emptyLevel();
            l.height = Math.max(1, Math.min(MAX_LEVEL_HEIGHT, h | 0));
            out.push(l);
        });
        for (let k = out.length; k < plan.levels.length; k++) if (plan.levels[k].objects.length) out.push(plan.levels[k]);
        plan.levels = out;
        plan.groundIndex = Math.max(0, Math.min(out.length - 1, groundIndex | 0));
        st.level = plan.groundIndex;
        select(st, null, null);
        commit(st, 'levels');
        notifyLevel(st);
    }

    // ---- Architecture : évaluation des formes --------------------------------------------------------
    // Spécification partagée avec ArchShapes.cs (tout en entiers, exacts en double jusqu'à 2^53) : boîte englobante
    // inclusive a..b ; R = étendue par axe ; d = 2·coord − x0 − x1. Sphère : Σ d²·(autres R²) ≤ ΠR² ; cylindre :
    // idem sur les deux axes radiaux ; creux : dans l'extérieur et hors de la boîte rétrécie de thickness (closed : pavé ou
    // cylindre creux fermé aux deux bouts, la boîte est aussi rétrécie le long de z / de l'axe).

    function isArch(st) { return st.plan.mode === 'architecture'; }
    // Y de la dalle du niveau k (miroir de PlanDocument.LevelBaseY) ; levelBaseY(levels.length) = plafond du dernier niveau.
    function levelBaseY(st, k) { let y = 0; for (let i = 0; i < k; i++) y += levelHeight(st, i) + 1; return y; }
    // Hauteur des couches affichables : couches éditables des formes, pile des niveaux comprise.
    function archH(st) {
        return Math.max(st.plan.architecture.height, levelBaseY(st, st.plan.levels.length) + 1);
    }
    // Plafond des formes (identique à ArchShapes.PaintAll côté C#) : au-dessus, rien n'est posé.
    function opsH(st) { return st.plan.architecture.height; }

    function opBounds(op) {
        const a = op.a, b = op.b, c = op.kind === 'curve' ? op.c : a;
        return { x0: Math.min(a[0], b[0], c[0]), x1: Math.max(a[0], b[0], c[0]), y0: Math.min(a[1], b[1], c[1]), y1: Math.max(a[1], b[1], c[1]), z0: Math.min(a[2], b[2], c[2]), z1: Math.max(a[2], b[2], c[2]) };
    }
    function shrinkBounds(b, t, sx, sy, sz) {
        const r = { x0: b.x0 + (sx ? t : 0), x1: b.x1 - (sx ? t : 0), y0: b.y0 + (sy ? t : 0), y1: b.y1 - (sy ? t : 0), z0: b.z0 + (sz ? t : 0), z1: b.z1 - (sz ? t : 0) };
        return r.x0 > r.x1 || r.y0 > r.y1 || r.z0 > r.z1 ? null : r;
    }
    function opAxis(op) { return op.kind === 'cylinder' && (op.axis === 'x' || op.axis === 'y') ? op.axis : 'z'; }
    function innerBounds(op, outer) {
        if (!op.hollow) return null;
        const t = Math.max(1, op.thickness | 0), axis = opAxis(op), closed = !!op.closed;
        if (op.kind === 'box') return shrinkBounds(outer, t, true, true, closed);
        if (op.kind === 'sphere') return shrinkBounds(outer, t, true, true, true);
        return shrinkBounds(outer, t, axis !== 'x' || closed, axis !== 'y' || closed, axis !== 'z' || closed);
    }
    function insideBounds(kind, axis, b, x, y, z) {
        if (x < b.x0 || x > b.x1 || y < b.y0 || y > b.y1 || z < b.z0 || z > b.z1) return false;
        if (kind === 'box') return true;
        const rx = b.x1 - b.x0 + 1, ry = b.y1 - b.y0 + 1, rz = b.z1 - b.z0 + 1;
        const dx = 2 * x - b.x0 - b.x1, dy = 2 * y - b.y0 - b.y1, dz = 2 * z - b.z0 - b.z1;
        if (kind === 'sphere') return dx * dx * ry * ry * rz * rz + dy * dy * rx * rx * rz * rz + dz * dz * rx * rx * ry * ry <= rx * rx * ry * ry * rz * rz;
        if (axis === 'x') return dy * dy * rz * rz + dz * dz * ry * ry <= ry * ry * rz * rz;
        if (axis === 'y') return dx * dx * rz * rz + dz * dz * rx * rx <= rx * rx * rz * rz;
        return dx * dx * ry * ry + dy * dy * rx * rx <= rx * rx * ry * ry;
    }
    // Ligne 3D : n + 1 cellules, coordonnées arrondies au plus proche.
    function lineCells(a, b) {
        const n = Math.max(Math.abs(b[0] - a[0]), Math.abs(b[1] - a[1]), Math.abs(b[2] - a[2]));
        if (n === 0) return [[a[0], a[1], a[2]]];
        const out = [];
        for (let k = 0; k <= n; k++) {
            out.push([0, 1, 2].map(function (i) { return Math.floor((2 * a[i] * n + 2 * (b[i] - a[i]) * k + n) / (2 * n)); }));
        }
        return out;
    }
    // Courbe de Bézier quadratique a → b tirée par c : 2·max(|c−a|, |b−c|) + 1 échantillons (un pas ≤ 1 case par axe,
    // donc une chaîne 26-connexe), coordonnées arrondies au plus proche, doublons consécutifs fusionnés. Même
    // arithmétique entière que ArchShapes.CurveCells.
    function curveCells(a, c, b) {
        const cheb = function (p, q) { return Math.max(Math.abs(q[0] - p[0]), Math.abs(q[1] - p[1]), Math.abs(q[2] - p[2])); };
        const n = 2 * Math.max(cheb(a, c), cheb(c, b));
        if (n === 0) return [[a[0], a[1], a[2]]];
        const den = n * n, out = [];
        for (let k = 0; k <= n; k++) {
            const p = [0, 1, 2].map(function (i) { return Math.floor((2 * ((n - k) * (n - k) * a[i] + 2 * k * (n - k) * c[i] + k * k * b[i]) + den) / (2 * den)); });
            const last = out[out.length - 1];
            if (!last || last[0] !== p[0] || last[1] !== p[1] || last[2] !== p[2]) out.push(p);
        }
        return out;
    }
    function shapeCells(op) { return op.kind === 'curve' ? curveCells(op.a, op.c, op.b) : lineCells(op.a, op.b); }
    function validOp(op) {
        if (op.kind === 'cells') return Array.isArray(op.cells);
        if (op.kind === 'curve' && !(Array.isArray(op.c) && op.c.length === 3)) return false;
        return Array.isArray(op.a) && op.a.length === 3 && Array.isArray(op.b) && op.b.length === 3;
    }

    // Forme de bloc portée par une op (op.form, op.rot 0..3) : code = (index dans FORMS + 1)·4 + rot, 0 = cube plein.
    // Le C# ignore ces champs (une forme est un cube plein pour le coût et les pièces) ; seule la vue 3D les lit.
    const FORMS = ['Wall', 'Floor', 'RoofSide', 'RoofCorner', 'RoofTurn', 'RoofPeak', 'Stairs', 'Column', 'Cube', 'WindowGrilles',
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
        'WhiteRampEdgeB', 'WhiteRampEdgeC', 'WhiteRampEdgeD', 'SimpleFloor', 'CanopyWindow', 'SteelPipe'];   // même ordre que BuildingGenerator.Forms
    function shapeCode(op) {
        if (op.subtract) return 0;
        const f = FORMS.indexOf(op.form);
        return f < 0 ? 0 : (f + 1) * 4 + ((op.rot | 0) & 3);
    }
    // Inverse de shapeCode (pipette) : 0 ou forme inconnue = Cube.
    function decodeShape(code) {
        const form = code ? FORMS[(code >> 2) - 1] : null;
        return form && form !== 'Cube' ? { form: form, rot: code & 3 } : { form: 'Cube', rot: 0 };
    }
    // Vue de dessus d'une case à forme : icône de la forme et côté bas du rot (null : cube). Côté bas pour rot 0..3 : est, nord,
    // ouest, sud (BuildingGenerator.LowToward) ; x croît vers l'est, y vers le sud = vers le bas de l'écran.
    const LOW_SIDE = [[1, 0], [0, -1], [-1, 0], [0, 1]];
    const shapeInfoCache = new Map();
    function shapeInfo(code) {
        let s = shapeInfoCache.get(code);
        if (s === undefined) {
            const d = decodeShape(code);
            s = d.form === 'Cube' ? null : { form: d.form, rot: d.rot, icon: d.form + 'Form', low: LOW_SIDE[d.rot] };
            shapeInfoCache.set(code, s);
        }
        return s;
    }
    // Triangle du côté bas dans la case (p = coin haut gauche à l'écran, cs = taille) : pointe sur le bord bas, base vers le centre.
    function lowSideTriangle(p, cs, low) {
        const cx = p.x + cs / 2, cy = p.y + cs / 2, r = cs / 2 - 2, h = Math.max(3, cs * 0.2), w = h * 0.9;
        const ax = cx + low[0] * r, ay = cy + low[1] * r, bx = ax - low[0] * h, by = ay - low[1] * h;
        return [[ax, ay], [bx - low[1] * w, by + low[0] * w], [bx + low[1] * w, by - low[0] * w]];
    }
    // Forme de pose portée par un nouveau volume ; omise pour un cube et pour une soustraction (compatibilité des plans).
    function applyForm(op, form, rot) {
        if (op.subtract || !form || form === 'Cube') { delete op.form; delete op.rot; }
        else { op.form = form; op.rot = (rot | 0) & 3; }
        return op;
    }
    // Rotation d'un quart de tour : dir = +1 (R, rot + 1 comme en jeu) ou −1 (Q).
    function rotStep(rot, dir) { return ((rot | 0) + (dir < 0 ? -1 : 1)) & 3; }
    // Forme de pose au changement de matériau (forms = [{name, rotatable}] du nouveau) : la dernière posée avec lui, sinon la même
    // forme s'il l'a, sinon Cube ; la rotation est gardée si la forme retenue se tourne.
    function formForMaterial(forms, remembered, form, rot) {
        const find = function (n) { return (forms || []).find(function (f) { return f.name === n; }); };
        const f = (remembered && find(remembered)) || find(form);
        return f && f.name !== 'Cube' ? { form: f.name, rot: f.rotatable ? rot & 3 : 0 } : { form: 'Cube', rot: 0 };
    }

    // Matériaux tuyaux (= BuildingPlannerCatalogService.PipeMaterials) : en jeu un tuyau traverse un mur ou une dalle sans les creuser.
    const PIPE_MATERIALS = ['CopperPipeItem', 'IronPipeItem', 'SteelPipeItem'];

    // Écrit value (0 = vide) dans cells[x + W·(y + D·z)], opIndex + 1 dans owner et shape dans shapes pour chaque cellule de l'op.
    // under (Map, facultatif) : un tuyau (value ∈ pipes) posé sur un bloc garde ce bloc dessous, [cellule, forme, owner] ; tout
    // autre ajout ou soustraction l'efface.
    function paintOp(op, W, D, H, cells, owner, value, opIndex, shapes, shape, under, pipes) {
        if (!validOp(op)) return;
        function set(x, y, z) {
            if (x < 0 || y < 0 || z < 0 || x >= W || y >= D || z >= H) return;
            const i = x + W * (y + D * z);
            if (under) {
                if (!value || !pipes.has(value)) under.delete(i);
                else if (cells[i] && !pipes.has(cells[i])) under.set(i, [cells[i], shapes ? shapes[i] : 0, owner ? owner[i] : 0]);
            }
            cells[i] = value; if (owner) owner[i] = opIndex + 1; if (shapes) shapes[i] = shape;
        }
        if (op.kind === 'cells') { for (let i = 0; i + 2 < op.cells.length; i += 3) set(op.cells[i], op.cells[i + 1], op.cells[i + 2]); return; }
        if (op.kind === 'line' || op.kind === 'curve') { shapeCells(op).forEach(function (c) { set(c[0], c[1], c[2]); }); return; }
        const outer = opBounds(op), inner = innerBounds(op, outer), axis = opAxis(op);
        for (let z = Math.max(0, outer.z0); z <= Math.min(H - 1, outer.z1); z++)
            for (let y = Math.max(0, outer.y0); y <= Math.min(D - 1, outer.y1); y++)
                for (let x = Math.max(0, outer.x0); x <= Math.min(W - 1, outer.x1); x++)
                    if (insideBounds(op.kind, axis, outer, x, y, z) && !(inner && insideBounds(op.kind, axis, inner, x, y, z))) set(x, y, z);
    }

    // Évaluation complète : les formes dans l'ordre, rognées à architecture.height comme côté C#. cells = index palette + 1
    // (0 = vide), owner = dernière op (ajout ou soustraction) ayant couvert la cellule (0 : jamais couverte ; à la couche 0,
    // terrain intact). H = nombre de couches du tableau.
    // shapes = forme de bloc (shapeCode) de la dernière op ayant couvert la cellule.
    // under = Map index → [cellule, forme, owner] du bloc qu'un tuyau traverse (dessiné avec lui en 3D ; le C# ne voit que le tuyau).
    function evalOps(plan, H) {
        const W = plan.grid.width, D = plan.grid.depth;
        H = H || plan.architecture.height;
        const cells = new Uint16Array(W * D * H), owner = new Uint16Array(W * D * H), shapes = new Uint16Array(W * D * H);
        const palette = [], index = {}, under = new Map(), pipes = new Set();
        function paletteIndex(name) {
            if (index[name] === undefined) { index[name] = palette.length; palette.push(name); if (PIPE_MATERIALS.indexOf(name) >= 0) pipes.add(palette.length); }
            return index[name] + 1;
        }
        const hOps = Math.min(H, plan.architecture.height);
        plan.architecture.ops.forEach(function (op, i) {
            paintOp(op, W, D, hOps, cells, owner, op.subtract ? 0 : paletteIndex(op.material || ''), i, shapes, shapeCode(op), under, pipes);
        });
        return { W: W, D: D, H: H, cells: cells, owner: owner, shapes: shapes, palette: palette, under: under };
    }

    // Masque d'une seule op sur la couche z (aperçu, contour de sélection) : Uint8Array(W·D), index x + W·y.
    function opLayerMask(op, z, W, D) {
        const mask = new Uint8Array(W * D);
        if (!validOp(op)) return mask;
        const cells = new Uint16Array(W * D * (z + 1));
        paintOp(op, W, D, z + 1, cells, null, 1, 0);
        mask.set(cells.subarray(W * D * z, W * D * (z + 1)));
        return mask;
    }

    function layerCells(vox, z) { return vox.cells.subarray(vox.W * vox.D * z, vox.W * vox.D * (z + 1)); }
    function voxAt(vox, x, y, z) { return x < 0 || y < 0 || z < 0 || x >= vox.W || y >= vox.D || z >= vox.H ? 0 : vox.cells[x + vox.W * (y + vox.D * z)]; }
    // Op visible qui a posé le bloc en (x, y, z), sinon null : une case vide (trou creusé, air) ne sélectionne rien,
    // jamais une soustraction.
    function ownerAt(st, x, y, z) {
        const v = st.vox;
        if (!v || x < 0 || y < 0 || z < 0 || x >= v.W || y >= v.D || z >= v.H) return null;
        const i = x + v.W * (y + v.D * z), o = v.owner[i];
        return o && v.cells[i] ? (st.plan.architecture.ops[o - 1] || null) : null;
    }

    // Vue de côté : projection le long de axis ('x' ou 'y') vers dir (+1 : indices croissants). cut = cellules du plan
    // index (null pour une élévation depuis le bord), beyond = premier bloc rencontré au-delà, depth = sa distance.
    // cols = l'autre axe horizontal (miroir si le spectateur le voit de droite à gauche), rows = z.
    // objs (mode maison) = cellules de meubles : obj[o] vaut 1 si le meuble est devant tout bloc, 2 s'il est derrière
    // (un meuble dans une pièce fermée est toujours masqué par un mur : il se dessine alors en transparence).
    function sideView(vox, axis, index, dir, objs) {
        const along = axis === 'x' ? vox.W : vox.D, cols = axis === 'x' ? vox.D : vox.W, rows = vox.H;
        const mirror = axis === 'x' ? dir < 0 : dir > 0;
        const cut = index == null ? null : new Uint16Array(cols * rows), beyond = new Uint16Array(cols * rows), depth = new Int16Array(cols * rows).fill(-1);
        const obj = objs ? new Uint8Array(cols * rows) : null;   // meuble rencontré avant tout bloc (mode maison)
        const start = index == null ? (dir > 0 ? 0 : along - 1) : index + dir;
        for (let z = 0; z < rows; z++) for (let c = 0; c < cols; c++) {
            const cc = mirror ? cols - 1 - c : c;
            const o = c + cols * z;
            if (cut) cut[o] = axis === 'x' ? voxAt(vox, index, cc, z) : voxAt(vox, cc, index, z);
            let hitBlock = false, hitObj = 0;
            for (let k = start, d = 0; k >= 0 && k < along; k += dir, d++) {
                const x = axis === 'x' ? k : cc, y = axis === 'x' ? cc : k;
                const v = voxAt(vox, x, y, z);
                if (v && !hitBlock) { beyond[o] = v; depth[o] = d; hitBlock = true; if (!objs) break; }
                if (objs && !hitObj && objs.has(x + vox.W * (y + vox.D * z))) hitObj = hitBlock ? 2 : 1;
                if (hitBlock && hitObj) break;
            }
            if (obj) obj[o] = hitObj;
        }
        return { cols: cols, rows: rows, cut: cut, beyond: beyond, depth: depth, obj: obj };
    }
    function elevation(vox, axis, dir, objs) { return sideView(vox, axis, null, dir, objs); }
    function section(vox, axis, index, dir) { return sideView(vox, axis, index, dir); }

    // Même tri qu'ArchitectureEvaluator : total décroissant puis nom.
    function countByMaterial(vox) {
        const counts = new Int32Array(vox.palette.length + 1);
        for (let i = 0; i < vox.cells.length; i++) counts[vox.cells[i]]++;
        return vox.palette.map(function (m, i) { return { material: m, count: counts[i + 1] }; })
            .filter(function (l) { return l.count > 0; })
            .sort(function (a, b) { return b.count - a.count || (a.material < b.material ? -1 : a.material > b.material ? 1 : 0); });
    }

    // Dans les deux modes : les formes (owner = op) ; les pièces (poches) ne sont recalculées qu'au commit (reconcileRooms).
    function refreshVox(st) {
        const h = archH(st);
        st.vox = evalOps(st.plan, h);
        st.view3d.dirty = true;
        formsEnsure(st);   // bundles des matériaux du plan (une fois chacun)
        if (st.layer > h - 1) st.layer = h - 1;
        if (h !== st.notifiedH || st.layer !== st.notifiedLayer) notifyLayer(st);   // la hauteur effective change avec les niveaux
    }

    function setLayerInternal(st, z) {
        z = Math.max(0, Math.min(archH(st) - 1, z));
        if (z === st.layer) return;
        st.layer = z;
        st.staticDirty = true;
        st.view3d.dirty = true;   // maillage si coupe au-dessus
        recomputeFootprints(st);
        notifyLayer(st);
        requestRender(st);
    }

    function notifyLayer(st) {
        st.notifiedLayer = st.layer; st.notifiedH = archH(st);
        if (!st.dotnetRef) return;
        st.dotnetRef.invokeMethodAsync('OnLayerChanged', st.layer, st.notifiedH).catch(function () { });
    }

    function findOp(st, id) {
        return st.plan.architecture.ops.find(function (o) { return o.id === id; }) || null;
    }

    // Niveau dont la tranche [base_k, base_{k+1}) contient z (le dernier absorbe tout ce qui est au-dessus) — miroir de PlanDocument.LevelIndexAtY.
    function levelIndexAtY(st, z) {
        for (let k = st.plan.levels.length - 1; k >= 0; k--) if (z >= levelBaseY(st, k)) return k;
        return 0;
    }

    // Bloc posé par une forme en (x, y, z) : la dernière op couvrant la cellule est un ajout.
    function archSolidAt(st, x, y, z) {
        const v = st.vox;
        if (!v || x < 0 || y < 0 || z < 0 || x >= v.W || y >= v.D || z >= v.H) return false;
        const i = x + v.W * (y + v.D * z);
        return v.owner[i] !== 0 && v.cells[i] !== 0;
    }

    // ---- Gomme unifiée (clic droit maintenu, outil gomme, crayon en soustraction) --------------------------
    // Supprime le bloc en (x, y, z), jamais plus : une case posée au crayon est retirée de son trait, un bloc d'une
    // forme géométrique (pavé, sphère, cylindre, ligne) est creusé par une op « cells » soustractive (une seule pour
    // des gommages consécutifs, jamais sélectionnée ; la forme reste paramétrique et se supprime entière depuis le
    // panneau). Une cellule déjà vide est laissée telle quelle. Une entrée d'historique par geste ; drag.changed dit
    // s'il y a quelque chose à valider.
    function eraseAt(st, drag, x, y, z) {
        const vox = st.vox;
        if (!vox || !inGrid(st, x, y) || z < 0 || z >= vox.H) return;
        const i = x + vox.W * (y + vox.D * z);
        const owner = vox.owner[i];
        if (owner) {
            const ops = st.plan.architecture.ops, op = ops[owner - 1];
            if (!op || op.subtract) return;   // déjà vide
            if (!drag.pushed) { pushHistory(st); drag.pushed = true; }
            if (op.kind === 'cells') {
                for (let j = 0; j + 2 < op.cells.length; j += 3) if (op.cells[j] === x && op.cells[j + 1] === y && op.cells[j + 2] === z) { op.cells.splice(j, 3); break; }
                if (op.cells.length) { drag.changed = true; drag.dirtyVox = true; return; }
                ops.splice(owner - 1, 1);
                if (st.selection && st.selection.kind === 'op' && st.selection.id === op.id) select(st, null, null);
                drag.changed = true;
                refreshVox(st); drag.dirtyVox = false;   // les index des ops ont glissé : owner doit être recalculé avant la cellule suivante
                return;
            }
            // Forme géométrique : on creuse ce seul bloc. Si la dernière op est déjà un gommage, on y ajoute la cellule.
            let carve = ops[ops.length - 1];
            if (!carve || carve.kind !== 'cells' || !carve.subtract) { carve = { id: uid('a'), kind: 'cells', subtract: true, material: null, cells: [] }; ops.push(carve); }
            carve.cells.push(x, y, z);
            drag.changed = true; drag.dirtyVox = true;
        }
    }

    function newEraseDrag(cell) { return { kind: 'erase', pushed: false, changed: false, dirtyVox: false, last: cell }; }

    // Fin du geste : un seul commit.
    function finishErase(st, drag) {
        if (drag.changed) commit(st, 'erase');
        return drag.changed;
    }

    // Ajoute une op ; une op qui ne pose ni n'enlève aucune cellule dans la grille est abandonnée.
    function addOp(st, op, select_) {
        op.id = uid('a');
        pushHistory(st);
        st.plan.architecture.ops.push(op);
        const before = st.vox ? st.vox.cells : null;
        refreshVox(st);
        let changed = !before;
        if (before) for (let i = 0; i < before.length && !changed; i++) if (before[i] !== st.vox.cells[i]) changed = true;
        if (!changed && !(op.subtract && opInGrid(st, op))) {
            // Rien dans la grille : on ne garde ni l'op ni l'entrée d'historique. Une soustraction qui ne traverse que
            // de l'air est gardée (elle s'ajuste ensuite dans la fiche, par ex. pour atteindre une paroi).
            st.plan.architecture.ops.pop(); st.history.pop(); refreshVox(st); requestRender(st);
            return null;
        }
        commit(st, 'op');
        if (select_ !== false) select(st, 'op', op.id);
        return op;
    }

    // L'op a-t-elle au moins une cellule potentielle dans la grille ?
    function opInGrid(st, op) {
        if (op.kind === 'cells') return op.cells.length >= 3;
        if (!validOp(op)) return false;
        const b = opBounds(op), W = st.plan.grid.width, D = st.plan.grid.depth, H = opsH(st);
        return b.x1 >= 0 && b.y1 >= 0 && b.z1 >= 0 && b.x0 < W && b.y0 < D && b.z0 < H;
    }

    // Op créée par un drag de forme : centrée sur la couche courante (hauteur paire : la couche en plus va au-dessus) ;
    // ce qui dépasse sous 0 ou au-dessus de la hauteur max est coupé. La sphère garde sa boîte entière (dôme si centrée
    // trop bas : les cellules hors grille sont ignorées à l'évaluation).
    function shapeOpFromDrag(st, drag, cell) {
        const s = st.shape, z = st.layer, H = opsH(st);
        const op = { kind: drag.tool, subtract: !!drag.subtract, material: drag.subtract ? null : st.material, hollow: false, thickness: 1 };
        const h = drag.flat ? 1 : Math.max(1, s.height | 0);
        const z0 = Math.max(0, z - Math.floor((h - 1) / 2)), z1 = Math.min(H - 1, z - Math.floor((h - 1) / 2) + h - 1);
        if (drag.tool === 'room') {
            // Pièce : pavé creux fermé d'épaisseur 1, dalle sur la couche courante, h couches d'air, plafond au-dessus.
            op.kind = 'box'; op.hollow = true; op.closed = true;
            op.a = [drag.start.x, drag.start.y, z]; op.b = [cell.x, cell.y, Math.min(H - 1, z + h + 1)];
        } else if (drag.tool === 'box' || drag.tool === 'line' || drag.tool === 'curve') {
            op.a = [drag.start.x, drag.start.y, drag.tool === 'box' ? z0 : z];
            op.b = [cell.x, cell.y, drag.tool === 'box' ? z1 : z];
            if (drag.tool === 'box') { op.hollow = !!s.hollow; op.thickness = Math.max(1, s.thickness | 0); }
            if (drag.tool === 'curve') op.c = midpoint(op.a, op.b);
        } else {
            const r = Math.max(Math.abs(cell.x - drag.start.x), Math.abs(cell.y - drag.start.y));
            op.a = [drag.start.x - r, drag.start.y - r, drag.tool === 'sphere' ? z - r : drag.tool === 'disc' ? z : z0];
            if (drag.tool === 'sphere') op.b = [drag.start.x + r, drag.start.y + r, z + r];
            else { op.kind = 'cylinder'; op.axis = 'z'; op.b = [drag.start.x + r, drag.start.y + r, drag.tool === 'disc' ? z : z1]; }
            op.hollow = !!s.hollow; op.thickness = Math.max(1, s.thickness | 0);
        }
        if (op.kind === 'line' || op.kind === 'curve') { op.hollow = false; op.thickness = 1; }
        if (drag.tool !== 'room') applyForm(op, st.form, st.formRot);   // pièce : toujours des cubes
        return op;
    }

    // Point de contrôle initial d'une courbe : le milieu de a–b (la courbe naît droite, puis suit la souris).
    function midpoint(a, b) { return [0, 1, 2].map(function (i) { return Math.floor((a[i] + b[i]) / 2); }); }

    // Libellé d'une forme (aperçu) ; pavé ou cylindre creux fermé aux deux bouts : suffixe « fermé ».
    function opLabel(st, op) {
        if (op.kind === 'cells') return (op.cells.length / 3) + ' cells';
        const b = opBounds(op);
        const w = b.x1 - b.x0 + 1, d = b.y1 - b.y0 + 1, h = b.z1 - b.z0 + 1;
        const closed = op.hollow && op.closed ? ' ' + ((st.options.labels && st.options.labels.closed) || 'closed') : '';
        if (op.kind === 'box') return w + '×' + d + ' h' + h + closed;
        if (op.kind === 'line') return 'L' + (Math.max(w, d, h));
        if (op.kind === 'curve') return 'C' + (Math.max(w, d, h));
        if (op.kind === 'sphere') return 'Ø' + Math.max(w, d);
        // Cylindre : diamètre sur les axes radiaux, longueur sur l'axe (h debout, L couché).
        const axis = opAxis(op), dia = axis === 'x' ? Math.max(d, h) : axis === 'y' ? Math.max(w, h) : Math.max(w, d), len = axis === 'x' ? w : axis === 'y' ? d : h;
        return 'Ø' + dia + (len > 1 ? (axis === 'z' ? ' h' : ' L') + len : '') + closed;
    }

    function translateOp(op, dx, dy, dz) {
        dz = dz || 0;
        if (op.kind === 'cells') { for (let i = 0; i + 2 < op.cells.length; i += 3) { op.cells[i] += dx; op.cells[i + 1] += dy; op.cells[i + 2] += dz; } return; }
        op.a[0] += dx; op.a[1] += dy; op.a[2] += dz; op.b[0] += dx; op.b[1] += dy; op.b[2] += dz;
        if (op.kind === 'curve') { op.c[0] += dx; op.c[1] += dy; op.c[2] += dz; }
    }

    // Déplacement clavier de la forme sélectionnée (flèches : une case ; Maj+PgUp/PgDn : une couche, la couche suit).
    // Déplace l'objet id de (dx, dy) sur son niveau, les objets posés dessus avec lui (glisser à la souris, flèches).
    function shiftObject(objects, id, dx, dy) {
        objects.forEach(function (o) { if (o.id === id || o.attachedTo === id) { o.x += dx; o.y += dy; } });
    }

    // Un pas aux flèches est refusé s'il sort l'ancre de l'objet de la grille (le glisser à la souris ne borne pas).
    function canShiftObject(obj, dx, dy, grid) {
        const x = obj.x + dx, y = obj.y + dy;
        return x >= 0 && y >= 0 && x < grid.width && y < grid.depth;
    }

    // Flèches : l'objet sélectionné avance d'une case, en une étape annulable ; un pas refusé est ignoré.
    // Renvoie true si la sélection est un objet (la touche est alors consommée, même quand le pas est refusé).
    function moveSelectedObject(st, dx, dy) {
        const f = st.selection && st.selection.kind === 'object' ? findObject(st, st.selection.id) : null;
        if (!f) return false;
        if (!canShiftObject(f.item, dx, dy, st.plan.grid)) return true;
        pushHistory(st);
        shiftObject(st.plan.levels[f.level].objects, f.item.id, dx, dy);
        commit(st, 'move');
        return true;
    }

    // PgUp / PgDn en 3D : l'objet sélectionné monte ou descend d'une case par son Z (hauteur de l'origine relative au niveau,
    // 1 = posé sur la dalle, rangé en null = « posé sur ce qu'il y a dessous »), sans sortir de l'air du niveau (1..hauteur).
    // Un objet posé sur un autre suit son support : pas refusé. Renvoie true si la sélection est un objet (touche consommée).
    function liftSelectedObject(st, dz) {
        const f = st.selection && st.selection.kind === 'object' ? findObject(st, st.selection.id) : null;
        if (!f) return false;
        const o = f.item, z = (o.z == null ? 1 : o.z) + dz;
        if (o.attachedTo || (dz < 0 && z < 1) || (dz > 0 && z > levelHeight(st, f.level))) return true;
        pushHistory(st);
        if (z === 1) delete o.z; else o.z = z;
        commit(st, 'move');
        return true;
    }

    // Flèches en 3D, relatives à la caméra : ↑ = s'éloigner (visée horizontale de v3dBasis, −sin lacet, −cos lacet en repère
    // du plan, ramenée à l'axe de la grille le plus proche), ↓ = se rapprocher, → / ← = droite / gauche de l'écran, toujours
    // perpendiculaires à cette visée (droite = (−fy, fx), comme v3dBasis.right). Renvoie [dx, dy], ou null hors flèches.
    function cameraArrowStep(yaw, key) {
        const fx = -Math.sin(yaw), fy = -Math.cos(yaw);
        const f = Math.abs(fx) > Math.abs(fy) ? [fx > 0 ? 1 : -1, 0] : [0, fy > 0 ? 1 : -1];
        return { ArrowUp: f, ArrowDown: [-f[0], -f[1]], ArrowRight: [-f[1], f[0]], ArrowLeft: [f[1], -f[0]] }[key] || null;
    }

    function moveSelectedOp(st, dx, dy, dz) {
        const op = st.selection && st.selection.kind === 'op' ? findOp(st, st.selection.id) : null;
        if (!op) return false;
        pushHistory(st);
        translateOp(op, dx, dy, dz);
        if (dz) setLayerInternal(st, st.layer + dz);
        commit(st, 'move');
        return true;
    }

    // ---- Presse-papiers (formes) : Ctrl+C / Ctrl+X / Ctrl+V -------------------------------------------------
    // La copie retient la case survolée et la couche courante ; le collage replace la forme de sorte que cette case
    // se retrouve sous le curseur, sur la couche courante (même décalage de couche). Sans survol : en place, décalée
    // d'une case en diagonale s'il s'agit d'une copie (un couper-coller se remet exactement en place).
    let clipboard = null;
    function hoverCell(st) { return st.pointerOver && st.hover && inGrid(st, st.hover.x, st.hover.y) ? st.hover : null; }
    function copySelection(st, cut) {
        const op = st.selection && st.selection.kind === 'op' ? findOp(st, st.selection.id) : null;
        if (!op) return false;
        clipboard = { op: clone(op), from: hoverCell(st), layer: st.layer, cut: !!cut };
        if (cut) deleteSelection(st);
        return true;
    }
    function pasteClipboard(st) {
        if (!clipboard) return false;
        const op = clone(clipboard.op), to = hoverCell(st);
        let dx = to && clipboard.from ? to.x - clipboard.from.x : 0, dy = to && clipboard.from ? to.y - clipboard.from.y : 0;
        const dz = st.layer - clipboard.layer;
        if (!dx && !dy && !dz && !clipboard.cut) { dx = 1; dy = 1; }
        translateOp(op, dx, dy, dz);
        clipboard.cut = false;   // un second collage au même endroit se décale
        return !!addOp(st, op);
    }

    // ---- Architecture : fond de plan ------------------------------------------------------------------------
    // Les pixels (st.bgImage, data URL côté C#) ne sont pas dans le document ; seul le placement (plan.architecture.image) l'est.

    function loadBgImage(st, dataUrl) {
        st.bgImage = null;
        st.staticDirty = true; requestRender(st);
        if (!dataUrl) return;
        const img = new Image();
        img.onload = function () { st.bgImage = img; st.staticDirty = true; requestRender(st); };
        img.src = dataUrl;
    }

    // Largeur (en cellules) pour tenir dans la grille en gardant le ratio.
    function fitImageWidth(st, img) {
        const W = st.plan.grid.width, D = st.plan.grid.depth;
        return Math.max(1, Math.min(W, D * img.width / img.height));
    }

    // Import : réduction à 1024 px maxi (JPEG 0,8) sur un canvas hors écran, puis placement par défaut et envoi à Blazor.
    function importBackgroundFile(st, file) {
        if (!file || !file.type || file.type.indexOf('image/') !== 0) return;
        const url = URL.createObjectURL(file);
        const img = new Image();
        img.onload = function () {
            URL.revokeObjectURL(url);
            const scale = Math.min(1, 1024 / Math.max(img.width, img.height));
            const c = document.createElement('canvas');
            c.width = Math.max(1, Math.round(img.width * scale)); c.height = Math.max(1, Math.round(img.height * scale));
            c.getContext('2d').drawImage(img, 0, 0, c.width, c.height);
            st.bgImage = c; st.bgVisible = true;
            if (!st.plan.architecture.image) {
                pushHistory(st);
                st.plan.architecture.image = { x: 0, y: 0, width: fitImageWidth(st, c), opacity: 0.5 };
                commit(st, 'image');
            } else { st.staticDirty = true; requestRender(st); }
            if (st.dotnetRef) st.dotnetRef.invokeMethodAsync('OnBackgroundImageChanged', c.toDataURL('image/jpeg', 0.8)).catch(function () { });
        };
        img.onerror = function () { URL.revokeObjectURL(url); };
        img.src = url;
    }

    // Import d'un plan JSON : le texte est remis à Blazor qui le valide (PlanDocumentJson) et le charge par setPlan ;
    // null signale un fichier trop grand.
    function importPlanFile(st, file) {
        if (!file || !st.dotnetRef) return;
        if (file.size > MAX_PLAN_FILE_BYTES) { st.dotnetRef.invokeMethodAsync('OnPlanFileImported', null).catch(function () { }); return; }
        file.text().then(function (text) { return st.dotnetRef.invokeMethodAsync('OnPlanFileImported', text); }).catch(function () { });
    }

    // ---- Historique -------------------------------------------------------------------------------------

    function pushHistory(st) {
        st.history.push(JSON.stringify(st.plan));
        if (st.history.length > MAX_HISTORY) st.history.shift();
        st.future = [];
    }

    function commit(st, label) {
        st.dirty = true;
        st.staticDirty = true;
        refreshVox(st);   // avant les pièces : les formes les ferment
        reconcileRooms(st);
        recomputeFootprints(st);
        saveDraft(st);
        notifyPlan(st);
        requestRender(st);
    }

    function notifyPlan(st) {
        if (!st.dotnetRef) return;
        st.dotnetRef.invokeMethodAsync('OnPlanChanged', JSON.stringify(st.plan), st.history.length > 0, st.future.length > 0, st.dirty)
            .catch(function () { /* circuit fermé */ });
    }
    function notifySelection(st) {
        if (!st.dotnetRef) return;
        st.dotnetRef.invokeMethodAsync('OnSelectionChanged', st.selection ? st.selection.kind : null, st.selection ? st.selection.id : null).catch(function () { });
    }
    function notifyTool(st) {
        if (!st.dotnetRef) return;
        st.dotnetRef.invokeMethodAsync('OnToolChanged', st.tool).catch(function () { });
    }
    function notifyObjectType(st) {
        if (!st.dotnetRef) return;
        st.dotnetRef.invokeMethodAsync('OnObjectTypeChanged', st.objectType).catch(function () { });
    }
    function notifyMaterial(st) {
        if (!st.dotnetRef) return;
        st.dotnetRef.invokeMethodAsync('OnMaterialPicked', st.material).catch(function () { });
    }
    function notifyForm(st) {
        if (!st.dotnetRef) return;
        st.dotnetRef.invokeMethodAsync('OnFormPicked', st.form, st.formRot).catch(function () { });
    }

    // Forme de pose (Cube par défaut), retenue pour le matériau courant ; notify : prévenir la palette (changement venu du canvas).
    function setFormInternal(st, form, rot, notify) {
        st.form = form && form !== 'Cube' ? form : 'Cube';
        st.formRot = st.form === 'Cube' ? 0 : (rot | 0) & 3;
        if (st.material) st.formByMaterial[st.material] = st.form;
        if (notify) notifyForm(st);
        requestRender(st);
    }
    function formRotatable(st, material, form) {
        const m = material ? st.materialsByName[material] : null;
        const f = m && m.forms ? m.forms.find(function (x) { return x.name === form; }) : null;
        return !!(f && f.rotatable);
    }

    function draftKey(st) { return 'ecoBuildingPlanner.draft.' + (st.catalog.serverId || 'default'); }
    function saveDraft(st) { try { localStorage.setItem(draftKey(st), JSON.stringify(st.plan)); } catch (e) { /* quota / privé */ } }
    function rawDraft(st) { try { return localStorage.getItem(draftKey(st)); } catch (e) { return null; } }   // texte brut : Blazor le migre (C#) avant setPlan
    function loadDraft(st) { try { const s = rawDraft(st); return s ? JSON.parse(s) : null; } catch (e) { return null; } }
    function clearDraft(st) { try { localStorage.removeItem(draftKey(st)); } catch (e) { } }

    // ---- Pièces = poches d'air fermées ----------------------------------------------------------------
    // Le jeu (RoomChecker) n'a ni sol ni plafond : une pièce est une poche d'air fermée par des blocs. detectPockets en donne
    // l'aperçu (teintes, graines) ; l'analyse C# vérifie chaque graine (tier, volume, plafond, distance, arêtes vides).
    const MAX_ROOMS = 200;               // = PlanValidator.MaxRooms (Error bloquante côté C#)
    const MIN_POCKET_CELLS = 3;          // = RoomChecker : volume ≤ 2 rejeté
    const POCKET_MAX_VOLUME = 2000000;   // boîte de détection plus grande (tour Eiffel…) : détection sautée, pièces du document gardées

    function nextRoomNumber(st) {
        const label = (st.options.roomLabel || 'Room') + ' ';
        let max = 0;
        st.plan.levels.forEach(function (level) {
            level.rooms.forEach(function (room) {
                if (!room.name || room.name.indexOf(label) !== 0) return;
                const suffix = room.name.slice(label.length);
                const n = parseInt(suffix, 10);
                if (!isNaN(n) && String(n) === suffix && n > max) max = n;
            });
        });
        return max + 1;
    }

    // Plein en (x, y, z) pour les pièces, miroir de VoxelGrid.Get côté C# : sous la couche 0 tout est plein, hors grille (x, y)
    // la couche 0 est du terrain et au-dessus de l'air, un bloc posé est plein, une case de la couche 0 qu'aucune op n'a couverte
    // est du terrain intact ; objWalls (facultatif) = index des cellules Wall des objets (portes : ObjectWall côté C#).
    function solidAt(vox, x, y, z, objWalls) {
        if (z < 0) return true;
        if (x < 0 || y < 0 || x >= vox.W || y >= vox.D || z >= vox.H) return z === 0;
        const i = x + vox.W * (y + vox.D * z);
        return vox.cells[i] !== 0 || (z === 0 && vox.owner[i] === 0) || (!!objWalls && objWalls.has(i));
    }

    // Poches d'air fermées dans la boîte des cellules couvertes par les ops + 1 case autour, de la couche 0 à une couche au-dessus
    // de la plus haute. Propagation orthogonale seulement, comme l'EmptySpace du jeu : une diagonale ne fait ni fuir ni grandir
    // une pièce (c'est une arête vide). labels (index x + nx·(y + ny·z) dans la boîte) : 0 plein, −1 extérieur (air relié à la face
    // haute, poche de moins de 3 cases ou au-delà de MAX_ROOMS), n > 0 poche n ; pockets[n − 1] = { count, minZ, seed: { x, y, z } }
    // en coordonnées du plan, graine = case de la couche la plus basse la plus proche du centroïde de cette couche (à égalité, la
    // première dans l'ordre x, y). skipped : boîte de plus de POCKET_MAX_VOLUME cases, rien n'est calculé.
    function detectPockets(vox, objWalls) {
        const W = vox.W, D = vox.D, H = vox.H;
        let x0 = W, y0 = D, x1 = -1, y1 = -1, z1 = -1;
        for (let z = 0, i = 0; z < H; z++) for (let y = 0; y < D; y++) for (let x = 0; x < W; x++, i++) {
            if (!vox.owner[i]) continue;
            if (x < x0) x0 = x; if (x > x1) x1 = x; if (y < y0) y0 = y; if (y > y1) y1 = y; z1 = z;
        }
        const out = { skipped: false, x0: x0 - 1, y0: y0 - 1, nx: x1 - x0 + 3, ny: y1 - y0 + 3, nz: z1 + 2, labels: null, pockets: [] };
        if (x1 < 0) return out;
        const nx = out.nx, ny = out.ny, nz = out.nz, nxy = nx * ny, V = nxy * nz;
        if (V > POCKET_MAX_VOLUME) { out.skipped = true; return out; }
        const labels = new Int32Array(V), queue = new Int32Array(V);
        for (let z = 0, j = 0; z < nz; z++) for (let y = 0; y < ny; y++) for (let x = 0; x < nx; x++, j++) if (!solidAt(vox, out.x0 + x, out.y0 + y, z, objWalls)) labels[j] = -2;   // air non visité
        // Composante 6-connexe d'air non visité de start, étiquetée lbl ; ses cases sont queue[0..n).
        const flood = function (start, lbl) {
            let n = 0, head = 0;
            const push = function (k) { if (labels[k] === -2) { labels[k] = lbl; queue[n++] = k; } };
            push(start);
            while (head < n) {
                const j = queue[head++], x = j % nx, y = Math.floor(j / nx) % ny, z = Math.floor(j / nxy);
                if (x > 0) push(j - 1); if (x < nx - 1) push(j + 1);
                if (y > 0) push(j - nx); if (y < ny - 1) push(j + nx);
                if (z > 0) push(j - nxy); if (z < nz - 1) push(j + nxy);
            }
            return n;
        };
        for (let j = nxy * (nz - 1); j < V; j++) flood(j, -1);   // extérieur : depuis la face haute (tout air)
        for (let j = 0; j < V; j++) {
            if (labels[j] !== -2) continue;
            const lbl = out.pockets.length + 1, n = flood(j, lbl);
            if (n < MIN_POCKET_CELLS || lbl > MAX_ROOMS) { for (let k = 0; k < n; k++) labels[queue[k]] = -1; continue; }
            // Le scan croissant rencontre la poche par sa couche la plus basse (celle de j).
            const mz = Math.floor(j / nxy);
            let sx = 0, sy = 0, c = 0, best = j, bestD = Infinity;
            for (let k = 0; k < n; k++) { const q = queue[k]; if (Math.floor(q / nxy) === mz) { sx += q % nx; sy += Math.floor(q / nx) % ny; c++; } }
            for (let k = 0; k < n; k++) {
                const q = queue[k];
                if (Math.floor(q / nxy) !== mz) continue;
                const dx = q % nx - sx / c, dy = Math.floor(q / nx) % ny - sy / c, d = dx * dx + dy * dy;
                if (d < bestD || (d === bestD && q < best)) { bestD = d; best = q; }
            }
            out.pockets.push({ count: n, minZ: mz, seed: { x: out.x0 + best % nx, y: out.y0 + Math.floor(best / nx) % ny, z: mz } });
        }
        out.labels = labels;
        return out;
    }

    // Étiquette de detectPockets en (x, y, z) du plan ; −1 hors de la boîte (extérieur) ou sans détection.
    function pocketAt(P, x, y, z) {
        if (!P.labels) return -1;
        x -= P.x0; y -= P.y0;
        return x < 0 || y < 0 || z < 0 || x >= P.nx || y >= P.ny || z >= P.nz ? -1 : P.labels[x + P.nx * (y + P.ny * z)];
    }

    // Poches du bâti courant (cache) : recalculées seulement si les blocs, la couche 0 creusée ou les cellules-murs des objets
    // ont changé — au plus un flood par commit, rien quand on déplace un meuble qui n'est pas une porte. Signale à Blazor le
    // passage en détection sautée, et le retour.
    function ensurePockets(st) {
        const vox = st.vox, objWalls = v3dObjectCells(st).walls, sig = Array.from(objWalls).join(','), c = st.pocketCache;
        if (c && c.sig === sig && sameBlocks(c.vox, vox)) { c.vox = vox; return c.pockets; }
        const P = detectPockets(vox, objWalls);
        st.pocketCache = { vox: vox, sig: sig, pockets: P };
        if (P.skipped !== !!st.roomDetectionSkipped) {
            st.roomDetectionSkipped = P.skipped;
            if (st.dotnetRef) st.dotnetRef.invokeMethodAsync('OnRoomDetectionChanged', P.skipped).catch(function () { });
        }
        return P;
    }
    function sameBlocks(a, b) {
        if (a === b) return true;
        if (a.W !== b.W || a.D !== b.D || a.H !== b.H) return false;
        for (let i = 0; i < a.cells.length; i++) if (a.cells[i] !== b.cells[i]) return false;
        for (let i = 0; i < a.W * a.D; i++) if (!a.owner[i] !== !b.owner[i]) return false;   // couche 0 : terrain intact ou creusé
        return true;
    }

    // Poche désignée par une pièce du niveau k : celle de sa graine (z relatif à la dalle), sinon la première poche libre de sa
    // colonne dans la tranche du niveau ; 0 si aucune.
    function roomPocket(st, P, room, k, claimed) {
        const s = room.seed, base = levelBaseY(st, k);
        let n = pocketAt(P, s.x, s.y, base + s.z);
        if (n > 0 && !claimed[n]) return n;
        const top = k + 1 < st.plan.levels.length ? levelBaseY(st, k + 1) : P.nz;
        for (let z = base; z < top; z++) { n = pocketAt(P, s.x, s.y, z); if (n > 0 && !claimed[n]) return n; }
        return 0;
    }

    // Pièces → poches, sans rien modifier (en cas de fusion, la première pièce dans l'ordre du document gagne) : pose
    // st.pocketRooms et renvoie { n° de poche: pièce }. Seul appel de restorePlan : undo/redo restaurent la liste exacte.
    function mapRoomsToPockets(st) {
        const P = ensurePockets(st), claimed = {};
        st.pocketRooms = {};
        if (P.skipped) return claimed;
        st.plan.levels.forEach(function (level, k) {
            level.rooms.forEach(function (room) { const n = roomPocket(st, P, room, k, claimed); if (n > 0) claimed[n] = room; });
        });
        for (const n in claimed) st.pocketRooms[n] = claimed[n].id;
        return claimed;
    }

    // Une pièce par poche : une pièce qui désigne une poche garde id/nom/réglages et prend la graine de la poche, au niveau
    // de sa couche la plus basse ; une poche orpheline reçoit une pièce neuve ; les autres pièces sont supprimées. Détection
    // sautée : les pièces du document restent telles quelles (le C# vérifie leurs graines). Appelé par commit et setPlanInternal.
    function reconcileRooms(st) {
        const claimed = mapRoomsToPockets(st), P = st.pocketCache.pockets;
        if (P.skipped) return;
        const pocketOf = new Map(), out = st.plan.levels.map(function () { return []; });
        for (const n in claimed) pocketOf.set(claimed[n], +n);
        const place = function (room, n) {
            const p = P.pockets[n - 1], k = levelIndexAtY(st, p.minZ);
            room.seed = { x: p.seed.x, y: p.seed.y, z: p.seed.z - levelBaseY(st, k) };
            out[k].push(room);
        };
        st.plan.levels.forEach(function (level) { level.rooms.forEach(function (room) { if (pocketOf.has(room)) place(room, pocketOf.get(room)); }); });
        let nextNum = 0;   // calculé paresseusement, seulement si une pièce est créée
        P.pockets.forEach(function (p, i) {
            if (claimed[i + 1]) return;
            if (!nextNum) nextNum = nextRoomNumber(st);
            const room = { id: uid('r'), name: (st.options.roomLabel || 'Room') + ' ' + (nextNum++), seed: null, lockCategory: null };
            st.pocketRooms[i + 1] = room.id;
            place(room, i + 1);
        });
        st.plan.levels.forEach(function (level, k) { level.rooms = out[k]; });
        if (st.selection && st.selection.kind === 'room' && !findRoom(st, st.selection.id)) select(st, null, null);
    }

    // Couches montrées par le plan 2D [z0, z1] : la couche courante en Structure, la tranche du niveau en Aménagement (le dernier
    // niveau absorbe tout ce qui est au-dessus, comme levelIndexAtY : les combles lui appartiennent).
    function viewLayers(st) {
        if (isArch(st)) return [st.layer, st.layer];
        const k = st.level;
        return [levelBaseY(st, k), k + 1 < st.plan.levels.length ? levelBaseY(st, k + 1) - 1 : archH(st) - 1];
    }

    // Empreinte des pièces sur les couches affichées : roomGrid[x + W·y] = poche de la première couche (de bas en haut) qui en a
    // une ; roomLabels = case de chaque empreinte la plus proche de son centre (ancre de l'étiquette).
    function recomputeFootprints(st) {
        const W = st.plan.grid.width, D = st.plan.grid.depth, P = st.pocketCache && st.pocketCache.pockets, r = viewLayers(st);
        const grid = st.roomGrid = new Int32Array(W * D), sums = {};
        st.roomLabels = {};
        if (!P || !P.labels) return;
        const z0 = Math.max(0, r[0]), z1 = Math.min(P.nz - 1, r[1]);
        for (let y = Math.max(0, P.y0); y < Math.min(D, P.y0 + P.ny); y++) for (let x = Math.max(0, P.x0); x < Math.min(W, P.x0 + P.nx); x++) {
            for (let z = z0; z <= z1; z++) {
                const n = pocketAt(P, x, y, z);
                if (n <= 0) continue;
                grid[x + W * y] = n;
                const s = sums[n] || (sums[n] = { x: 0, y: 0, c: 0, d: Infinity });
                s.x += x; s.y += y; s.c++;
                break;
            }
        }
        for (let i = 0; i < grid.length; i++) {
            const n = grid[i];
            if (n <= 0) continue;
            const s = sums[n], dx = i % W - s.x / s.c, dy = ((i / W) | 0) - s.y / s.c, d = dx * dx + dy * dy;
            if (d < s.d) { s.d = d; st.roomLabels[n] = { x: i % W, y: (i / W) | 0 }; }
        }
    }

    function roomAt(st, x, y) {
        const n = st.roomGrid && inGrid(st, x, y) ? st.roomGrid[x + st.plan.grid.width * y] : 0;
        return n > 0 ? st.pocketRooms[n] || null : null;
    }

    // ---- Objets ---------------------------------------------------------------------------------------

    function objectCells(st, obj) {
        const info = st.objectsByName[obj.type];
        if (!info) return [{ x: obj.x, y: obj.y, kind: KIND.OCCUPIED, dz: 0 }];
        const out = [];
        info.cells.forEach(function (c) {
            const kind = c[3];
            if (kind === KIND.NONE || kind === KIND.WATER) return;
            const r = rotate({ x: c[0], y: c[1], z: c[2] }, obj.rotation || 0);
            out.push({ x: obj.x + r.x, y: obj.y + r.z, kind: kind, dz: r.y });
        });
        return out.length ? out : [{ x: obj.x, y: obj.y, kind: KIND.OCCUPIED, dz: 0 }];
    }

    function objectAt(st, x, y) {
        // Priorité aux objets empilés (dessinés au-dessus), puis aux objets au sol — sur le niveau affiché.
        let found = null;
        cur(st).objects.forEach(function (o) {
            if (o.attachedTo) { if (o.x === x && o.y === y) found = o; return; }
            if (found && found.attachedTo) return;
            if (objectCells(st, o).some(function (c) { return c.x === x && c.y === y; })) found = o;
        });
        return found;
    }

    function analysisObject(st, id) {
        if (!st.analysis || !st.analysis.objects) return null;
        return st.analysis.objects.find(function (o) { return o.id === id; }) || null;
    }

    function analysisRoom(st, id) {
        if (!st.analysis || !st.analysis.rooms) return null;
        return st.analysis.rooms.find(function (r) { return r.roomId === id; }) || null;
    }

    function iconFor(st, name) {
        if (st.icons[name] !== undefined) return st.icons[name];
        const img = new Image();
        st.icons[name] = null;
        img.onload = function () { st.icons[name] = img; st.staticDirty = true; requestRender(st); };
        img.onerror = function () { st.icons[name] = false; };
        img.src = '/assets/eco-icons/' + name + '.png?serverId=' + (st.catalog.serverId || '');
        return null;
    }

    // ---- Vue 3D (WebGL, lecture seule) ----------------------------------------------------------------
    // Une autre façon de dessiner st.vox (les formes) et le mobilier : faces visibles des cubes, couleur de tier
    // ombrée par face, icône de l'item sur les cubes de mobilier (atlas de textures), couche courante teintée, option
    // « couper au-dessus de la couche ». Caméra orbitale ; aucune édition. Coordonnées : plan (x, y, z) → GL (x, −y, z)
    // pour rester en repère direct (le plan a y vers le bas).
    // Les blocs d'un matériau dont le bundle de formes est chargé (voir « Formes de blocs ») sont dessinés avec le vrai mesh du
    // jeu (mur mince, escalier, pan de toit…) et son matériau à trois textures, comme le shader du jeu : un sommet à uv (−2, −2)
    // (normale non nulle) prend la texture de côté en triplanaire (unité 1), (−3, −3) celle de dessus en triplanaire (unité 2),
    // et un sommet à uv ≥ 0 avec normale non nulle la texture de détail échantillonnée à ses uv (unité 3) ; l'atlas d'icônes
    // reste en unité 0 (uv ≥ 0, normale nulle). Les trois sont éclairés par une directionnelle + un ambiant hémisphérique ;
    // vColor n'y sert qu'à la couleur de sélection.
    const V3D_MAX_FACES = 2000000, V3D_ATLAS = 2048, V3D_SLOT = 64;
    const V3D_SHADE = { pz: 1.0, nz: 0.45, px: 0.8, nx: 0.65, py: 0.7, ny: 0.85 };
    const V3D_VS = 'attribute vec3 aPos; attribute vec4 aColor; attribute vec2 aUv; attribute vec3 aNormal; uniform mat4 uMvp;' +
        'varying vec3 vColor; varying vec2 vUv; varying vec3 vNrm; varying vec3 vPos; varying float vMode;' +
        'void main() { gl_Position = uMvp * vec4(aPos, 1.0); vColor = aColor.rgb; vUv = aUv; vNrm = aNormal; vPos = aPos;' +
        ' vMode = length(aNormal) < 0.5 ? (aUv.x >= 0.0 ? 1.0 : 0.0) : (aUv.x < -2.5 ? 3.0 : aUv.x < -1.5 ? 2.0 : aUv.x < -0.5 ? 0.0 : 4.0); }';   // 0 couleur unie, 1 icône de meuble, 2 côté triplanaire, 3 dessus triplanaire, 4 détail aux uv
    const V3D_FS = '#ifdef GL_FRAGMENT_PRECISION_HIGH\nprecision highp float;\n#else\nprecision mediump float;\n#endif\n' +
        'varying vec3 vColor; varying vec2 vUv; varying vec3 vNrm; varying vec3 vPos; varying float vMode;' +
        'uniform sampler2D uTex; uniform sampler2D uMat; uniform sampler2D uTop; uniform sampler2D uDetail; uniform float uAlpha; uniform float uTexScale; uniform float uTexOffset;' +
        // Coupe face caméra : plan (n, w) en coordonnées du plan (vPos = aPos, y non inversé) ; côté caméra jeté. Coupe éteinte : (0, 0, 0, −1).
        'uniform vec4 uCut;' +
        // Comme Triplanar.cginc du jeu : uv = position (blocs) × _TextureScale + _TextureOffset, par texture de matériau.
        'vec3 tri(sampler2D s, vec3 w) { return texture2D(s, vec2(vPos.y, -vPos.z) * uTexScale + uTexOffset).rgb * w.x + texture2D(s, vec2(vPos.x, -vPos.z) * uTexScale + uTexOffset).rgb * w.y + texture2D(s, vPos.xy * uTexScale + uTexOffset).rgb * w.z; }' +
        'void main() { if (dot(uCut.xyz, vPos) + uCut.w > 0.001) discard; vec3 c = vColor; vec4 ti = texture2D(uTex, vUv);' +
        ' vec3 n = normalize(vNrm + vec3(0.0, 0.0, 1e-6)); vec3 w = n * n; w = w * w; w /= max(w.x + w.y + w.z, 1e-6);' +
        ' vec3 side = tri(uMat, w), top = tri(uTop, w), det = texture2D(uDetail, vUv).rgb; vec3 tp = vMode > 3.5 ? det : vMode > 2.5 ? top : side;' +
        ' float l = min(1.0, 0.45 + 0.2 * n.z + 0.5 * max(dot(n, normalize(vec3(0.4, -0.6, 0.7))), 0.0));' +
        ' if (vMode > 1.5) c = tp * l * vColor; else if (vMode > 0.5) c = mix(c, ti.rgb, ti.a);' +
        ' gl_FragColor = vec4(c, uAlpha); }';

    function parseColor(str, fallback) {
        let m = /^#([0-9a-f]{6})$/i.exec(str || '');
        if (m) { const v = parseInt(m[1], 16); return [(v >> 16) & 255, (v >> 8) & 255, v & 255]; }
        m = /rgba?\(\s*(\d+)\s*,\s*(\d+)\s*,\s*(\d+)/i.exec(str || '');
        if (m) return [+m[1], +m[2], +m[3]];
        return fallback;
    }

    // Tampon de sommets à croissance géométrique : pos (3 float), couleur (4 u8 : rgb + couche du bloc), uv (2 float),
    // normale (3 float, nulle pour les faces unies et les icônes).
    function v3dBuffer(capacity) {
        return { n: 0, cap: capacity, pos: new Float32Array(capacity * 3), col: new Uint8Array(capacity * 4), uv: new Float32Array(capacity * 2), nrm: new Float32Array(capacity * 3) };
    }
    function v3dGrow(b, extra) {
        if (b.n + extra <= b.cap) return;
        let cap = b.cap * 2; while (cap < b.n + extra) cap *= 2;
        const pos = new Float32Array(cap * 3), col = new Uint8Array(cap * 4), uv = new Float32Array(cap * 2), nrm = new Float32Array(cap * 3);
        pos.set(b.pos.subarray(0, b.n * 3)); col.set(b.col.subarray(0, b.n * 4)); uv.set(b.uv.subarray(0, b.n * 2)); nrm.set(b.nrm.subarray(0, b.n * 3));
        b.pos = pos; b.col = col; b.uv = uv; b.nrm = nrm; b.cap = cap;
    }
    function v3dVertex(b, x, y, z, r, g, bl, layer, u, v, nx, ny, nz) {
        const i = b.n++;
        b.pos[i * 3] = x; b.pos[i * 3 + 1] = y; b.pos[i * 3 + 2] = z;
        b.col[i * 4] = r; b.col[i * 4 + 1] = g; b.col[i * 4 + 2] = bl; b.col[i * 4 + 3] = layer;
        b.uv[i * 2] = u; b.uv[i * 2 + 1] = v;
        b.nrm[i * 3] = nx || 0; b.nrm[i * 3 + 1] = ny || 0; b.nrm[i * 3 + 2] = nz || 0;
    }
    // Quad p0 p1 p2 p3 (ordre quelconque, pas de culling GL) ; uv = null → face unie, sinon [u0, v0, u1, v1] appliqué
    // dans l'ordre des sommets (p0 = haut gauche de l'icône, p1 = haut droit, p2 = bas droit, p3 = bas gauche).
    function v3dQuad(b, p0, p1, p2, p3, c, layer, uv) {
        v3dGrow(b, 6);
        const pts = [p0, p1, p2, p3], us = uv ? [uv[0], uv[2], uv[2], uv[0]] : null, vs = uv ? [uv[1], uv[1], uv[3], uv[3]] : null;
        [0, 1, 2, 0, 2, 3].forEach(function (k) { v3dVertex(b, pts[k][0], pts[k][1], pts[k][2], c[0], c[1], c[2], layer, us ? us[k] : -1, vs ? vs[k] : -1); });
    }

    function v3dAtlasSlot(st, name) {
        const a = st.view3d.atlas;
        if (a.slots[name] !== undefined) return a.slots[name];
        const img = iconFor(st, name);
        if (img === null) { a.pending[name] = true; return -1; }   // en cours de chargement : on reconstruira
        if (img === false) { a.slots[name] = -1; return -1; }
        const per = V3D_ATLAS / V3D_SLOT;
        if (a.next >= per * per) { a.slots[name] = -1; return -1; }
        const slot = a.next++, sx = (slot % per) * V3D_SLOT, sy = Math.floor(slot / per) * V3D_SLOT;
        const s = Math.min(V3D_SLOT / img.width, V3D_SLOT / img.height), w = img.width * s, h = img.height * s;
        a.ctx.drawImage(img, sx + (V3D_SLOT - w) / 2, sy + (V3D_SLOT - h) / 2, w, h);
        a.slots[name] = slot; a.dirty = true; delete a.pending[name];
        return slot;
    }
    function v3dSlotUv(slot) {
        const per = V3D_ATLAS / V3D_SLOT, u0 = (slot % per) / per, v0 = Math.floor(slot / per) / per, e = 1 / per, pad = 0.5 / V3D_ATLAS;
        return [u0 + pad, v0 + pad, u0 + e - pad, v0 + e - pad];
    }

    // ---- Formes de blocs (bundles servis en statique sous /assets/forms) ------------------------------------
    // index.json = { sets: { Set: { json, atlas } }, materials: { MaterialItem: Set } } ; <Set>.json (version 3) = { tile: côté d'une tuile de l'atlas (px),
    // meshes: { nom: { pos, nrm, idx, glass?, slots?: [idx du slot FBX 1, 2…], uv?: [u, v par sommet], chan?: [0 | 1 | 2 par sommet] } },
    // forms: { Forme: [ { mesh, rot } × 4 (index = rot de l'op) ] ou { bitMeans, cases: [ { mesh, rot, conds: [[dx, dy, dz, attendu, bitMeans?]…] }… ] } },
    // categories: { Forme: catégorie du jeu }, materials: [ { side, top, detail, scale, offset } ] (tuiles de l'atlas, top/detail null = side ;
    // scale/offset = projection triplanaire), skins: { MaterialItem: index de matériau }, formMaterials: { MaterialItem: { Forme: index } } (surcharges
    // par forme, ex. toit), slotMaterials?: { MaterialItem: { Forme: [index du matériau des slots 1, 2… | null] } } (bloc à plusieurs
    // matériaux : poutres, plâtre et bois des murs FarEast ; slot sans matériau = celui de la forme), glass?: [r, g, b, a] } ;
    // l'atlas WebP est carré, en tuiles de tile px (index ligne-major).
    // chan d'un sommet = texture du matériau qu'il prend : 0 côté (triplanaire), 1 dessus (triplanaire), 2 détail (aux uv du mesh, v inversé) ;
    // mesh sans chan = tout côté. glass d'un mesh = triangles translucides (vitres) sur les mêmes sommets, idx ne gardant que l'opaque ;
    // glass du set = teinte du verre en 0..1 (défaut noir à 26 %), dessinée après tout l'opaque en mélange alpha sans écrire la profondeur.
    // Forme contextuelle : premier cas dont toutes les conditions sont satisfaites (rotations déjà expansées par l'extracteur,
    // cas sans conds = repli, aucun cas = cube du set). Meshes dans le repère plan
    // (x est, y sud, z haut), centrés dans [−0.5, 0.5]³ ; rot = degrés autour de z, cuits à la préparation (une copie par angle).
    // Un matériau du plan présent dans index.materials déclenche le chargement de son set ; tant qu'il manque (ou s'il n'existe
    // pas : serveur sans bundle), ses blocs restent des cubes de couleur unie.
    // Objets (meubles, portes) : index.objects = { ObjetItem: Set } ; le set porte objects: { ObjetItem: { parts: [ { mesh, material } ],
    // rot: [degrés planner × 4 (index = rotation de l'objet)] } } (extract_objects.py). Le mesh, centré sur la case d'ancrage de l'objet
    // et pouvant déborder sur ses autres cases (porte 1×2), est dessiné une fois ; les autres cases de son occupancy restent vides.
    // Sans set chargé, un objet reste des cubes gris à icône sur toutes ses cases.
    const FORMS_URL = '/assets/forms/', FORMS_TILE = 512;
    const FORMS_FACES = [[1, 0, 0], [-1, 0, 0], [0, 1, 0], [0, -1, 0], [0, 0, 1], [0, 0, -1]];   // faces du cube : +x −x +y −y +z −z

    // Face du cube unitaire portant chaque triangle (trois sommets à ±0.5 sur un axe, normale dans ce sens), −1 sinon :
    // ces triangles sont masqués quand le voisin de ce côté est un bloc plein, comme les faces des cubes.
    function meshFaces(pos, nrm, idx) {
        const out = new Int8Array(idx.length / 3);
        for (let t = 0; t < idx.length; t += 3) {
            let f = -1;
            for (let k = 0; k < 6 && f < 0; k++) {
                const d = FORMS_FACES[k], ax = d[0] ? 0 : d[1] ? 1 : 2, s = d[ax];
                let on = true;
                for (let j = 0; j < 3 && on; j++) { const v = idx[t + j] * 3; on = Math.abs(pos[v + ax] - s * 0.5) < 1e-3 && nrm[v + ax] * s > 0.5; }
                if (on) f = k;
            }
            out[t / 3] = f;
        }
        return out;
    }

    // Copie du mesh tournée de deg degrés (multiple de 90) autour de z, repère plan : 90° envoie (x, y) sur (−y, x), normales comprises ;
    // glass (indices), uv et chan (par sommet) sont recopiés tels quels, null s'ils manquent ; chaque slot devient un mesh (subs) qui
    // partage les sommets.
    function formsRotateMesh(mesh, deg) {
        const q = Math.round(deg / 90) & 3, pos = Float32Array.from(mesh.pos), nrm = Float32Array.from(mesh.nrm), idx = Uint32Array.from(mesh.idx);
        const turn = function (a) {
            for (let i = 0; i < a.length; i += 3) {
                const x = a[i], y = a[i + 1];
                if (q === 1) { a[i] = -y; a[i + 1] = x; } else if (q === 2) { a[i] = -x; a[i + 1] = -y; } else if (q === 3) { a[i] = y; a[i + 1] = -x; }
            }
        };
        turn(pos); turn(nrm);
        const uv = Array.isArray(mesh.uv) ? Float32Array.from(mesh.uv) : null, chan = Array.isArray(mesh.chan) ? Uint8Array.from(mesh.chan) : null;
        const shade = Array.isArray(mesh.shade) ? Uint8Array.from(mesh.shade) : null;
        const subs = (Array.isArray(mesh.slots) ? mesh.slots : []).map(function (s) {
            const si = Uint32Array.from(Array.isArray(s) ? s : []);
            return { pos: pos, nrm: nrm, idx: si, uv: uv, chan: chan, shade: shade, faces: meshFaces(pos, nrm, si) };
        });
        const glass = Array.isArray(mesh.glass) ? Uint32Array.from(mesh.glass) : null;
        return { pos: pos, nrm: nrm, idx: idx, glass: glass, glassFaces: glass && meshFaces(pos, nrm, glass),
            uv: uv, chan: chan, shade: shade, faces: meshFaces(pos, nrm, idx), subs: subs };
    }

    // Cube unitaire (6 faces, normales) : repli d'un set sans forme Cube.
    function formsCubeMesh() {
        const pos = [], nrm = [], idx = [], chan = [], uv = [];
        FORMS_FACES.forEach(function (d, k) {
            const ax = d[0] ? 0 : d[1] ? 1 : 2, u = (ax + 1) % 3, w = (ax + 2) % 3;
            [[-0.5, -0.5], [0.5, -0.5], [0.5, 0.5], [-0.5, 0.5]].forEach(function (c) {
                const p = [0, 0, 0]; p[ax] = d[ax] * 0.5; p[u] = c[0]; p[w] = c[1];
                pos.push(p[0], p[1], p[2]); nrm.push(d[0], d[1], d[2]); chan.push(ax === 2 ? 1 : 0); uv.push(0, 0);   // dessus/dessous : texture « top », comme les cubes du jeu
            });
            idx.push(k * 4, k * 4 + 1, k * 4 + 2, k * 4, k * 4 + 2, k * 4 + 3);
        });
        return formsRotateMesh({ pos: pos, nrm: nrm, idx: idx, chan: chan, uv: uv }, 0);
    }

    // Prépare un set du bundle : meshes tournés (une copie par angle réellement référencé), formes indexées, cube de repli.
    // Forme orientée → { list: [mesh × 4] } ; forme contextuelle → { bitMeans, cases: [{ mesh, conds }] } ; mesh invalide → null.
    // Bundle sans materials (v1/v2 : skins = tuile) : un matériau côté seul par skin.
    function formsPrepareSet(json) {
        const meshes = {}, forms = {};
        let materials = json.materials, skins = json.skins || {};
        if (!Array.isArray(materials)) {
            materials = []; const bySkin = {};
            Object.keys(skins).forEach(function (name) { bySkin[name] = materials.push({ side: skins[name] | 0, top: null, detail: null, scale: 1, offset: 0 }) - 1; });
            skins = bySkin;
        }
        const get = function (entry) {
            const src = entry && json.meshes && json.meshes[entry.mesh];
            if (!src || !Array.isArray(src.pos) || !Array.isArray(src.nrm) || !Array.isArray(src.idx)) return null;
            const deg = (Math.round((entry.rot || 0) / 90) & 3) * 90, byRot = meshes[entry.mesh] || (meshes[entry.mesh] = {});
            return byRot[deg] || (byRot[deg] = formsRotateMesh(src, deg));
        };
        // Jeu de cas contextuels { bitMeans, cases } ; une forme « rots » (v4, quais) en porte un par rotation de l'op.
        const group = function (g) {
            return { bitMeans: (g && g.bitMeans) || '', cases: (g && Array.isArray(g.cases) ? g.cases : []).filter(function (c) { return c && typeof c === 'object'; })
                .map(function (c) { return { mesh: get(c), conds: Array.isArray(c.conds) ? c.conds : [] }; }) };
        };
        Object.keys(json.forms || {}).forEach(function (name) {
            const f = json.forms[name];
            if (Array.isArray(f)) forms[name] = { list: f.map(get) };
            else if (f && Array.isArray(f.rots)) forms[name] = { rots: [0, 1, 2, 3].map(function (r) { return group(f.rots[r]); }) };
            else if (f && Array.isArray(f.cases)) forms[name] = group(f);
        });
        // Objets : chaque partie tournée d'avance pour les 4 rotations (meshes[r]), partie au mesh invalide ignorée ; glass (rgba)
        // d'alpha < 0.95 = partie de verre (vitre de porte, globe de lampe), dessinée translucide de cette teinte.
        const objects = {};
        Object.keys(json.objects || {}).forEach(function (item) {
            const o = json.objects[item], rot = Array.isArray(o && o.rot) && o.rot.length === 4 ? o.rot : [0, 0, 0, 0];
            const parts = (Array.isArray(o && o.parts) ? o.parts : []).map(function (p) { return { material: (p && p.material) | 0, glass: p && Array.isArray(p.glass) && p.glass.length === 4 && p.glass[3] < 0.95 ? p.glass : null, meshes: rot.map(function (deg) { return get({ mesh: p && p.mesh, rot: deg }); }) }; })
                .filter(function (p) { return p.meshes.every(Boolean); });
            if (parts.length) objects[item] = parts;
        });
        return { forms: forms, objects: objects, skins: skins, materials: materials, formMaterials: json.formMaterials || {}, slotMaterials: json.slotMaterials || {}, categories: json.categories || {},
            skinForms: json.skinForms || {},   // forme d'une cellule sans forme de ce matériau (tuyaux : le bloc est toujours un tuyau)
            glass: Array.isArray(json.glass) ? json.glass : [0, 0, 0, 0.26], tilePx: json.tile > 0 ? json.tile : FORMS_TILE,
            // Cube du set : forme Cube explicite, sinon son cas de repli (cube contextuel, ex. gravier), sinon un cube généré.
            cube: (forms.Cube && forms.Cube.list && forms.Cube.list[0]) || (forms.Cube && forms.Cube.cases && (forms.Cube.cases.filter(function (c) { return c.mesh && !c.conds.length; })[0] || forms.Cube.cases[0] || {}).mesh) || formsCubeMesh() };
    }

    // Prédicat d'une condition de voisinage (cond = [dx, dy, dz, attendu, bitMeans?]) pour la cellule (x, y, z) ; hors grille = faux.
    // 'category:<cat>' : voisin non vide dont la forme a cette catégorie (categories du bundle ; bloc sans forme = 'Cube').
    // 'sameType' : même bloc du jeu = même forme et même matériau que la cellule,
    // et même rotation si la forme a 4 blocs tournés (liste de 4 ou « rots » : FarEastLumberWall_0190 ≠ FarEastLumberWall_01).
    // 'type:<A>,<B>…' (tuyaux) : voisin de matériau <A>Item, ou prise d'un objet (vox.pipeSlots) si la liste contient PipeSlot.
    // Les autres prédicats lisent la couche des blocs : une case traversée par un tuyau y vaut le bloc qu'il traverse (vox.under).
    function formsNeighbor(set, bitMeans, vox, x, y, z, cond) {
        const nx = x + cond[0], ny = y + cond[1], nz = z + cond[2];
        if (nx < 0 || ny < 0 || nz < 0 || nx >= vox.W || ny >= vox.D || nz >= vox.H) return false;
        const i = x + vox.W * (y + vox.D * z), j = nx + vox.W * (ny + vox.D * nz), means = cond[4] || bitMeans;
        if (means.indexOf('type:') === 0) {
            const types = means.slice(5).split(','), name = vox.cells[j] ? vox.palette[vox.cells[j] - 1] : null;
            if (types.indexOf('PipeSlot') >= 0 && vox.pipeSlots && vox.pipeSlots.has(j)) return true;
            return !!name && types.some(function (t) { return t !== 'PipeSlot' && name === t + 'Item'; });
        }
        const ui = vox.under && vox.under.get(i), uj = vox.under && vox.under.get(j);
        const ci = ui ? ui[0] : vox.cells[i], si = ui ? ui[1] : vox.shapes[i];
        const cj = uj ? uj[0] : vox.cells[j], sj = uj ? uj[1] : vox.shapes[j];
        if (!cj) return false;
        if (means === 'solid') return true;
        if (means === 'sameType') {
            // Cube implicite (code 0, crayon) et forme Cube explicite (ex. EmptyEdgeFiller) : même bloc du jeu.
            const cube = FORMS.indexOf('Cube') + 1, ti = (si >> 2) === cube ? 0 : si >> 2, tj = (sj >> 2) === cube ? 0 : sj >> 2;
            if (cj !== ci || tj !== ti) return false;
            const f = set.forms[si ? FORMS[(si >> 2) - 1] : 'Cube'];
            return !(f && (f.rots || (f.list && f.list.length === 4))) || (sj & 3) === (si & 3);
        }
        if (means.indexOf('category:') !== 0) return false;
        let cat = set.categories[sj ? FORMS[(sj >> 2) - 1] : 'Cube'];
        if (Array.isArray(cat)) cat = cat[sj & 3];   // catégorie par rotation du voisin (quais)
        return cat === means.slice(9);
    }

    // Mesh de la cellule : forme orientée → entrée [rot], forme contextuelle → premier cas dont toutes les conditions sont
    // satisfaites ; cube plein (code 0), forme absente du set, aucun cas ou mesh invalide → cube du set. cube = true si le bloc
    // est plein (ses voisins masquent leurs faces contre lui) ; form = nom de la forme (choix du matériau de rendu).
    function formsPick(set, code, vox, x, y, z) {
        // Forme absente du set (mur en gravier…) : le bloc y est un cube, avec les cas du Cube s'il en a (bords, coins).
        const name = code ? FORMS[(code >> 2) - 1] : 'Cube', f = set.forms[name] || set.forms.Cube;
        let mesh = null;
        const g = f && f.rots ? f.rots[code & 3] : f;   // forme orientée et contextuelle : les cas de la rotation de l'op
        if (f && f.list) mesh = f.list[code & 3] || f.list[0];
        else if (g && g.cases) {
            const hit = g.cases.find(function (c) { return c.conds.every(function (k) { return formsNeighbor(set, g.bitMeans, vox, x, y, z, k) === !!k[3]; }); });
            if (hit) mesh = hit.mesh;
        }
        return mesh ? { mesh: mesh, cube: name === 'Cube' || !set.forms[name], form: name } : { mesh: set.cube, cube: true, form: name };
    }

    // Set chargé pour un matériau, ou null (pas de bundle, en cours, ou skin inconnue) : { set, mats: { Forme: { key: 'Set:matériau', mat } }, shape },
    // le matériau de rendu de chaque forme étant la surcharge formMaterials du set, sinon celui de la skin ; mats[Forme].slots = matériaux
    // des slots 1… du mesh (slotMaterials, repli sur celui de la forme) ; shape = code de la forme par défaut du matériau (skinForms), 0 sinon.
    function formsMaterial(forms, name) {
        const idx = forms.index, setName = idx && idx.materials && idx.materials[name], set = setName && forms.sets[setName];
        if (!set || set.skins[name] === undefined) return null;
        const over = set.formMaterials[name] || {}, slotOver = set.slotMaterials[name] || {}, mats = {};
        const entry = function (mi) { return { key: setName + ':' + mi, mat: set.materials[mi] }; };
        FORMS.forEach(function (form) {
            const mi = (over[form] !== undefined ? over[form] : set.skins[name]) | 0;
            mats[form] = entry(mi);
            mats[form].slots = (Array.isArray(slotOver[form]) ? slotOver[form] : []).map(function (s) { return entry(s === null || s === undefined || !set.materials[s] ? mi : s | 0); });
        });
        return { set: set, mats: mats, shape: set.skinForms && set.skinForms[name] ? shapeCode({ form: set.skinForms[name] }) : 0 };
    }

    // Parties d'un objet chargé, ou null : [ { key: 'Set:matériau', mat, glass (rgba ou null), meshes: [mesh × 4 rotations] } ].
    function formsObject(forms, type) {
        const idx = forms.index, setName = idx && idx.objects && idx.objects[type], set = setName && forms.sets[setName];
        if (!set || !set.objects[type]) return null;
        return set.objects[type].map(function (p) { return { key: setName + ':' + p.material, mat: set.materials[p.material], glass: p.glass, meshes: p.meshes }; });
    }

    // Charge l'index puis les sets des matériaux et des objets du plan, une fois chacun ; à l'arrivée d'un set le maillage est
    // reconstruit. Silencieux en cas d'échec ou d'absence (cubes colorés).
    function formsEnsure(st) {
        const f = st.view3d.forms;
        if (typeof fetch !== 'function' || !st.vox) return;
        if (f.index === null) {
            f.index = 'pending';
            fetch(FORMS_URL + 'index.json').then(function (r) { return r.ok ? r.json() : Promise.reject(r.status); })
                .then(function (idx) { f.index = idx && idx.sets && idx.materials ? idx : false; formsEnsure(st); })
                .catch(function () { f.index = false; });
            return;
        }
        if (!f.index || f.index === 'pending') return;
        const load = function (setName) {
            const entry = setName && f.index.sets[setName];
            if (!entry || f.sets[setName] !== undefined) return;
            f.sets[setName] = null;
            const img = new Image();
            const image = new Promise(function (resolve, reject) { img.onload = function () { resolve(img); }; img.onerror = reject; });
            img.src = FORMS_URL + entry.atlas;
            Promise.all([fetch(FORMS_URL + entry.json).then(function (r) { return r.ok ? r.json() : Promise.reject(r.status); }), image])
                .then(function (parts) { const set = formsPrepareSet(parts[0]); set.image = parts[1]; f.sets[setName] = set; st.view3d.dirty = true; requestRender(st); })
                .catch(function () { f.sets[setName] = false; });
        };
        st.vox.palette.forEach(function (name) { load(f.index.materials[name]); });
        if (f.index.objects) st.plan.levels.forEach(function (level) { level.objects.forEach(function (o) { load(f.index.objects[o.type]); }); });
    }

    // Texture d'une tuile de l'atlas (clé 'Set:tuile', carrée de côté set.tilePx, REPEAT + mipmaps) découpée à la première demande ;
    // unités 1 à 3 au dessin (côté, dessus, détail du matériau du tampon).
    function formsTexture(gl, v, key) {
        if (v.matTex[key] !== undefined) return v.matTex[key];
        const p = key.split(':'), set = v.forms.sets[p[0]], tile = +p[1], px = set.tilePx;
        const c = v.forms.scratch || (v.forms.scratch = document.createElement('canvas'));
        c.width = c.height = px;
        const per = Math.max(1, Math.round(set.image.width / px));
        c.getContext('2d').drawImage(set.image, (tile % per) * px, Math.floor(tile / per) * px, px, px, 0, 0, px, px);
        const tex = gl.createTexture();
        gl.bindTexture(gl.TEXTURE_2D, tex);
        gl.texImage2D(gl.TEXTURE_2D, 0, gl.RGBA, gl.RGBA, gl.UNSIGNED_BYTE, c);
        gl.generateMipmap(gl.TEXTURE_2D);
        gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MIN_FILTER, gl.LINEAR_MIPMAP_LINEAR); gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MAG_FILTER, gl.LINEAR);
        gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_S, gl.REPEAT); gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_T, gl.REPEAT);
        v.matTex[key] = tex;
        return tex;
    }

    // Ajoute le mesh à la cellule (x, y, z) en sommets à normale, uv selon chan : côté (−2, −2), dessus (−3, −3), détail (u, 1 − v)
    // (uv Unity, origine en bas) ; hidden[f] = face f du cube masquée ; m.shade (0-255 par sommet, meubles) assombrit la couleur.
    // Renvoie le nombre de triangles émis.
    function v3dMesh(b, m, x, y, z, c, layer, hidden) {
        v3dGrow(b, m.idx.length);
        let tris = 0;
        for (let t = 0; t < m.idx.length; t += 3) {
            const f = m.faces[t / 3];
            if (f >= 0 && hidden[f]) continue;
            for (let k = 0; k < 3; k++) {
                const i = m.idx[t + k], j = i * 3, ch = m.chan ? m.chan[i] : 0;
                const u = ch === 2 ? m.uv[i * 2] : ch === 1 ? -3 : -2, w = ch === 2 ? 1 - m.uv[i * 2 + 1] : u, s = m.shade ? m.shade[i] / 255 : 1;
                v3dVertex(b, m.pos[j] + x + 0.5, m.pos[j + 1] + y + 0.5, m.pos[j + 2] + z + 0.5, Math.round(c[0] * s), Math.round(c[1] * s), Math.round(c[2] * s), layer, u, w, m.nrm[j], m.nrm[j + 1], m.nrm[j + 2]);
            }
            tris++;
        }
        return tris;
    }

    // Ajoute les triangles de verre du mesh (m.glass) à la cellule : couleur unie = teinte du set ombrée par face comme les cubes,
    // jamais comptés dans les faces ; une vitre sur une face du cube est masquée contre un voisin plein (deux cubes de verre
    // accolés : pas de vitre entre eux, comme le jeu) ; l'alpha de la teinte est posé sur le tampon (uAlpha au dessin).
    function v3dGlass(b, m, x, y, z, tint, layer, hidden) {
        v3dGrow(b, m.glass.length);
        b.alpha = tint[3];
        for (let t = 0; t < m.glass.length; t += 3) {
            const f = m.glassFaces ? m.glassFaces[t / 3] : -1;
            if (f >= 0 && hidden[f]) continue;
            const n = m.glass[t] * 3, nx = m.nrm[n], ny = m.nrm[n + 1], nz = m.nrm[n + 2];
            const s = Math.abs(nz) >= Math.abs(nx) && Math.abs(nz) >= Math.abs(ny) ? (nz > 0 ? V3D_SHADE.pz : V3D_SHADE.nz) : Math.abs(nx) >= Math.abs(ny) ? (nx > 0 ? V3D_SHADE.px : V3D_SHADE.nx) : (ny > 0 ? V3D_SHADE.py : V3D_SHADE.ny);
            for (let k = 0; k < 3; k++) {
                const j = m.glass[t + k] * 3;
                v3dVertex(b, m.pos[j] + x + 0.5, m.pos[j + 1] + y + 0.5, m.pos[j + 2] + z + 0.5, Math.round(tint[0] * 255 * s), Math.round(tint[1] * 255 * s), Math.round(tint[2] * 255 * s), layer, -1, -1, m.nrm[j], m.nrm[j + 1], m.nrm[j + 2]);
            }
        }
    }

    // Cellules de mobilier (tous niveaux, objets posés compris) : Map index → objet { id, type, x, y, z (case d'ancrage), rot } ;
    // out.slots = index des prises (cellules Solid = PipeSlot) où se raccordent les tuyaux ; out.walls = index des cellules Wall
    // (portes : elles ferment les poches d'air, comme ObjectWall côté C#).
    function v3dObjectCells(st) {
        const W = st.plan.grid.width, D = st.plan.grid.depth, H = st.vox.H, out = new Map();
        out.slots = new Set(); out.walls = new Set();
        st.plan.levels.forEach(function (level, k) {
            const base = levelBaseY(st, k) + 1;
            level.objects.forEach(function (o) {
                let z0 = o.z != null ? base - 1 + o.z : base;   // Z = hauteur de l'origine relative à la dalle du niveau (ObjectPlacer)
                if (o.attachedTo) {   // sur son support, qui peut avoir un Z (ObjectPlacer.Attach)
                    const parent = level.objects.find(function (p) { return p.id === o.attachedTo; });
                    const pi = parent && st.objectsByName[parent.type];
                    z0 = (parent && parent.z != null ? base - 1 + parent.z : base) + (pi && pi.size ? pi.size[2] : 1);
                }
                const rec = { id: o.id, type: o.type, x: o.x, y: o.y, z: z0, rot: (o.rotation || 0) & 3 };
                objectCells(st, o).forEach(function (c) {
                    const z = z0 + c.dz;
                    if (c.x < 0 || c.y < 0 || z < 0 || c.x >= W || c.y >= D || z >= H) return;
                    out.set(c.x + W * (c.y + D * z), rec);
                    if (c.kind === KIND.SOLID) out.slots.add(c.x + W * (c.y + D * z));
                    else if (c.kind === KIND.WALL) out.walls.add(c.x + W * (c.y + D * z));
                });
            });
        });
        return out;
    }

    // Maillage : faces visibles des blocs et des meubles (+ grille au sol, nappe de la couche) ; les blocs d'un matériau à bundle
    // chargé vont dans un tampon par matériau de rendu (data.forms[clé], .mat = ses textures) avec le mesh de leur forme, leur verre dans data.glass[clé].
    function view3dBuild(st) {
        const v = st.view3d, vox = st.vox, W = vox.W, D = vox.D, H = vox.H;
        const cap = v.cap ? st.layer : H - 1;
        const objs = v3dObjectCells(st);
        const sel = st.selection && st.selection.kind === 'op' ? st.plan.architecture.ops.findIndex(function (o) { return o.id === st.selection.id; }) + 1 : 0;
        const selColor = parseColor(st.palette.secondary, [255, 183, 77]), objColor = [207, 212, 218], white = [255, 255, 255];
        const matColor = vox.palette.map(function (name) { return materialRgb(st, name); });
        const matForms = vox.palette.map(function (name) { return formsMaterial(v.forms, name); });   // set chargé par matériau (null : cubes de couleur)
        const idx = function (x, y, z) { return x + W * (y + D * z); };
        // Formes résolues d'avance : une forme non pleine (mur mince, escalier…) est dessinée entière et laisse voir derrière elle,
        // ses voisins ne masquent donc pas leurs faces contre elle (thin). Une cellule sans forme prend celle de son matériau (tuyaux).
        const picks = new Map(), thin = new Uint8Array(W * D * H);
        vox.pipeSlots = objs.slots;
        for (let i = 0; i < vox.shapes.length; i++) {
            const fm = vox.cells[i] ? matForms[vox.cells[i] - 1] : null, code = fm ? vox.shapes[i] || fm.shape : 0;
            if (!code) continue;
            const p = formsPick(fm.set, code, vox, i % W, Math.floor(i / W) % D, Math.floor(i / (W * D)));
            picks.set(i, p); if (!p.cube) thin[i] = 1;
        }
        // Bloc traversé par un tuyau : dessiné dans la même case ; s'il est plein (dalle), la case reste pleine pour ses voisins.
        const underPicks = new Map();
        (vox.under || new Map()).forEach(function (u, i) {
            const fm = matForms[u[0] - 1];
            if (!fm) return;
            const p = formsPick(fm.set, u[1] || fm.shape, vox, i % W, Math.floor(i / W) % D, Math.floor(i / (W * D)));
            underPicks.set(i, { fm: fm, pick: p, owner: u[2] });
            if (p.cube) thin[i] = 0;
        });
        // Objets à bundle chargé : dessinés une fois par leur mesh à la case d'ancrage, leurs cases ne sont ni pleines ni cubes.
        const objParts = {}, anchors = new Map();
        objs.forEach(function (o) { if (!(o.type in objParts)) objParts[o.type] = formsObject(v.forms, o.type); if (objParts[o.type]) anchors.set(o.id, o); });
        const modeled = function (o) { return !!(o && objParts[o.type]); };
        const solid = function (x, y, z) {
            if (x < 0 || y < 0 || z < 0 || x >= W || y >= D || z > cap) return false;
            const i = idx(x, y, z); return (vox.cells[i] !== 0 && !thin[i]) || (objs.has(i) && !modeled(objs.get(i)));
        };
        const mesh = v3dBuffer(4096), formBufs = {}, glassBufs = {};
        let faces = 0, bx0 = W, by0 = D, bz0 = H, bx1 = -1, by1 = -1, bz1 = -1;
        v.tooMany = false; v.atlas.pending = {};
        const shade = function (c, f) { return [Math.round(c[0] * f), Math.round(c[1] * f), Math.round(c[2] * f)]; };
        const selObj = st.selection && st.selection.kind === 'object' ? st.selection.id : null;
        // Mesh d'une forme dans le tampon de son matériau de rendu, ses slots FBX 1… dans le leur, son verre à part ; renvoie les faces.
        const drawPick = function (fm, pick, x, y, z, tint, hidden) {
            const rm = fm.mats[pick.form];
            if (!formBufs[rm.key]) { formBufs[rm.key] = v3dBuffer(4096); formBufs[rm.key].mat = rm.mat; }   // textures, échelle et décalage (au dessin)
            let n = v3dMesh(formBufs[rm.key], pick.mesh, x, y, z, tint, z, hidden);
            (pick.mesh.subs || []).forEach(function (sub, k) {   // slots FBX 1… : leur propre matériau (plâtre des murs FarEast…)
                const sm = rm.slots[k] || rm;
                if (!formBufs[sm.key]) { formBufs[sm.key] = v3dBuffer(4096); formBufs[sm.key].mat = sm.mat; }
                n += v3dMesh(formBufs[sm.key], sub, x, y, z, tint, z, hidden);
            });
            if (pick.mesh.glass) v3dGlass(glassBufs[rm.key] || (glassBufs[rm.key] = v3dBuffer(256)), pick.mesh, x, y, z, fm.set.glass, z, hidden);
            return n;
        };
        anchors.forEach(function (o) {
            if (o.x < 0 || o.y < 0 || o.x >= W || o.y >= D || o.z > cap || v.tooMany) return;
            if (o.x < bx0) bx0 = o.x; if (o.x > bx1) bx1 = o.x; if (o.y < by0) by0 = o.y; if (o.y > by1) by1 = o.y; if (o.z < bz0) bz0 = o.z; if (o.z > bz1) bz1 = o.z;
            objParts[o.type].forEach(function (p) {
                if (p.glass) {   // verre de l'objet : tampon par teinte, tout le mesh en vitre
                    const m = p.meshes[o.rot], gk = 'glass:' + p.glass.join(',');
                    v3dGlass(glassBufs[gk] || (glassBufs[gk] = v3dBuffer(256)), { glass: m.idx, glassFaces: null, pos: m.pos, nrm: m.nrm }, o.x, o.y, o.z, p.glass, o.z, null);
                    return;
                }
                if (!formBufs[p.key]) { formBufs[p.key] = v3dBuffer(4096); formBufs[p.key].mat = p.mat; }
                faces += v3dMesh(formBufs[p.key], p.meshes[o.rot], o.x, o.y, o.z, o.id === selObj ? selColor : white, o.z, [false, false, false, false, false, false]);
            });
            if (faces > V3D_MAX_FACES) v.tooMany = true;
        });
        for (let z = 0; z <= cap && !v.tooMany; z++) for (let y = 0; y < D; y++) for (let x = 0; x < W; x++) {
            const i = idx(x, y, z), mat = vox.cells[i], obj = modeled(objs.get(i)) ? null : objs.get(i);
            if (!mat && !obj) continue;
            if (x < bx0) bx0 = x; if (x > bx1) bx1 = x; if (y < by0) by0 = y; if (y > by1) by1 = y; if (z < bz0) bz0 = z; if (z > bz1) bz1 = z;
            const fm = obj ? null : matForms[mat - 1];
            if (fm) {
                const pick = picks.get(i) || formsPick(fm.set, 0, vox, x, y, z);
                const hidden = [solid(x + 1, y, z), solid(x - 1, y, z), solid(x, y + 1, z), solid(x, y - 1, z), solid(x, y, z + 1), solid(x, y, z - 1)];
                const up = underPicks.get(i);
                if (up) faces += drawPick(up.fm, up.pick, x, y, z, sel && up.owner === sel ? selColor : white, hidden);
                faces += drawPick(fm, pick, x, y, z, sel && vox.owner[i] === sel ? selColor : white, hidden);
                if (faces > V3D_MAX_FACES) { v.tooMany = true; break; }
                continue;
            }
            let base = obj ? (obj.id === selObj ? selColor : objColor) : matColor[mat - 1], uv = null;
            if (obj) { const slot = v3dAtlasSlot(st, obj.type); if (slot >= 0) uv = v3dSlotUv(slot); }
            else if (sel && vox.owner[i] === sel) base = selColor;
            // Sommets du cube [x, x+1] × [y, y+1] × [z, z+1] en coordonnées plan ; GL inverse y (matrice modèle).
            if (!solid(x, y, z + 1)) { faces++; v3dQuad(mesh, [x, y, z + 1], [x + 1, y, z + 1], [x + 1, y + 1, z + 1], [x, y + 1, z + 1], shade(base, V3D_SHADE.pz), z, uv); }
            if (!solid(x, y, z - 1)) { faces++; v3dQuad(mesh, [x, y + 1, z], [x + 1, y + 1, z], [x + 1, y, z], [x, y, z], shade(base, V3D_SHADE.nz), z, uv); }
            if (!solid(x + 1, y, z)) { faces++; v3dQuad(mesh, [x + 1, y, z + 1], [x + 1, y + 1, z + 1], [x + 1, y + 1, z], [x + 1, y, z], shade(base, V3D_SHADE.px), z, uv); }
            if (!solid(x - 1, y, z)) { faces++; v3dQuad(mesh, [x, y + 1, z + 1], [x, y, z + 1], [x, y, z], [x, y + 1, z], shade(base, V3D_SHADE.nx), z, uv); }
            if (!solid(x, y + 1, z)) { faces++; v3dQuad(mesh, [x + 1, y + 1, z + 1], [x, y + 1, z + 1], [x, y + 1, z], [x + 1, y + 1, z], shade(base, V3D_SHADE.py), z, uv); }
            if (!solid(x, y - 1, z)) { faces++; v3dQuad(mesh, [x, y, z + 1], [x + 1, y, z + 1], [x + 1, y, z], [x, y, z], shade(base, V3D_SHADE.ny), z, uv); }
            if (faces > V3D_MAX_FACES) { v.tooMany = true; break; }
        }
        v.faces = v.tooMany ? 0 : faces;
        v.bbox = bx1 < 0 ? { x0: 0, y0: 0, z0: 0, x1: W - 1, y1: D - 1, z1: 0 } : { x0: bx0, y0: by0, z0: bz0, x1: bx1, y1: by1, z1: bz1 };
        // Grille au sol (lignes) et nappe translucide du terrain.
        const lines = v3dBuffer(4 * (W + D + 2));
        const faint = parseColor(st.palette.text, [255, 255, 255]);
        for (let x = 0; x <= W; x++) { const s = x % 5 === 0 ? 0.45 : 0.18; v3dGrow(lines, 2); v3dVertex(lines, x, 0, 0, faint[0] * s, faint[1] * s, faint[2] * s, 255, -1, -1); v3dVertex(lines, x, D, 0, faint[0] * s, faint[1] * s, faint[2] * s, 255, -1, -1); }
        for (let y = 0; y <= D; y++) { const s = y % 5 === 0 ? 0.45 : 0.18; v3dGrow(lines, 2); v3dVertex(lines, 0, y, 0, faint[0] * s, faint[1] * s, faint[2] * s, 255, -1, -1); v3dVertex(lines, W, y, 0, faint[0] * s, faint[1] * s, faint[2] * s, 255, -1, -1); }
        const sheets = v3dBuffer(6);
        v3dQuad(sheets, [0, 0, 0.005], [W, 0, 0.005], [W, D, 0.005], [0, D, 0.005], [255, 255, 255], 255, null);
        // Arêtes vides des pièces (analyse C#, tier 0 dans le jeu) : cubes rouges translucides, vus à travers les blocs.
        const edges = v3dBuffer(256), red = parseColor(st.palette.error, [244, 67, 54]);
        ((st.analysis && st.analysis.rooms) || []).forEach(function (room) {
            const e = room.emptyEdges || [];
            for (let i = 0; i + 2 < e.length; i += 3) {
                const x = e[i], y = e[i + 1], z = e[i + 2];
                if (z > cap) continue;
                v3dQuad(edges, [x, y, z + 1], [x + 1, y, z + 1], [x + 1, y + 1, z + 1], [x, y + 1, z + 1], shade(red, V3D_SHADE.pz), 255, null);
                v3dQuad(edges, [x, y + 1, z], [x + 1, y + 1, z], [x + 1, y, z], [x, y, z], shade(red, V3D_SHADE.nz), 255, null);
                v3dQuad(edges, [x + 1, y, z + 1], [x + 1, y + 1, z + 1], [x + 1, y + 1, z], [x + 1, y, z], shade(red, V3D_SHADE.px), 255, null);
                v3dQuad(edges, [x, y + 1, z + 1], [x, y, z + 1], [x, y, z], [x, y + 1, z], shade(red, V3D_SHADE.nx), 255, null);
                v3dQuad(edges, [x + 1, y + 1, z + 1], [x, y + 1, z + 1], [x, y + 1, z], [x + 1, y + 1, z], shade(red, V3D_SHADE.py), 255, null);
                v3dQuad(edges, [x, y, z + 1], [x + 1, y, z + 1], [x + 1, y, z], [x, y, z], shade(red, V3D_SHADE.ny), 255, null);
            }
        });
        v.data = { mesh: mesh, lines: lines, sheets: sheets, edges: edges, forms: formBufs, glass: glassBufs };
        v.dirty = false; v.upload = true;
    }

    function view3dEnsureGl(st) {
        const v = st.view3d;
        if (v.gl && !v.gl.isContextLost()) return v.gl;
        const gl = v.canvas.getContext('webgl', { antialias: true, alpha: false, preserveDrawingBuffer: true });
        if (!gl) return null;
        const compile = function (type, src) { const s = gl.createShader(type); gl.shaderSource(s, src); gl.compileShader(s); return s; };
        const prog = gl.createProgram();
        gl.attachShader(prog, compile(gl.VERTEX_SHADER, V3D_VS)); gl.attachShader(prog, compile(gl.FRAGMENT_SHADER, V3D_FS));
        gl.linkProgram(prog);
        if (!gl.getProgramParameter(prog, gl.LINK_STATUS)) { console.error('building-planner 3D: ' + gl.getProgramInfoLog(prog)); return null; }
        v.gl = gl; v.prog = prog;
        v.loc = { aPos: gl.getAttribLocation(prog, 'aPos'), aColor: gl.getAttribLocation(prog, 'aColor'), aUv: gl.getAttribLocation(prog, 'aUv'), aNormal: gl.getAttribLocation(prog, 'aNormal'),
            uMvp: gl.getUniformLocation(prog, 'uMvp'), uTex: gl.getUniformLocation(prog, 'uTex'), uMat: gl.getUniformLocation(prog, 'uMat'), uTop: gl.getUniformLocation(prog, 'uTop'),
            uDetail: gl.getUniformLocation(prog, 'uDetail'),
            uAlpha: gl.getUniformLocation(prog, 'uAlpha'), uCut: gl.getUniformLocation(prog, 'uCut'),
            uTexScale: gl.getUniformLocation(prog, 'uTexScale'), uTexOffset: gl.getUniformLocation(prog, 'uTexOffset') };
        v.bufs = {};
        ['mesh', 'lines', 'sheets', 'edges'].forEach(function (k) { v.bufs[k] = v3dGlBuffers(gl); });
        v.formBufs = {}; v.glassBufs = {}; v.matTex = {};   // tampons (formes, verre) par clé 'Set:matériau', textures par 'Set:tuile' (recréés avec le contexte)
        v.tex = gl.createTexture();
        gl.bindTexture(gl.TEXTURE_2D, v.tex);
        gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MIN_FILTER, gl.LINEAR); gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MAG_FILTER, gl.LINEAR);
        gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_S, gl.CLAMP_TO_EDGE); gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_T, gl.CLAMP_TO_EDGE);
        v.atlas.dirty = true; v.upload = true;
        return gl;
    }

    function v3dGlBuffers(gl) { return { pos: gl.createBuffer(), col: gl.createBuffer(), uv: gl.createBuffer(), nrm: gl.createBuffer(), n: 0 }; }
    function v3dUpload(gl, buf, data) {
        gl.bindBuffer(gl.ARRAY_BUFFER, buf.pos); gl.bufferData(gl.ARRAY_BUFFER, data.pos.subarray(0, data.n * 3), gl.STATIC_DRAW);
        gl.bindBuffer(gl.ARRAY_BUFFER, buf.col); gl.bufferData(gl.ARRAY_BUFFER, data.col.subarray(0, data.n * 4), gl.STATIC_DRAW);
        gl.bindBuffer(gl.ARRAY_BUFFER, buf.uv); gl.bufferData(gl.ARRAY_BUFFER, data.uv.subarray(0, data.n * 2), gl.STATIC_DRAW);
        gl.bindBuffer(gl.ARRAY_BUFFER, buf.nrm); gl.bufferData(gl.ARRAY_BUFFER, data.nrm.subarray(0, data.n * 3), gl.STATIC_DRAW);
        buf.n = data.n;
    }
    function v3dBind(gl, v, buf) {
        gl.bindBuffer(gl.ARRAY_BUFFER, buf.pos); gl.vertexAttribPointer(v.loc.aPos, 3, gl.FLOAT, false, 0, 0);
        gl.bindBuffer(gl.ARRAY_BUFFER, buf.col); gl.vertexAttribPointer(v.loc.aColor, 4, gl.UNSIGNED_BYTE, true, 0, 0);
        gl.bindBuffer(gl.ARRAY_BUFFER, buf.uv); gl.vertexAttribPointer(v.loc.aUv, 2, gl.FLOAT, false, 0, 0);
        gl.bindBuffer(gl.ARRAY_BUFFER, buf.nrm); gl.vertexAttribPointer(v.loc.aNormal, 3, gl.FLOAT, false, 0, 0);
    }
    function v3dDraw(gl, v, buf, mode, alpha) {
        if (!buf.n) return;
        v3dBind(gl, v, buf);
        gl.uniform1f(v.loc.uAlpha, alpha);
        gl.drawArrays(mode, 0, buf.n);
    }

    // Matrices colonne-major 4×4.
    function m4Perspective(fovy, aspect, near, far) {
        const f = 1 / Math.tan(fovy / 2), nf = 1 / (near - far);
        return [f / aspect, 0, 0, 0, 0, f, 0, 0, 0, 0, (far + near) * nf, -1, 0, 0, 2 * far * near * nf, 0];
    }
    function m4LookAt(eye, target, up) {
        const sub = function (a, b) { return [a[0] - b[0], a[1] - b[1], a[2] - b[2]]; };
        const cross = function (a, b) { return [a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0]]; };
        const norm = function (a) { const l = Math.hypot(a[0], a[1], a[2]) || 1; return [a[0] / l, a[1] / l, a[2] / l]; };
        const dot = function (a, b) { return a[0] * b[0] + a[1] * b[1] + a[2] * b[2]; };
        const z = norm(sub(eye, target)), x = norm(cross(up, z)), y = cross(z, x);
        return [x[0], y[0], z[0], 0, x[1], y[1], z[1], 0, x[2], y[2], z[2], 0, -dot(x, eye), -dot(y, eye), -dot(z, eye), 1];
    }
    function m4Mul(a, b) {
        const out = new Array(16);
        for (let c = 0; c < 4; c++) for (let r = 0; r < 4; r++) out[c * 4 + r] = a[r] * b[c * 4] + a[4 + r] * b[c * 4 + 1] + a[8 + r] * b[c * 4 + 2] + a[12 + r] * b[c * 4 + 3];
        return out;
    }
    // Œil en repère GL (y inversé) : lacet 0 = depuis le sud du plan (y plan croissant), tangage vers le haut.
    function v3dEye(v) {
        const t = v.target, cp = Math.cos(v.pitch);
        return [t[0] + v.dist * cp * Math.sin(v.yaw), -t[1] - v.dist * cp * Math.cos(v.yaw), t[2] + v.dist * Math.sin(v.pitch)];
    }
    function v3dMvp(v, aspect) {
        const eye = v3dEye(v), view = m4LookAt(eye, [v.target[0], -v.target[1], v.target[2]], [0, 0, 1]);
        const proj = m4Perspective(Math.PI / 4, aspect, Math.max(0.1, v.dist * 0.02), v.dist * 4 + 500);
        const flipY = [1, 0, 0, 0, 0, -1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1];
        return m4Mul(m4Mul(proj, view), flipY);
    }

    // Repère de la caméra en coordonnées du plan (y non inversé) : œil, visée, droite et haut de l'écran.
    function v3dBasis(v) {
        const cy = Math.cos(v.yaw), sy = Math.sin(v.yaw), cp = Math.cos(v.pitch), sp = Math.sin(v.pitch), t = v.target;
        return { eye: [t[0] + v.dist * cp * sy, t[1] + v.dist * cp * cy, t[2] + v.dist * sp], fwd: [-cp * sy, -cp * cy, -sp], right: [cy, -sy, 0], up: [-sp * sy, -sp * cy, cp] };
    }
    // Rayon (coordonnées du plan, direction unitaire) du point écran (nx, ny) en NDC (−1..1, y vers le haut).
    function v3dRay(v, aspect, nx, ny) {
        const b = v3dBasis(v), k = Math.tan(Math.PI / 8), d = [0, 1, 2].map(function (a) { return b.fwd[a] + b.right[a] * nx * k * aspect + b.up[a] * ny * k; });
        const l = Math.hypot(d[0], d[1], d[2]);
        return { origin: b.eye, dir: [d[0] / l, d[1] / l, d[2] / l] };
    }
    // Point du plan → NDC [nx, ny] (même matrice que le rendu).
    function v3dProject(v, aspect, p) {
        const m = v3dMvp(v, aspect), w = m[3] * p[0] + m[7] * p[1] + m[11] * p[2] + m[15];
        return [(m[0] * p[0] + m[4] * p[1] + m[8] * p[2] + m[12]) / w, (m[1] * p[0] + m[5] * p[1] + m[9] * p[2] + m[13]) / w];
    }
    // Zoom de facteur f (distance × f) vers P : homothétie de centre P, P reste au même pixel.
    function v3dZoomAt(v, p, f) {
        v.target = [0, 1, 2].map(function (a) { return p[a] + (v.target[a] - p[a]) * f; });
        v.dist *= f;
    }
    // Orbite autour de P : la caméra entière (œil, cible) tourne autour de P, P reste au même pixel et à la même distance.
    // Lacet + Δ = rotation de −Δ autour de la verticale passant par P ; tangage + δ (borné) = rotation autour de l'axe droite
    // de la caméra passant par P, dans le plan vertical (h = direction horizontale vers l'œil, z). La distance ne change pas.
    function v3dOrbit(v, p, dYaw, dPitch) {
        const pitch = Math.max(0.09, Math.min(1.55, v.pitch + dPitch)), dp = pitch - v.pitch;
        const c = Math.cos(-dYaw), s = Math.sin(-dYaw), d0 = [v.target[0] - p[0], v.target[1] - p[1], v.target[2] - p[2]];
        const d = [d0[0] * c - d0[1] * s, d0[0] * s + d0[1] * c, d0[2]];
        v.yaw += dYaw;
        const h = [Math.sin(v.yaw), Math.cos(v.yaw)], a = d[0] * h[0] + d[1] * h[1], cp = Math.cos(dp), sp = Math.sin(dp);
        const a2 = a * cp - d[2] * sp;
        v.target = [p[0] + d[0] + (a2 - a) * h[0], p[1] + d[1] + (a2 - a) * h[1], p[2] + a * sp + d[2] * cp];
        v.pitch = pitch;
    }
    // Coupe face caméra : plan vertical par v.cutAt, normale = direction horizontale vers l'œil (lacet). [nx, ny, nz, w] en
    // coordonnées du plan ; un point q est masqué si n·q + w > 0 (côté caméra). Coupe éteinte : [0, 0, 0, −1] (rien de masqué).
    // cutAt est un point fixe du plan, posé au cadrage (centre des blocs) et au double-clic : l'orbite autour d'un point visé
    // déplace la cible, la coupe ne doit pas la suivre hors du bâtiment.
    function v3dCutPlane(v) {
        if (!v.cutaway) return [0, 0, 0, -1];
        const nx = Math.sin(v.yaw), ny = Math.cos(v.yaw);
        return [nx, ny, 0, -(nx * v.cutAt[0] + ny * v.cutAt[1])];
    }
    function v3dCutHidden(v, x, y, z) {
        const c = v3dCutPlane(v);
        return c[0] * x + c[1] * y + c[2] * z + c[3] > 0.001;
    }

    // Lancer de rayon dans la grille (Amanatides-Woo), coordonnées du plan (case [x, x+1[ × [y, y+1[ × [z, z+1[) : première case
    // pleine ou occupée par un objet, sinon null. opts.cap = plus haute couche prise (masquage au-dessus) ; opts.clip(x, y, z) = true
    // si la case est masquée (traversée) ; opts.objects = Map index → objet (v3dObjectCells). Résultat : { x, y, z, t, normal (face
    // d'entrée, nulle si l'origine est dans la case), object (ou null), owner (vox.owner de la case, 0 pour un objet) }.
    function voxRaycast(vox, origin, dir, opts) {
        opts = opts || {};
        const W = vox.W, D = vox.D, hi = [W, D, Math.min(vox.H, opts.cap == null ? vox.H : opts.cap + 1)];
        if (hi[2] <= 0) return null;
        let t0 = 0, t1 = Infinity, entry = -1;
        for (let a = 0; a < 3; a++) {
            if (!dir[a]) { if (origin[a] < 0 || origin[a] > hi[a]) return null; continue; }
            let ta = -origin[a] / dir[a], tb = (hi[a] - origin[a]) / dir[a];
            if (ta > tb) { const s = ta; ta = tb; tb = s; }
            if (ta > t0) { t0 = ta; entry = a; }
            if (tb < t1) t1 = tb;
        }
        if (t0 > t1) return null;
        const cell = [0, 0, 0], step = [0, 0, 0], tMax = [Infinity, Infinity, Infinity], tDelta = [Infinity, Infinity, Infinity];
        for (let a = 0; a < 3; a++) {
            cell[a] = Math.min(hi[a] - 1, Math.max(0, Math.floor(origin[a] + dir[a] * t0)));
            if (!dir[a]) continue;
            step[a] = dir[a] > 0 ? 1 : -1;
            tMax[a] = (cell[a] + (step[a] > 0 ? 1 : 0) - origin[a]) / dir[a];
            tDelta[a] = 1 / Math.abs(dir[a]);
        }
        let t = t0, normal = [0, 0, 0];
        if (entry >= 0) normal[entry] = -step[entry];
        for (;;) {
            const x = cell[0], y = cell[1], z = cell[2], i = x + W * (y + D * z);
            if (!opts.clip || !opts.clip(x, y, z)) {
                const o = opts.objects ? opts.objects.get(i) || null : null;
                if (o || vox.cells[i]) return { x: x, y: y, z: z, t: t, normal: normal, object: o, owner: o ? 0 : vox.owner[i] };
            }
            const a = tMax[0] < tMax[1] ? (tMax[0] < tMax[2] ? 0 : 2) : (tMax[1] < tMax[2] ? 1 : 2);
            if (tMax[a] > t1) return null;
            t = tMax[a]; cell[a] += step[a];
            if (cell[a] < 0 || cell[a] >= hi[a]) return null;
            tMax[a] += tDelta[a];
            normal = [0, 0, 0]; normal[a] = -step[a];
        }
    }

    // Ce que vise le pixel (px, py) de la vue 3D : { hit (voxRaycast ou null), p (point touché, sinon le sol z = 0 à moins de
    // 2000 de l'œil) }, null si rien.
    function view3dPick(st, px, py) {
        const v = st.view3d, vr = viewRect(st);
        if (!st.vox || !inRect(vr, px, py)) return null;
        const ray = v3dRay(v, vr.w / Math.max(1, vr.h), (px - vr.x) / vr.w * 2 - 1, 1 - (py - vr.y) / vr.h * 2);
        // Coupe face caméra : une case dont le centre est masqué est traversée (on ne vise pas la moitié cachée).
        const clip = v.cutaway ? function (x, y, z) { return v3dCutHidden(v, x + 0.5, y + 0.5, z + 0.5); } : null;
        const hit = voxRaycast(st.vox, ray.origin, ray.dir, { cap: v.cap ? st.layer : st.vox.H - 1, clip: clip, objects: v3dObjectCells(st) });
        let t = hit ? hit.t : ray.dir[2] < 0 ? -ray.origin[2] / ray.dir[2] : -1;
        if (!hit && !(t > 0 && t <= 2000)) return null;
        return { hit: hit, p: [0, 1, 2].map(function (a) { return ray.origin[a] + ray.dir[a] * t; }) };
    }

    // Cadre la boîte des blocs (cible au centre, distance pour la voir entière à 45°).
    function view3dFit(st) {
        const v = st.view3d;
        if (!v.bbox || v.dirty) view3dBuild(st);
        const b = v.bbox;
        v.target = [(b.x0 + b.x1 + 1) / 2, (b.y0 + b.y1 + 1) / 2, (b.z0 + b.z1 + 1) / 2];
        const radius = Math.max(2, Math.hypot(b.x1 - b.x0 + 1, b.y1 - b.y0 + 1, b.z1 - b.z0 + 1) / 2);
        v.dist = radius / Math.sin(Math.PI / 8) * 1.1;
        v.cutAt = v.target.slice();
        requestRender(st);
    }

    // Place le canvas 3D sur la zone de vue (CSS) et le dimensionne au dpr.
    function view3dLayout(st) {
        const v = st.view3d, vr = viewRect(st), dpr = st.dpr || 1, c = v.canvas;
        c.style.left = vr.x + 'px'; c.style.top = vr.y + 'px'; c.style.width = vr.w + 'px'; c.style.height = vr.h + 'px';
        const pw = Math.max(1, Math.round(vr.w * dpr)), ph = Math.max(1, Math.round(vr.h * dpr));
        if (c.width !== pw || c.height !== ph) { c.width = pw; c.height = ph; }
        return vr;
    }

    function view3dRender(st) {
        const v = st.view3d, vr = view3dLayout(st);
        const gl = view3dEnsureGl(st);
        if (!gl || !st.vox) return;
        for (const name in v.atlas.pending) if (st.icons[name]) { v.dirty = true; break; }   // une icône vient d'arriver
        if (v.dirty) view3dBuild(st);
        if (v.upload) {
            v3dUpload(gl, v.bufs.mesh, v.data.mesh); v3dUpload(gl, v.bufs.lines, v.data.lines); v3dUpload(gl, v.bufs.sheets, v.data.sheets); v3dUpload(gl, v.bufs.edges, v.data.edges);
            for (const key in v.data.forms) v3dUpload(gl, v.formBufs[key] || (v.formBufs[key] = v3dGlBuffers(gl)), v.data.forms[key]);
            for (const key in v.data.glass) v3dUpload(gl, v.glassBufs[key] || (v.glassBufs[key] = v3dGlBuffers(gl)), v.data.glass[key]);
            v.upload = false;
        }
        if (v.atlas.dirty) { gl.bindTexture(gl.TEXTURE_2D, v.tex); gl.texImage2D(gl.TEXTURE_2D, 0, gl.RGBA, gl.RGBA, gl.UNSIGNED_BYTE, v.atlas.canvas); v.atlas.dirty = false; }
        const bg = parseColor(st.palette.bg, [30, 36, 41]);
        const clean = v.clean;   // export « propre » : ni grille, ni nappe
        gl.viewport(0, 0, v.canvas.width, v.canvas.height);
        gl.clearColor(bg[0] / 255, bg[1] / 255, bg[2] / 255, 1);
        gl.enable(gl.DEPTH_TEST); gl.depthMask(true); gl.disable(gl.BLEND); gl.disable(gl.CULL_FACE);
        gl.clear(gl.COLOR_BUFFER_BIT | gl.DEPTH_BUFFER_BIT);
        gl.useProgram(v.prog);
        gl.enableVertexAttribArray(v.loc.aPos); gl.enableVertexAttribArray(v.loc.aColor); gl.enableVertexAttribArray(v.loc.aUv); gl.enableVertexAttribArray(v.loc.aNormal);
        gl.activeTexture(gl.TEXTURE0); gl.bindTexture(gl.TEXTURE_2D, v.tex); gl.uniform1i(v.loc.uTex, 0); gl.uniform1i(v.loc.uMat, 1); gl.uniform1i(v.loc.uTop, 2); gl.uniform1i(v.loc.uDetail, 3);
        gl.uniformMatrix4fv(v.loc.uMvp, false, new Float32Array(v3dMvp(v, vr.w / Math.max(1, vr.h))));
        const cut = v3dCutPlane(v);
        gl.uniform4fv(v.loc.uCut, cut);
        if (!v.tooMany) {
            v3dDraw(gl, v, v.bufs.mesh, gl.TRIANGLES, 1);
            // Formes texturées : un tampon par matériau de rendu, ses textures côté / dessus / détail en unités 1 à 3 (absentes = côté).
            for (const key in v.data.forms) {
                const mat = v.data.forms[key].mat, setName = key.split(':')[0];
                [mat.side, mat.top, mat.detail].forEach(function (tile, u) { gl.activeTexture(gl.TEXTURE1 + u); gl.bindTexture(gl.TEXTURE_2D, formsTexture(gl, v, setName + ':' + (tile === null || tile === undefined ? mat.side : tile))); });
                gl.uniform1f(v.loc.uTexScale, mat.scale); gl.uniform1f(v.loc.uTexOffset, mat.offset);
                v3dDraw(gl, v, v.formBufs[key], gl.TRIANGLES, 1);
            }
            gl.activeTexture(gl.TEXTURE0);
            // Verre des formes (vitres) : après tout l'opaque, mélange alpha sans écrire la profondeur ; alpha = teinte du set.
            gl.enable(gl.BLEND); gl.blendFunc(gl.SRC_ALPHA, gl.ONE_MINUS_SRC_ALPHA); gl.depthMask(false);
            for (const key in v.data.glass) v3dDraw(gl, v, v.glassBufs[key], gl.TRIANGLES, v.data.glass[key].alpha);
            gl.depthMask(true); gl.disable(gl.BLEND);
        }
        if (clean) return;
        // Grille et nappe du sol : jamais coupées (repère au sol) ; les arêtes vides, si.
        gl.uniform4fv(v.loc.uCut, [0, 0, 0, -1]);
        v3dDraw(gl, v, v.bufs.lines, gl.LINES, 1);
        gl.enable(gl.BLEND); gl.blendFunc(gl.SRC_ALPHA, gl.ONE_MINUS_SRC_ALPHA); gl.depthMask(false);
        // Arêtes vides sans test de profondeur : elles sont sous les toits, on doit les voir sans couper.
        gl.uniform4fv(v.loc.uCut, cut);
        gl.disable(gl.DEPTH_TEST); v3dDraw(gl, v, v.bufs.edges, gl.TRIANGLES, 0.35); gl.enable(gl.DEPTH_TEST);
        gl.uniform4fv(v.loc.uCut, [0, 0, 0, -1]);
        // Nappe du terrain (3 %).
        v3dBind(gl, v, v.bufs.sheets);
        gl.uniform1f(v.loc.uAlpha, 0.03); gl.drawArrays(gl.TRIANGLES, 0, 6);
        gl.depthMask(true); gl.disable(gl.BLEND);
    }

    function setView3dInternal(st, on) {
        const v = st.view3d;
        on = !!on;
        if (v.on === on) return;
        v.on = on;
        v.canvas.style.display = on ? '' : 'none';
        if (on) { st.drag = null; st.hover = null; v.dirty = true; view3dFit(st); }
        // Sélection faite en 3D qui n'est pas du mode (volume en Aménagement, objet en Structure) : on la retire en quittant.
        else if (st.selection && (st.selection.kind === 'op') !== isArch(st)) select(st, null, null);
        st.staticDirty = true;
        notifyView3d(st);
        requestRender(st);
    }
    // Coupe face caméra : uniforme du shader seulement, pas de reconstruction du maillage.
    function setView3dCutawayInternal(st, on) {
        st.view3d.cutaway = !!on;
        notifyView3d(st);
        requestRender(st);
    }
    function notifyView3d(st) {
        if (!st.dotnetRef) return;
        st.dotnetRef.invokeMethodAsync('OnView3dChanged', st.view3d.on, st.view3d.cap, st.view3d.cutaway).catch(function () { });
    }

    // ---- Rendu ----------------------------------------------------------------------------------------

    function requestRender(st) {
        if (st.raf) return;
        st.raf = requestAnimationFrame(function () { st.raf = 0; render(st); });
    }

    function render(st) {
        if (st.staticDirty) { renderStatic(st); st.staticDirty = false; }
        renderDynamic(st);
    }

    function setupCtx(canvas, st) {
        const ctx = canvas.getContext('2d');
        ctx.setTransform(st.dpr || 1, 0, 0, st.dpr || 1, 0, 0);
        return ctx;
    }

    function renderStatic(st) {
        const ctx = setupCtx(st.staticCanvas, st);
        const w = st.container.clientWidth, h = st.container.clientHeight;
        ctx.fillStyle = st.palette.bg;
        ctx.fillRect(0, 0, w, h);
        const b = bands(st);
        // Largeur de la bande pour les surcouches HTML (la bascule de la bande longe son bord).
        if (st.container.parentElement) st.container.parentElement.style.setProperty('--bp-strip-w', (b.strip ? b.strip.w : 0) + 'px');
        if (st.view3d.on) { if (b.strip && st.vox) drawElevationStrip(st, ctx, b.strip); return; }   // le centre est le canvas WebGL
        if (isArch(st)) { renderStaticArch(st, ctx); return; }
        renderStaticHouse(st, ctx);
        if (b.strip && st.vox) drawElevationStrip(st, ctx, b.strip);
    }

    // Grille et coordonnées, communes aux deux modes.
    function drawGrid(st, ctx, cs, origin, gw, gh) {
        const plan = st.plan;
        ctx.lineWidth = 1;
        for (let x = 0; x <= plan.grid.width; x++) {
            ctx.strokeStyle = x % 5 === 0 ? st.palette.gridStrong : st.palette.grid;
            ctx.beginPath(); ctx.moveTo(origin.x + x * cs + 0.5, origin.y); ctx.lineTo(origin.x + x * cs + 0.5, origin.y + gh); ctx.stroke();
        }
        for (let y = 0; y <= plan.grid.depth; y++) {
            ctx.strokeStyle = y % 5 === 0 ? st.palette.gridStrong : st.palette.grid;
            ctx.beginPath(); ctx.moveTo(origin.x, origin.y + y * cs + 0.5); ctx.lineTo(origin.x + gw, origin.y + y * cs + 0.5); ctx.stroke();
        }
    }
    // Teinte des pièces sur les couches affichées (roomGrid) : vert si l'analyse la dit fermée, rouge sinon, bleu en attendant.
    function drawRoomTints(st, ctx, cs) {
        const grid = st.roomGrid;
        if (!grid) return;
        const W = st.plan.grid.width, colors = {};
        for (let i = 0; i < grid.length; i++) {
            const n = grid[i];
            if (n <= 0) continue;
            if (colors[n] === undefined) {
                const ar = analysisRoom(st, st.pocketRooms[n]);
                colors[n] = !ar ? 'rgba(100,181,246,0.14)' : ar.contained ? 'rgba(76,175,80,0.16)' : 'rgba(244,67,54,0.16)';
            }
            const p = toScreen(st, i % W, (i / W) | 0);
            ctx.fillStyle = colors[n];
            ctx.fillRect(p.x, p.y, cs, cs);
        }
    }
    // Arêtes vides des pièces (air en diagonale non atteint orthogonalement : tier 0 dans le jeu, calculées par le C#) sur les
    // couches affichées : case rouge encadrée, une fois par colonne.
    function drawEmptyEdges(st, ctx, cs) {
        const rooms = st.analysis && st.analysis.rooms;
        if (!rooms) return;
        const r = viewLayers(st), W = st.plan.grid.width, seen = new Set(), inset = Math.min(3, cs / 4);
        ctx.fillStyle = 'rgba(244,67,54,0.3)'; ctx.strokeStyle = st.palette.error; ctx.lineWidth = 1.5;
        rooms.forEach(function (room) {
            const e = room.emptyEdges || [];
            for (let i = 0; i + 2 < e.length; i += 3) {
                const x = e[i], y = e[i + 1];
                if (e[i + 2] < r[0] || e[i + 2] > r[1] || !inGrid(st, x, y) || seen.has(x + W * y)) continue;
                seen.add(x + W * y);
                const p = toScreen(st, x, y);
                ctx.fillRect(p.x + inset, p.y + inset, cs - 2 * inset, cs - 2 * inset);
                ctx.strokeRect(p.x + inset + 0.5, p.y + inset + 0.5, cs - 2 * inset - 1, cs - 2 * inset - 1);
            }
        });
    }

    function drawRuler(st, ctx, cs, origin) {
        if (cs < 14) return;
        const plan = st.plan;
        ctx.fillStyle = 'rgba(255,255,255,0.35)';
        ctx.font = '10px sans-serif'; ctx.textAlign = 'center'; ctx.textBaseline = 'bottom';
        for (let x = 0; x < plan.grid.width; x += 5) ctx.fillText(x, origin.x + x * cs + cs / 2, origin.y - 2);
        ctx.textAlign = 'right'; ctx.textBaseline = 'middle';
        for (let y = 0; y < plan.grid.depth; y += 5) ctx.fillText(y, origin.x - 4, origin.y + y * cs + cs / 2);
    }

    // ---- Architecture : bandes (élévation à gauche, coupe du côté regardé) et zone de vue du plan ----------
    const STRIP_W = 150, STRIP_MAX_W = 320, STRIP_MAX_CELL = 8, STRIP_PAD = 12, STRIP_TOP = 30, STRIP_BOTTOM = 48, SECTION_W = 300, SECTION_H = 220, GAP = 8;

    // Bande d'élévation : cases carrées, jamais étirées (une tour de 179 couches sur 75 colonnes était déformée 2,4×).
    // La case est imposée par la hauteur disponible (plafonnée), la bande s'élargit pour contenir le plus grand côté de
    // la grille entre STRIP_W et 30 % du conteneur ; au-delà la case se réduit et le sol reste en bas.
    // Hauteur montrée par la bande : toutes les couches en architecture / 3D, le seul bâtiment en mode maison
    // (architecture.height, 20 par défaut, laisserait la moitié de la bande vide au-dessus d'une maison de 9 couches).
    function stripH(st) {
        const vox = st.vox;
        if (!vox) return 1;
        if (isArch(st) || st.view3d.on) return vox.H;
        let h = levelBaseY(st, st.plan.levels.length) + 1;
        st.plan.architecture.ops.forEach(function (op) {
            if (op.subtract || !validOp(op)) return;
            // Un trait de crayon (kind 'cells') n'a pas de boîte englobante : sa hauteur est celle de ses triplets.
            if (op.kind === 'cells') { for (let i = 2; i < op.cells.length; i += 3) h = Math.max(h, op.cells[i] + 1); }
            else h = Math.max(h, opBounds(op).z1 + 1);
        });
        return Math.max(1, Math.min(vox.H, h));
    }

    function stripLayout(st, w, h) {
        const maxW = Math.min(STRIP_MAX_W, Math.floor(w * 0.3)), minW = Math.min(STRIP_W, maxW), vox = st.vox;
        if (!vox) return { w: minW, cell: 1 };
        const cols = Math.max(1, vox.W, vox.D), avail = Math.max(1, h - STRIP_TOP - STRIP_BOTTOM);
        const cell = Math.max(1, Math.min(avail / Math.max(1, stripH(st)), (maxW - 2 * STRIP_PAD) / cols, STRIP_MAX_CELL));
        return { w: Math.round(Math.max(minW, Math.min(maxW, cols * cell + 2 * STRIP_PAD))), cell: cell };
    }

    function bands(st) {
        if (!isArch(st) && !st.view3d.on && !st.houseElev) return { strip: null, section: null };
        const w = st.container.clientWidth, h = st.container.clientHeight;
        const strip = { x: 0, y: 0, w: stripLayout(st, w, h).w, h: h };
        let section = null;
        if (st.cut && !st.view3d.on && isArch(st)) {
            const sw = Math.min(SECTION_W, Math.floor(w * 0.35)), sh = Math.min(SECTION_H, Math.floor(h * 0.35));
            if (st.cut.axis === 'x') section = st.cut.dir > 0 ? { x: w - sw, y: 0, w: sw, h: h } : { x: strip.w + GAP, y: 0, w: sw, h: h };
            else section = st.cut.dir > 0 ? { x: strip.w + GAP, y: h - sh, w: w - strip.w - GAP, h: sh } : { x: strip.w + GAP, y: 0, w: w - strip.w - GAP, h: sh };
        }
        return { strip: strip, section: section };
    }

    // Rectangle écran où le plan est cadré : tout le conteneur en mode maison, moins les bandes en architecture.
    function viewRect(st) {
        const w = st.container.clientWidth, h = st.container.clientHeight;
        const b = bands(st);
        const r = { x: 0, y: 0, w: w, h: h };
        if (b.strip) { r.x = b.strip.w + GAP; r.w -= b.strip.w + GAP; }
        const s = b.section;
        if (s) {
            if (st.cut.axis === 'x') { if (st.cut.dir > 0) r.w -= s.w + GAP; else { r.x += s.w + GAP; r.w -= s.w + GAP; } }
            else { if (st.cut.dir > 0) r.h -= s.h + GAP; else { r.y += s.h + GAP; r.h -= s.h + GAP; } }
        }
        return r;
    }

    function materialName(st, v) { return st.vox && v ? st.vox.palette[v - 1] : null; }

    function renderStaticArch(st, ctx) {
        const cs = cellSize(st), plan = st.plan, vox = st.vox, vr = viewRect(st);
        const origin = toScreen(st, 0, 0);
        const gw = plan.grid.width * cs, gh = plan.grid.depth * cs;
        ctx.save();
        ctx.beginPath(); ctx.rect(vr.x, vr.y, vr.w, vr.h); ctx.clip();

        ctx.fillStyle = 'rgba(255,255,255,0.03)';
        ctx.fillRect(origin.x, origin.y, gw, gh);

        // Fond de plan : sous les blocs et la grille pour rester lisible par-dessus.
        const img = plan.architecture.image;
        if (img && st.bgImage && st.bgVisible && st.bgImage.width) {
            const iw = img.width * cs, ih = iw * st.bgImage.height / st.bgImage.width;
            ctx.globalAlpha = Math.max(0, Math.min(1, img.opacity));
            ctx.drawImage(st.bgImage, origin.x + img.x * cs, origin.y + img.y * cs, iw, ih);
            ctx.globalAlpha = 1;
        }

        if (vox) {
            // Deux couches du dessous en filigrane (surplombs lisibles), puis la couche courante.
            [2, 1].forEach(function (dz) {
                const z = st.layer - dz;
                if (z < 0) return;
                const cells = layerCells(vox, z), alpha = dz === 1 ? 0.22 : 0.10;
                for (let i = 0; i < cells.length; i++) {
                    if (!cells[i]) continue;
                    const p = toScreen(st, i % vox.W, (i / vox.W) | 0);
                    ctx.fillStyle = materialColor(st, vox.palette[cells[i] - 1], alpha);
                    ctx.fillRect(p.x + 1, p.y + 1, cs - 2, cs - 2);
                }
            });
            drawRoomTints(st, ctx, cs);   // poches de la couche (air : jamais sous un bloc de la couche)
            const cells = layerCells(vox, st.layer);
            ctx.strokeStyle = 'rgba(0,0,0,0.5)'; ctx.lineWidth = 1;
            for (let i = 0; i < cells.length; i++) {
                if (!cells[i]) continue;
                const p = toScreen(st, i % vox.W, (i / vox.W) | 0);
                ctx.fillStyle = materialColor(st, vox.palette[cells[i] - 1], 0.9);
                ctx.fillRect(p.x + 1, p.y + 1, cs - 2, cs - 2);
                ctx.strokeRect(p.x + 1.5, p.y + 1.5, cs - 3, cs - 3);
                drawBlockIcon(st, ctx, vox.palette[cells[i] - 1], p, cs, vox.shapes[i + vox.W * vox.D * st.layer]);
            }
        }

        drawGrid(st, ctx, cs, origin, gw, gh);
        drawEmptyEdges(st, ctx, cs);
        drawRuler(st, ctx, cs, origin);

        // Plan de coupe : trait pointillé et deux yeux ; celui du sens du regard est plein.
        st.cutEyes = []; st.cutLine = null;
        if (st.cut) {
            const c = st.cut;
            ctx.save();
            // Trait mixte des plans (grand trait, petit trait), extrémités renforcées ; poignée au milieu pour le déplacer.
            ctx.strokeStyle = st.palette.primary; ctx.lineWidth = 2; ctx.setLineDash([14, 5, 4, 5]);
            ctx.beginPath();
            // Projection européenne : l'œil est du côté de l'observateur et regarde vers le trait (flèche) ; la vue se
            // dessine de l'autre côté. e1 regarde vers les indices croissants (posé avant le trait), e2 l'inverse.
            let e1, e2, ends, grip;
            if (c.axis === 'x') {
                const px = origin.x + (c.index + 0.5) * cs;
                ctx.moveTo(px, origin.y - 12); ctx.lineTo(px, origin.y + gh + 12);
                e1 = { x: px - 18, y: origin.y - 14, dir: 1 }; e2 = { x: px + 18, y: origin.y - 14, dir: -1 };
                ends = [[px, origin.y - 12, px, origin.y], [px, origin.y + gh, px, origin.y + gh + 12]];
                grip = { x: px, y: origin.y + gh / 2 };
                st.cutLine = { axis: 'x', at: px, from: origin.y - 12, to: origin.y + gh + 12 };
            } else {
                const py = origin.y + (c.index + 0.5) * cs;
                ctx.moveTo(origin.x - 12, py); ctx.lineTo(origin.x + gw + 12, py);
                e1 = { x: origin.x - 14, y: py - 18, dir: 1 }; e2 = { x: origin.x - 14, y: py + 18, dir: -1 };
                ends = [[origin.x - 12, py, origin.x, py], [origin.x + gw, py, origin.x + gw + 12, py]];
                grip = { x: origin.x + gw / 2, y: py };
                st.cutLine = { axis: 'y', at: py, from: origin.x - 12, to: origin.x + gw + 12 };
            }
            ctx.stroke();
            ctx.setLineDash([]);
            ctx.lineWidth = 4;
            ends.forEach(function (s) { ctx.beginPath(); ctx.moveTo(s[0], s[1]); ctx.lineTo(s[2], s[3]); ctx.stroke(); });
            ctx.beginPath(); ctx.arc(grip.x, grip.y, 6, 0, Math.PI * 2);
            ctx.fillStyle = st.palette.primary; ctx.fill();
            ctx.strokeStyle = 'rgba(0,0,0,0.6)'; ctx.lineWidth = 1.5; ctx.stroke();
            [e1, e2].forEach(function (e) {
                const active = e.dir === c.dir;
                ctx.beginPath(); ctx.arc(e.x, e.y, 9, 0, Math.PI * 2);
                ctx.fillStyle = active ? st.palette.primary : 'rgba(0,0,0,0.6)'; ctx.fill();
                ctx.strokeStyle = st.palette.primary; ctx.lineWidth = 2; ctx.stroke();
                ctx.beginPath(); ctx.arc(e.x, e.y, 3.5, 0, Math.PI * 2);
                ctx.fillStyle = active ? '#fff' : st.palette.primary; ctx.fill();
                // Petite flèche du sens du regard.
                const ax = c.axis === 'x' ? e.dir : 0, ay = c.axis === 'x' ? 0 : e.dir;
                ctx.beginPath(); ctx.moveTo(e.x + ax * 11, e.y + ay * 11); ctx.lineTo(e.x + ax * 17 - ay * 4, e.y + ay * 17 - ax * 4); ctx.lineTo(e.x + ax * 17 + ay * 4, e.y + ay * 17 + ax * 4); ctx.closePath();
                ctx.fillStyle = st.palette.primary; ctx.fill();
                st.cutEyes.push({ x: e.x, y: e.y, r: 12, dir: e.dir });
            });
            ctx.restore();
        }
        ctx.restore();

        const b = bands(st);
        if (b.strip && vox) drawElevationStrip(st, ctx, b.strip);
        if (b.section && vox) drawSectionBand(st, ctx, b.section);
    }

    // Couleur d'un bloc vu de côté : plein sur le plan de coupe, atténué avec la distance au-delà.
    function sideColor(st, v, depth, cutAlpha, farAlpha) {
        return materialColor(st, st.vox.palette[v - 1], depth === 0 ? cutAlpha : Math.max(0.12, farAlpha - depth * 0.06));
    }

    function bandFrame(st, ctx, r, title) {
        ctx.fillStyle = 'rgba(0,0,0,0.25)';
        ctx.fillRect(r.x, r.y, r.w, r.h);
        ctx.strokeStyle = 'rgba(255,255,255,0.08)'; ctx.lineWidth = 1;
        ctx.strokeRect(r.x + 0.5, r.y + 0.5, r.w - 1, r.h - 1);
        ctx.fillStyle = 'rgba(255,255,255,0.5)'; ctx.font = '10px sans-serif'; ctx.textAlign = 'left'; ctx.textBaseline = 'top';
        ctx.fillText(title, r.x + 8, r.y + 6);
    }

    // Face regardée par l'élévation → axe de projection et sens (sideView gère le miroir : la vue est celle d'un spectateur debout de ce côté).
    const ELEV_SIDES = { s: { axis: 'y', dir: -1 }, e: { axis: 'x', dir: -1 }, n: { axis: 'y', dir: 1 }, w: { axis: 'x', dir: 1 } };
    const ELEV_ORDER = ['s', 'e', 'n', 'w'];
    const OBJ_SIDE_COLOR = 'rgba(207,212,218,0.9)';    // meubles dans l'élévation : même gris clair que les cubes de meubles en 3D
    const OBJ_SIDE_XRAY = 'rgba(207,212,218,0.45)';   // meuble derrière un mur : en transparence par-dessus la façade

    // Élévation : vue depuis la face choisie (sud par défaut = bord bas du plan), échelles indépendantes — c'est d'abord le curseur de couche.
    function drawElevationStrip(st, ctx, r) {
        const vox = st.vox, side = ELEV_SIDES[st.elevSide] || ELEV_SIDES.s;
        // Mode maison : la bande montre aussi le mobilier et son curseur désigne un niveau entier, pas une couche.
        const house = !isArch(st) && !st.view3d.on;
        const objs = house ? v3dObjectCells(st) : null;
        const view = elevation(vox, side.axis, side.dir, objs && objs.size ? objs : null);
        const labels = st.options.labels || {}, sides = labels.sides || {};
        bandFrame(st, ctx, r, '');
        // En-tête : flèches bleues de part et d'autre pour tourner la face regardée, titre centré « Élévation · Sud ».
        const hy = r.y + 12;
        ctx.fillStyle = 'rgba(255,255,255,0.6)'; ctx.font = '10px sans-serif'; ctx.textAlign = 'center'; ctx.textBaseline = 'middle';
        ctx.fillText((labels.elevation || 'Elevation') + ' · ' + (sides[st.elevSide] || st.elevSide.toUpperCase()), r.x + r.w / 2, hy);
        st.elevArrows = [{ x: r.x + 14, y: hy, r: 12, delta: -1 }, { x: r.x + r.w - 14, y: hy, r: 12, delta: 1 }];
        ctx.fillStyle = st.palette.primary;
        st.elevArrows.forEach(function (a) {
            // Pointe vers l'extérieur : ◂ à gauche (delta −1), ▸ à droite (delta +1).
            ctx.beginPath(); ctx.moveTo(a.x - a.delta * 4, a.y - 6); ctx.lineTo(a.x + a.delta * 5, a.y); ctx.lineTo(a.x - a.delta * 4, a.y + 6); ctx.closePath(); ctx.fill();
        });
        // Cases carrées (stripLayout), même case pour les quatre faces : la silhouette ne change pas quand on tourne.
        // Le sol reste en bas (la bande est aussi le curseur de couche) ; la ligne de sol laisse la place à la pilule annuler/zoom.
        const top = r.y + STRIP_TOP, bottom = r.y + r.h - STRIP_BOTTOM;
        const cell = stripLayout(st, st.container.clientWidth, st.container.clientHeight).cell, colW = cell, rowH = cell;
        const x0 = r.x + (r.w - view.cols * colW) / 2;
        const zy = function (z) { return bottom - (z + 1) * rowH; };
        const rows = stripH(st);
        for (let z = 0; z < rows; z++) for (let c = 0; c < view.cols; c++) {
            const o = c + view.cols * z, v = view.beyond[o], furniture = view.obj ? view.obj[o] : 0;
            if (!v && !furniture) continue;
            const px = x0 + c * colW, py = zy(z), pw = Math.max(1, colW - 0.5), ph = Math.max(1, rowH - 0.5);
            // Silhouette uniforme : pas d'ombrage par profondeur dans l'élévation (dégradé illisible sur une tour).
            if (v) { ctx.fillStyle = sideColor(st, v, 0, 0.9, 0.9); ctx.fillRect(px, py, pw, ph); }
            if (furniture) { ctx.fillStyle = furniture === 1 ? OBJ_SIDE_COLOR : OBJ_SIDE_XRAY; ctx.fillRect(px, py, pw, ph); }
        }
        // Sol, couche courante (bande + trait) et son numéro.
        ctx.strokeStyle = 'rgba(255,255,255,0.35)'; ctx.lineWidth = 1;
        ctx.beginPath(); ctx.moveTo(r.x + 4, bottom + 0.5); ctx.lineTo(r.x + r.w - 4, bottom + 0.5); ctx.stroke();
        // Mode maison : un trait fin sous chaque dalle pour lire les étages dans la silhouette.
        if (house) {
            ctx.strokeStyle = 'rgba(255,255,255,0.18)'; ctx.lineWidth = 1;
            for (let k = 1; k < st.plan.levels.length; k++) {
                const y = Math.round(zy(levelBaseY(st, k)) + rowH) + 0.5;
                ctx.beginPath(); ctx.moveTo(r.x + 4, y); ctx.lineTo(r.x + r.w - 4, y); ctx.stroke();
            }
        }
        // Curseur : une couche en architecture et en 3D (la scène 3D, elle, n'a pas de teinte de couche), la tranche
        // complète du niveau courant en maison.
        const z0 = house ? levelBaseY(st, st.level) : st.layer;
        const z1 = house ? Math.max(z0, Math.min(rows - 1, levelBaseY(st, st.level + 1) - 1)) : st.layer;
        ctx.fillStyle = 'rgba(79,163,247,0.25)';
        ctx.fillRect(r.x + 2, zy(z1), r.w - 4, (z1 - z0 + 1) * rowH);
        ctx.strokeStyle = st.palette.primary; ctx.lineWidth = 2;
        ctx.beginPath(); ctx.moveTo(r.x + 2, zy(z1)); ctx.lineTo(r.x + r.w - 2, zy(z1)); ctx.stroke();
        ctx.fillStyle = st.palette.primary; ctx.font = 'bold 11px sans-serif'; ctx.textAlign = 'right'; ctx.textBaseline = 'top';
        ctx.fillText(house ? 'N ' + (st.level - (st.plan.groundIndex || 0)) : 'z ' + st.layer, r.x + r.w - 8, r.y + 24);
        st.stripGeom = { top: top, bottom: bottom, rowH: rowH, x0: x0, colW: colW, view: view, axis: side.axis, dir: side.dir, index: null };
    }

    // Cellule (colonne de vue, couche) sous le pointeur dans la bande de coupe, ou null.
    function sideCellAt(g, px, py) {
        if (!g) return null;
        const c = Math.floor((px - g.x0) / g.colW), z = Math.floor((g.bottom - py) / g.rowH);
        return c < 0 || c >= g.view.cols || z < 0 || z >= g.view.rows ? null : { c: c, z: z };
    }

    // Même chose, mais ramené dans la grille de la vue (poursuite d'un drag hors de la bande).
    function sideCellClamped(g, px, py) {
        const c = Math.floor((px - g.x0) / g.colW), z = Math.floor((g.bottom - py) / g.rowH);
        return { c: Math.max(0, Math.min(g.view.cols - 1, c)), z: Math.max(0, Math.min(g.view.rows - 1, z)) };
    }

    // Coordonnée du plan le long des colonnes de la vue (miroir selon le sens du regard).
    function sideCol(g, c) { return (g.axis === 'x' ? g.dir < 0 : g.dir > 0) ? g.view.cols - 1 - c : c; }

    // Op créée par un drag de forme dans la coupe : tracée dans le plan de coupe (colonnes × couches), la « hauteur »
    // des options devient la profondeur perpendiculaire au plan, centrée sur lui ; un cylindre prend l'axe de la coupe.
    function sideShapeOp(st, drag, cell) {
        const g = drag.geom, s = st.shape, along = g.axis === 'x' ? st.plan.grid.width : st.plan.grid.depth;
        const pos = function (u, k, z) { return g.axis === 'x' ? [k, u, z] : [u, k, z]; };
        const op = { kind: drag.tool, subtract: !!drag.subtract, material: drag.subtract ? null : st.material, hollow: false, thickness: 1 };
        const u0 = sideCol(g, drag.start.c), u1 = sideCol(g, cell.c), z0 = drag.start.z, z1 = cell.z;
        const h = Math.max(1, s.height | 0), kk = g.index - Math.floor((h - 1) / 2);
        const k0 = Math.max(0, kk), k1 = Math.min(along - 1, kk + h - 1);
        if (drag.tool === 'room') { op.kind = 'box'; op.a = pos(u0, k0, z0); op.b = pos(u1, k1, z1); op.hollow = true; op.closed = true; }   // pièce : pavé creux fermé, épaisseur 1
        else if (drag.tool === 'box') { op.a = pos(u0, k0, z0); op.b = pos(u1, k1, z1); op.hollow = !!s.hollow; op.thickness = Math.max(1, s.thickness | 0); }
        else if (drag.tool === 'line' || drag.tool === 'curve') { op.a = pos(u0, g.index, z0); op.b = pos(u1, g.index, z1); if (drag.tool === 'curve') op.c = midpoint(op.a, op.b); }
        else {
            const r = Math.max(Math.abs(u1 - u0), Math.abs(z1 - z0));
            if (drag.tool === 'sphere') { op.a = pos(u0 - r, g.index - r, z0 - r); op.b = pos(u0 + r, g.index + r, z0 + r); }
            else { op.kind = 'cylinder'; op.axis = g.axis; const d0 = drag.tool === 'disc' ? g.index : k0, d1 = drag.tool === 'disc' ? g.index : k1; op.a = pos(u0 - r, d0, z0 - r); op.b = pos(u0 + r, d1, z0 + r); }
            op.hollow = !!s.hollow; op.thickness = Math.max(1, s.thickness | 0);
        }
        if (drag.tool !== 'room') applyForm(op, st.form, st.formRot);
        return op;
    }

    // Cellules d'une op sur le plan de coupe, dans la grille de la vue : Uint8Array(cols·rows), index c + cols·z.
    function opPlaneMask(op, g) {
        const cols = g.view.cols, rows = g.view.rows, mask = new Uint8Array(cols * rows);
        if (!validOp(op)) return mask;
        const ax = g.axis === 'x' ? 0 : 1, au = 1 - ax, mirror = g.axis === 'x' ? g.dir < 0 : g.dir > 0;
        const put = function (c) { if (c[ax] !== g.index || c[2] < 0 || c[2] >= rows) return; const u = c[au]; if (u >= 0 && u < cols) mask[(mirror ? cols - 1 - u : u) + cols * c[2]] = 1; };
        if (op.kind === 'cells') { for (let i = 0; i + 2 < op.cells.length; i += 3) put([op.cells[i], op.cells[i + 1], op.cells[i + 2]]); return mask; }
        if (op.kind === 'line' || op.kind === 'curve') { shapeCells(op).forEach(put); return mask; }
        const outer = opBounds(op), inner = innerBounds(op, outer), axis = opAxis(op);
        for (let z = Math.max(0, outer.z0); z <= Math.min(rows - 1, outer.z1); z++)
            for (let u = 0; u < cols; u++) {
                const x = g.axis === 'x' ? g.index : u, y = g.axis === 'x' ? u : g.index;
                if (insideBounds(op.kind, axis, outer, x, y, z) && !(inner && insideBounds(op.kind, axis, inner, x, y, z))) put([x, y, z]);
            }
        return mask;
    }

    // Position dans le plan (x, y, z) du bloc à poser ou à effacer pour la cellule (c, z) de la coupe :
    // on pose sur le plan de coupe ; on efface le bloc coupé, sinon le premier bloc visible au-delà.
    function sideTarget(g, c, z, erase) {
        const v = g.view, o = c + v.cols * z;
        const mirror = g.axis === 'x' ? g.dir < 0 : g.dir > 0, cc = mirror ? v.cols - 1 - c : c;
        const pos = function (k) { return g.axis === 'x' ? { x: k, y: cc, z: z } : { x: cc, y: k, z: z }; };
        if (!erase || v.cut[o]) return pos(g.index);
        return v.beyond[o] ? pos(g.index + g.dir * (v.depth[o] + 1)) : null;
    }

    // Coupe : plan de coupe plein + au-delà atténué, z vers le haut, cases carrées ajustées à la bande.
    function drawSectionBand(st, ctx, r) {
        const vox = st.vox, c = st.cut, view = section(vox, c.axis, c.index, c.dir);
        const label = st.options.labels && st.options.labels.section || 'Section';
        bandFrame(st, ctx, r, label + ' ' + c.axis + ' = ' + c.index + (c.axis === 'x' ? (c.dir > 0 ? ' →' : ' ←') : (c.dir > 0 ? ' ↓' : ' ↑')));
        const pad = 16, top = r.y + 24, bottom = r.y + r.h - pad;
        const cell = Math.max(1, Math.min((r.w - 2 * pad) / view.cols, (bottom - top) / view.rows));
        const x0 = r.x + (r.w - view.cols * cell) / 2;
        const zy = function (z) { return bottom - (z + 1) * cell; };
        for (let z = 0; z < view.rows; z++) for (let col = 0; col < view.cols; col++) {
            const o = col + view.cols * z, v = view.cut[o];
            if (v) {
                ctx.fillStyle = sideColor(st, v, 0, 0.95, 0.95);
                ctx.fillRect(x0 + col * cell, zy(z), cell, cell);
                ctx.strokeStyle = 'rgba(0,0,0,0.5)'; ctx.lineWidth = 1;
                if (cell >= 4) ctx.strokeRect(x0 + col * cell + 0.5, zy(z) + 0.5, cell - 1, cell - 1);
                // Forme de la case coupée : son icône (même colonne que sideView, miroir compris).
                if (cell >= ICON_MIN_CELL) {
                    const cc = (c.axis === 'x' ? c.dir < 0 : c.dir > 0) ? view.cols - 1 - col : col;
                    const s = shapeInfo(vox.shapes[(c.axis === 'x' ? c.index + vox.W * cc : cc + vox.W * c.index) + vox.W * vox.D * z]);
                    const icon = s && iconFor(st, s.icon);
                    if (icon) ctx.drawImage(icon, x0 + col * cell + 2, zy(z) + 2, cell - 4, cell - 4);
                }
            } else if (view.beyond[o]) {
                ctx.fillStyle = sideColor(st, view.beyond[o], view.depth[o] + 1, 0.55, 0.55);
                ctx.fillRect(x0 + col * cell, zy(z), cell, cell);
            }
        }
        ctx.strokeStyle = 'rgba(255,255,255,0.35)'; ctx.lineWidth = 1;
        ctx.beginPath(); ctx.moveTo(x0, bottom + 0.5); ctx.lineTo(x0 + view.cols * cell, bottom + 0.5); ctx.stroke();
        ctx.strokeStyle = st.palette.primary; ctx.lineWidth = 2;
        ctx.beginPath(); ctx.moveTo(x0 - 6, zy(st.layer) + cell / 2); ctx.lineTo(x0 + view.cols * cell + 6, zy(st.layer) + cell / 2); ctx.stroke();
        st.sectionGeom = { top: top, bottom: bottom, rowH: cell, x0: x0, colW: cell, view: view, axis: c.axis, dir: c.dir, index: c.index };
    }

    // Aménagement : le niveau courant vu de dessus — pièces teintées, blocs de sa tranche (couche de la dalle en teinte sol,
    // couches d'air comme des murs), grille, arêtes vides.
    function renderStaticHouse(st, ctx) {
        const cs = cellSize(st);
        const plan = st.plan;

        const origin = toScreen(st, 0, 0);
        const gw = plan.grid.width * cs, gh = plan.grid.depth * cs;

        const vr = viewRect(st);
        ctx.save();
        ctx.beginPath(); ctx.rect(vr.x, vr.y, vr.w, vr.h); ctx.clip();   // le plan ne déborde pas sur la bande d'élévation

        // Terrain de la grille.
        ctx.fillStyle = 'rgba(255,255,255,0.03)';
        ctx.fillRect(origin.x, origin.y, gw, gh);

        drawRoomTints(st, ctx, cs);

        if (st.vox) {
            const vox = st.vox, base = levelBaseY(st, st.level), top = Math.min(vox.H - 1, base + levelHeight(st, st.level));
            for (let z = base; z <= top; z++) {
                const cells = layerCells(vox, z), floor = z === base;
                for (let i = 0; i < cells.length; i++) {
                    if (!cells[i]) continue;
                    const p = toScreen(st, i % vox.W, (i / vox.W) | 0);
                    ctx.fillStyle = materialColor(st, vox.palette[cells[i] - 1], floor ? 0.35 : 0.9);
                    if (floor) ctx.fillRect(p.x, p.y, cs, cs);
                    else {
                        ctx.fillRect(p.x + 1, p.y + 1, cs - 2, cs - 2); ctx.strokeStyle = 'rgba(0,0,0,0.5)'; ctx.strokeRect(p.x + 1.5, p.y + 1.5, cs - 3, cs - 3);
                        drawBlockIcon(st, ctx, vox.palette[cells[i] - 1], p, cs, vox.shapes[i + vox.W * vox.D * z]);
                    }
                }
            }
        }

        drawGrid(st, ctx, cs, origin, gw, gh);
        drawEmptyEdges(st, ctx, cs);
        drawRuler(st, ctx, cs, origin);
        ctx.restore();
    }

    // Couleur du bloc dans le jeu, extraite de son icône côté serveur (ClientMaterial.Color) ; un matériau
    // que le catalogue ne connaît pas (bloc d'un mod) retombe sur la couleur de son tier.
    function materialRgb(st, name) {
        let rgb = st.matRgb[name];
        if (rgb === undefined) {
            const m = st.materialsByName[name];
            rgb = parseColor(m && m.color, parseColor(TIER_COLORS[Math.max(0, Math.min(5, m ? m.tier : 0))], [125, 125, 125]));
            st.matRgb[name] = rgb;
        }
        return rgb;
    }
    function materialColor(st, material, alpha) {
        const c = materialRgb(st, material);
        return 'rgba(' + c[0] + ',' + c[1] + ',' + c[2] + ',' + alpha + ')';
    }

    // Icône du bloc par-dessus la couleur de la case : c'est elle qui sépare les matériaux qu'aucune couleur ne
    // peut distinguer (onze bois composites d'un même brun). En dessous du seuil elle n'est plus que du bruit ;
    // le plan de maison, plus dense (objets, cotes, pièces), demande un zoom bien plus fort que l'architecture.
    // Case à forme (code de vox.shapes) : l'icône de la forme remplace celle du matériau ; une forme tournable pour ce
    // matériau reçoit un triangle sur son côté bas, dès LOW_MIN_CELL.
    function drawBlockIcon(st, ctx, material, p, cs, code) {
        if (!material) return;
        const s = code ? shapeInfo(code) : null;
        if (cs >= (isArch(st) ? ICON_MIN_CELL : ICON_MIN_CELL_HOUSE)) {
            const icon = (s && iconFor(st, s.icon)) || iconFor(st, material + '_FG');
            if (icon) ctx.drawImage(icon, p.x + 2, p.y + 2, cs - 4, cs - 4);
        }
        if (s && cs >= LOW_MIN_CELL && rotatableCached(st, material, s.form)) {
            const t = lowSideTriangle(p, cs, s.low);
            ctx.save();
            ctx.fillStyle = 'rgba(0,0,0,0.6)'; ctx.strokeStyle = 'rgba(255,255,255,0.75)'; ctx.lineWidth = 1;
            ctx.beginPath(); ctx.moveTo(t[0][0], t[0][1]); ctx.lineTo(t[1][0], t[1][1]); ctx.lineTo(t[2][0], t[2][1]); ctx.closePath();
            ctx.fill(); ctx.stroke();
            ctx.restore();
        }
    }
    function rotatableCached(st, material, form) {
        const k = material + '|' + form;
        let r = st.rotatable.get(k);
        if (r === undefined) { r = formRotatable(st, material, form); st.rotatable.set(k, r); }
        return r;
    }

    // Contour clair autour de ce qui est déjà posé dans le matériau courant : on repère d'un coup d'œil, en
    // construisant, où il est employé. Seules les arêtes de bord sont tracées (comme les ouvertures) : un cadre
    // par case noierait un mur entier d'un même matériau.
    // Structure seulement (l'Aménagement n'a pas de matériau).
    function drawMaterialHighlight(st, ctx, cs) {
        const vox = st.vox;
        if (!st.material || cs < 8 || !vox) return;
        const cells = layerCells(vox, st.layer);
        const same = function (x, y) {
            if (x < 0 || y < 0 || x >= vox.W || y >= vox.D) return false;
            const v = cells[x + vox.W * y];
            return !!v && vox.palette[v - 1] === st.material;
        };
        // Balayage borné à la zone visible : la couche dynamique est redessinée à chaque frame et un plan peut
        // faire 200 x 200 cases.
        const g = st.plan.grid, vr = viewRect(st);
        const a = toCell(st, vr.x, vr.y), b = toCell(st, vr.x + vr.w, vr.y + vr.h);
        const x0 = Math.max(0, a.x), x1 = Math.min(g.width - 1, b.x), y0 = Math.max(0, a.y), y1 = Math.min(g.depth - 1, b.y);
        ctx.save();
        ctx.strokeStyle = 'rgba(255,255,255,0.85)'; ctx.lineWidth = 2;
        ctx.beginPath();
        for (let y = y0; y <= y1; y++) {
            for (let x = x0; x <= x1; x++) {
                if (!same(x, y)) continue;
                const p = toScreen(st, x, y);
                if (!same(x, y - 1)) { ctx.moveTo(p.x, p.y); ctx.lineTo(p.x + cs, p.y); }
                if (!same(x, y + 1)) { ctx.moveTo(p.x, p.y + cs); ctx.lineTo(p.x + cs, p.y + cs); }
                if (!same(x - 1, y)) { ctx.moveTo(p.x, p.y); ctx.lineTo(p.x, p.y + cs); }
                if (!same(x + 1, y)) { ctx.moveTo(p.x + cs, p.y); ctx.lineTo(p.x + cs, p.y + cs); }
            }
        }
        ctx.stroke();
        ctx.restore();
    }

    function materialLabel(st, name) {
        const m = st.materialsByName[name];
        return m && m.label || name;
    }

    // Matériaux à légender : ceux du bâtiment entier (palette des formes), dans les trois modes.
    function legendMaterials(st) {
        const seen = {};
        if (st.vox) st.vox.palette.forEach(function (n) { if (n) seen[n] = true; });
        const order = st.catalog.materials || [];
        return Object.keys(seen).filter(function (n) { return n; })
            .sort(function (a, b) {
                const ia = order.findIndex(function (m) { return m.name === a; }), ib = order.findIndex(function (m) { return m.name === b; });
                return (ia < 0 ? 1e9 : ia) - (ib < 0 ? 1e9 : ib);
            });
    }

    function legendFrame(ctx, x, y, w, h) {
        ctx.fillStyle = 'rgba(0,0,0,0.55)';
        ctx.fillRect(x, y, w, h);
        ctx.strokeStyle = 'rgba(255,255,255,0.15)'; ctx.lineWidth = 1;
        ctx.strokeRect(x + 0.5, y + 0.5, w - 1, h - 1);
    }

    function legendSwatch(st, ctx, name, x, y, sw) {
        ctx.fillStyle = materialColor(st, name, 1);
        ctx.fillRect(x, y, sw, sw);
        ctx.strokeStyle = 'rgba(0,0,0,0.6)'; ctx.lineWidth = 1;
        ctx.strokeRect(x + 0.5, y + 0.5, sw - 1, sw - 1);
    }

    // Chevron cliquable, dans l'encadré : pointe vers le bas quand la légende est ouverte (elle se replie),
    // vers le haut sur la pastille repliée (elle se rouvre).
    function legendToggle(st, ctx, x, y, open) {
        st.legendHit = { x: x, y: y, w: LEGEND_BTN, h: LEGEND_BTN };
        const cx = x + LEGEND_BTN / 2, cy = y + LEGEND_BTN / 2, d = open ? 1 : -1;
        ctx.fillStyle = st.palette.text;
        ctx.beginPath();
        ctx.moveTo(cx - 4, cy - 2 * d); ctx.lineTo(cx + 4, cy - 2 * d); ctx.lineTo(cx, cy + 3 * d);
        ctx.closePath(); ctx.fill();
    }

    // Légende des matériaux employés : pastille + libellé, dans le coin bas-gauche de la zone de vue. Son chevron
    // la replie en une pastille de trois couleurs, d'où on la rouvre. L'export d'image (qui compose les trois
    // calques) emporte la légende ouverte, mais ni le chevron ni la pastille repliée : ce sont des commandes.
    function drawLegend(st, ctx) {
        st.legendHit = null;
        let names = legendMaterials(st);
        if (!names.length) return;
        // Au-dessus de la pilule annuler / zoom (surimpression HTML bas-gauche, absente de l'export PNG).
        const vr = viewRect(st), pad = 8, sw = 12, gap = 7, line = 18, bottom = st.exporting ? 10 : 48;
        ctx.save();
        if (!st.legend) {
            if (!st.exporting) {
                const n = Math.min(3, names.length), bh = pad * 2 + sw;
                const bw = pad * 2 + n * (sw + 3) - 3 + gap + LEGEND_BTN;
                const x = Math.round(vr.x + 10), y = Math.round(vr.y + vr.h - bottom - bh);
                legendFrame(ctx, x, y, bw, bh);
                for (let i = 0; i < n; i++) legendSwatch(st, ctx, names[i], x + pad + i * (sw + 3), y + pad, sw);
                legendToggle(st, ctx, x + bw - pad - LEGEND_BTN, y + (bh - LEGEND_BTN) / 2, false);
            }
            ctx.restore();
            return;
        }
        const room = Math.max(2, Math.floor((vr.h - 30 - bottom - pad * 2) / line));
        let extra = 0;
        if (names.length > room) { extra = names.length - room + 1; names = names.slice(0, room - 1); }
        ctx.font = '12px sans-serif'; ctx.textBaseline = 'middle'; ctx.textAlign = 'left';
        const labels = names.map(function (n) { return materialLabel(st, n); });
        if (extra) labels.push('+' + extra);
        let tw = 0;
        labels.forEach(function (l) { tw = Math.max(tw, ctx.measureText(l).width); });
        const bw = pad * 2 + sw + gap + tw + gap + LEGEND_BTN, bh = pad * 2 + line * labels.length;
        const x = Math.round(vr.x + 10), y = Math.round(vr.y + vr.h - bottom - bh);
        legendFrame(ctx, x, y, bw, bh);
        labels.forEach(function (label, i) {
            const cy = y + pad + line * i + line / 2;
            if (i < names.length) legendSwatch(st, ctx, names[i], x + pad, cy - sw / 2, sw);
            ctx.fillStyle = st.palette.text;
            ctx.fillText(label, x + pad + sw + gap, cy);
        });
        if (!st.exporting) legendToggle(st, ctx, x + bw - pad - LEGEND_BTN, y + pad + (line - LEGEND_BTN) / 2, true);
        ctx.restore();
    }

    function renderDynamic(st) {
        const ctx = setupCtx(st.dynamicCanvas, st);
        const w = st.container.clientWidth, h = st.container.clientHeight;
        ctx.clearRect(0, 0, w, h);
        if (st.view3d.on) {
            view3dRender(st);
            if (st.view3d.tooMany) {
                const vr = viewRect(st), label = st.options.labels && st.options.labels.tooManyBlocks || 'Too many blocks for the 3D view';
                ctx.fillStyle = st.palette.text; ctx.font = '13px sans-serif'; ctx.textAlign = 'center'; ctx.textBaseline = 'middle';
                ctx.fillText(label, vr.x + vr.w / 2, vr.y + vr.h / 2);
            }
            if (st.drag && st.drag.kind === 'orbit' && st.drag.moved) {
                // Repère du pivot pendant l'orbite.
                const vr = viewRect(st), n = v3dProject(st.view3d, vr.w / Math.max(1, vr.h), st.drag.pivot || st.view3d.target);
                const sx = vr.x + (n[0] + 1) / 2 * vr.w, sy = vr.y + (1 - n[1]) / 2 * vr.h;
                ctx.beginPath(); ctx.arc(sx, sy, 4, 0, Math.PI * 2);
                ctx.fillStyle = st.palette.secondary; ctx.fill(); ctx.lineWidth = 1.5; ctx.strokeStyle = st.palette.bg; ctx.stroke();
            }
            drawLegend(st, ctx);
            return;
        }
        const cs = cellSize(st);
        if (isArch(st)) { renderDynamicArch(st, ctx, cs); return; }
        const level = cur(st);
        const vr = viewRect(st);
        ctx.save();
        ctx.beginPath(); ctx.rect(vr.x, vr.y, vr.w, vr.h); ctx.clip();

        // Objets au sol puis empilés.
        const ordered = level.objects.slice().sort(function (a, b) { return (a.attachedTo ? 1 : 0) - (b.attachedTo ? 1 : 0); });
        ordered.forEach(function (o) { drawObject(st, ctx, o, cs); });

        drawRoomLabels(st, ctx, cs);
        level.rooms.forEach(function (room) {
            const ar = analysisRoom(st, room.id);
            if (ar && !ar.contained && ar.failCell && (ar.failLevel == null || ar.failLevel === st.level)) {
                const fp2 = toScreen(st, ar.failCell.x, ar.failCell.y);
                ctx.strokeStyle = st.palette.error; ctx.lineWidth = 3;
                ctx.beginPath(); ctx.moveTo(fp2.x + 4, fp2.y + 4); ctx.lineTo(fp2.x + cs - 4, fp2.y + cs - 4); ctx.moveTo(fp2.x + cs - 4, fp2.y + 4); ctx.lineTo(fp2.x + 4, fp2.y + cs - 4); ctx.stroke();
            }
        });

        // Marqueurs des problèmes localisés sur ce niveau.
        if (st.analysis && st.analysis.issues) {
            st.analysis.issues.forEach(function (i) {
                if (!i.cell || (i.level != null && i.level !== st.level)) return;
                const p = toScreen(st, i.cell.x, i.cell.y);
                ctx.fillStyle = i.severity === 2 ? st.palette.error : i.severity === 1 ? st.palette.warning : st.palette.info;
                ctx.beginPath(); ctx.arc(p.x + cs - 5, p.y + 5, 4, 0, Math.PI * 2); ctx.fill();
            });
        }

        // Sélection (si elle est sur ce niveau).
        if (st.selection) {
            ctx.strokeStyle = st.palette.secondary; ctx.lineWidth = 2; ctx.setLineDash([4, 3]);
            if (st.selection.kind === 'object') {
                const f = findObject(st, st.selection.id);
                if (f && f.level === st.level) objectCells(st, f.item).forEach(function (c) { const p = toScreen(st, c.x, c.y); ctx.strokeRect(p.x + 1, p.y + 1, cs - 2, cs - 2); });
            } else if (st.selection.kind === 'room' && st.roomGrid) {
                const W = st.plan.grid.width;
                st.roomGrid.forEach(function (n, i) { if (n > 0 && st.pocketRooms[n] === st.selection.id) { const p = toScreen(st, i % W, (i / W) | 0); ctx.strokeRect(p.x + 0.5, p.y + 0.5, cs - 1, cs - 1); } });
            }
            ctx.setLineDash([]);
        }

        // Aperçu de l'outil.
        drawPreview(st, ctx, cs);
        ctx.restore();

        drawLegend(st, ctx);
    }

    // Nom de la pièce survolée ou sélectionnée (deux modes), ancré sur la case de son empreinte la plus proche du centre ;
    // volume, tier et nombre d'arêtes vides (tier 0) une fois l'analyse arrivée.
    function drawRoomLabels(st, ctx, cs) {
        if (cs < 12) return;
        const hovered = st.hover ? roomAt(st, st.hover.x, st.hover.y) : null, selected = st.selection && st.selection.kind === 'room' ? st.selection.id : null;
        for (const n in st.roomLabels) {
            const id = st.pocketRooms[n], f = id && (id === hovered || id === selected) ? findRoom(st, id) : null;
            if (!f) continue;
            const ar = analysisRoom(st, id), p = toScreen(st, st.roomLabels[n].x, st.roomLabels[n].y);
            let label = f.item.name + (ar ? '  ' + ar.volume + 'm³ T' + ar.averageTier.toFixed(2) : '');
            if (ar && ar.emptyEdgeCount) label += '  ' + ar.emptyEdgeCount + ' ' + ((st.options.labels && st.options.labels.emptyEdges) || 'empty edges (tier 0)');
            ctx.font = 'bold ' + Math.max(10, cs * 0.45) + 'px sans-serif';
            ctx.textAlign = 'left'; ctx.textBaseline = 'middle';
            ctx.fillStyle = 'rgba(0,0,0,0.6)';
            const tw = ctx.measureText(label).width;
            ctx.fillRect(p.x + cs + 2, p.y + cs / 2 - cs * 0.3, tw + 6, cs * 0.6);
            ctx.fillStyle = '#fff';
            ctx.fillText(label, p.x + cs + 5, p.y + cs / 2);
        }
    }

    function drawObject(st, ctx, o, cs) {
        const info = st.objectsByName[o.type];
        const ao = analysisObject(st, o.id);
        const cells = objectCells(st, o);
        const placed = ao ? ao.placed : true;
        const known = ao ? ao.known : !!info;
        const isDoor = info && info.isDoor;
        const attached = !!o.attachedTo;

        cells.forEach(function (c) {
            const p = toScreen(st, c.x, c.y);
            if (attached) return;
            if (!known) ctx.fillStyle = 'rgba(158,158,158,0.6)';
            else if (!placed) ctx.fillStyle = 'rgba(244,67,54,0.45)';
            else if (c.kind === KIND.WALL) ctx.fillStyle = 'rgba(255,183,77,0.85)';
            else if (isDoor) ctx.fillStyle = 'rgba(255,183,77,0.25)';
            else if (info && info.isCraftingTable) ctx.fillStyle = 'rgba(100,181,246,0.55)';
            else ctx.fillStyle = 'rgba(156,204,101,0.5)';
            const inset = c.kind === KIND.OCCUPIED && isDoor ? cs * 0.3 : 2;
            ctx.fillRect(p.x + inset, p.y + inset, cs - inset * 2, cs - inset * 2);
            if (!placed) {
                ctx.strokeStyle = st.palette.error; ctx.lineWidth = 1.5;
                ctx.beginPath(); ctx.moveTo(p.x + 3, p.y + 3); ctx.lineTo(p.x + cs - 3, p.y + cs - 3); ctx.stroke();
            }
        });

        // Contour de l'emprise et icône au centre.
        const xs = cells.map(function (c) { return c.x; }), ys = cells.map(function (c) { return c.y; });
        const minX = Math.min.apply(null, xs), maxX = Math.max.apply(null, xs), minY = Math.min.apply(null, ys), maxY = Math.max.apply(null, ys);
        const p0 = toScreen(st, minX, minY);
        const bw = (maxX - minX + 1) * cs, bh = (maxY - minY + 1) * cs;
        if (!attached) {
            ctx.strokeStyle = placed ? 'rgba(255,255,255,0.35)' : st.palette.error; ctx.lineWidth = 1;
            ctx.strokeRect(p0.x + 1.5, p0.y + 1.5, bw - 3, bh - 3);
        }

        const icon = iconFor(st, o.type);
        const size = attached ? Math.min(bw, bh) * 0.45 : Math.min(bw, bh, cs * 2) * 0.7;
        const cx = p0.x + bw / 2, cy = p0.y + bh / 2;
        const ix = attached ? cx + cs * 0.18 : cx, iy = attached ? cy - cs * 0.18 : cy;
        if (icon) ctx.drawImage(icon, ix - size / 2, iy - size / 2, size, size);
        else {
            ctx.fillStyle = 'rgba(0,0,0,0.5)';
            ctx.beginPath(); ctx.arc(ix, iy, size / 2.5, 0, Math.PI * 2); ctx.fill();
        }
        if (attached) {
            ctx.strokeStyle = st.palette.secondary; ctx.lineWidth = 1.5;
            ctx.beginPath(); ctx.arc(ix, iy, size / 2 + 2, 0, Math.PI * 2); ctx.stroke();
        }
        if (!attached && cs >= 20 && (o.rotation || 0) !== 0) {
            ctx.fillStyle = 'rgba(255,255,255,0.7)'; ctx.font = '9px sans-serif'; ctx.textAlign = 'left'; ctx.textBaseline = 'top';
            ctx.fillText('r' + o.rotation, p0.x + 3, p0.y + 3);
        }
    }

    function drawPreview(st, ctx, cs) {
        if (!st.hover) return;
        const hx = st.hover.x, hy = st.hover.y;
        if (st.drag && st.drag.kind === 'rect') {   // gomme rectangulaire des objets
            const r = normRect(st.drag.start, st.hover);
            ctx.fillStyle = 'rgba(244,67,54,0.25)';
            for (let x = r.x0; x <= r.x1; x++) for (let y = r.y0; y <= r.y1; y++) { const p = toScreen(st, x, y); ctx.fillRect(p.x, p.y, cs, cs); }
            return;
        }
        if (!inGrid(st, hx, hy)) return;
        if (st.tool === 'object' && st.objectType) {
            const ghost = { type: st.objectType, x: hx, y: hy, rotation: st.rotation, id: '__ghost' };
            ctx.globalAlpha = 0.55;
            const cells = objectCells(st, ghost);
            const target = objectAt(st, hx, hy);
            const info = st.objectsByName[st.objectType];
            const canAttach = target && !target.attachedTo && st.objectsByName[target.type] && st.objectsByName[target.type].hasTableSurface && info && info.canBeOnSurface;
            const floorZ = levelBaseY(st, st.level) + 1;
            cells.forEach(function (c) {
                const p = toScreen(st, c.x, c.y);
                const blocked = !canAttach && (!inGrid(st, c.x, c.y) || (objectAt(st, c.x, c.y) && c.kind !== KIND.WALL)
                    || (c.kind !== KIND.WALL && archSolidAt(st, c.x, c.y, floorZ + c.dz)));
                ctx.fillStyle = canAttach ? 'rgba(255,183,77,0.8)' : blocked ? 'rgba(244,67,54,0.7)' : (c.kind === KIND.WALL ? 'rgba(255,183,77,0.9)' : 'rgba(100,181,246,0.7)');
                ctx.fillRect(p.x + 2, p.y + 2, cs - 4, cs - 4);
            });
            ctx.globalAlpha = 1;
        }
    }

    function normRect(a, b) {
        return { x0: Math.min(a.x, b.x), x1: Math.max(a.x, b.x), y0: Math.min(a.y, b.y), y1: Math.max(a.y, b.y) };
    }

    // ---- Architecture : couche dynamique (sélection, aperçu) ---------------------------------------------

    function renderDynamicArch(st, ctx, cs) {
        const vr = viewRect(st), plan = st.plan;
        ctx.save();
        ctx.beginPath(); ctx.rect(vr.x, vr.y, vr.w, vr.h); ctx.clip();

        drawMaterialHighlight(st, ctx, cs);

        // Forme sélectionnée : ses cellules sur la couche en pointillé, sa boîte englobante en trait fin.
        if (st.selection && st.selection.kind === 'op') {
            const op = findOp(st, st.selection.id);
            if (op) {
                ctx.strokeStyle = st.palette.secondary; ctx.lineWidth = 2; ctx.setLineDash([4, 3]);
                fillMask(st, ctx, opLayerMask(op, st.layer, plan.grid.width, plan.grid.depth), cs, null, true);
                ctx.setLineDash([]);
                if (op.kind !== 'cells') {
                    const b = opBounds(op), p = toScreen(st, b.x0, b.y0);
                    ctx.lineWidth = 1; ctx.strokeStyle = 'rgba(255,183,77,0.6)';
                    ctx.strokeRect(p.x + 0.5, p.y + 0.5, (b.x1 - b.x0 + 1) * cs - 1, (b.y1 - b.y0 + 1) * cs - 1);
                }
                if (op.kind === 'curve' && validOp(op)) drawCurveGuides(st, ctx, cs, op);
            }
        }
        if (st.pending && !st.pending.side) {
            // Courbe en attente de son point de contrôle : ses cellules sur la couche, puis les guides.
            const op = st.pending.op;
            fillMask(st, ctx, opLayerMask(op, st.layer, plan.grid.width, plan.grid.depth), cs, op.subtract ? 'rgba(255,152,0,0.45)' : 'rgba(100,181,246,0.45)', false);
            drawCurveGuides(st, ctx, cs, op);
        }
        drawRoomLabels(st, ctx, cs);
        drawPreviewArch(st, ctx, cs);
        ctx.restore();

        // Cubes ou forme en cours de pose dans la coupe (posés au relâchement).
        const d = st.drag;
        if (d && d.kind === 'sideDraw' && d.cellsView) {
            const g = d.geom;
            ctx.fillStyle = 'rgba(100,181,246,0.6)';
            d.cellsView.forEach(function (c) { ctx.fillRect(g.x0 + c.c * g.colW, g.bottom - (c.z + 1) * g.rowH, Math.max(1, g.colW - 0.5), Math.max(1, g.rowH - 0.5)); });
        } else if (d && d.kind === 'sideShape') {
            const g = d.geom, op = sideShapeOp(st, d, d.cur), mask = opPlaneMask(op, g), cols = g.view.cols;
            ctx.fillStyle = d.subtract ? 'rgba(255,152,0,0.45)' : 'rgba(100,181,246,0.45)';
            for (let i = 0; i < mask.length; i++) if (mask[i]) ctx.fillRect(g.x0 + (i % cols) * g.colW, g.bottom - (((i / cols) | 0) + 1) * g.rowH, Math.max(1, g.colW - 0.5), Math.max(1, g.rowH - 0.5));
            const label = opLabel(st, op), lx = g.x0 + (d.cur.c + 1) * g.colW + 4, ly = g.bottom - (d.cur.z + 1) * g.rowH;
            ctx.font = 'bold 11px sans-serif'; ctx.textAlign = 'left'; ctx.textBaseline = 'top';
            const tw = ctx.measureText(label).width;
            ctx.fillStyle = 'rgba(0,0,0,0.7)'; ctx.fillRect(lx - 2, ly - 2, tw + 8, 16);
            ctx.fillStyle = '#fff'; ctx.fillText(label, lx + 2, ly);
        } else if (st.pending && st.pending.side && st.cut && st.sectionGeom) {
            const g = st.sectionGeom, op = st.pending.op, mask = opPlaneMask(op, g), cols = g.view.cols;
            ctx.fillStyle = op.subtract ? 'rgba(255,152,0,0.45)' : 'rgba(100,181,246,0.45)';
            for (let i = 0; i < mask.length; i++) if (mask[i]) ctx.fillRect(g.x0 + (i % cols) * g.colW, g.bottom - (((i / cols) | 0) + 1) * g.rowH, Math.max(1, g.colW - 0.5), Math.max(1, g.rowH - 0.5));
            drawSideCurveGuides(st, ctx, g, op);
        }
        const curve = selectedCurve(st);
        if (curve && st.cut && st.sectionGeom) drawSideCurveGuides(st, ctx, st.sectionGeom, curve);

        drawLegend(st, ctx);
    }

    // Guides d'une courbe dans le plan : polygone de contrôle a–c–b en pointillé fin, poignée ronde sur c (tirable en sélection).
    function drawCurveGuides(st, ctx, cs, op) {
        const ctr = function (p) { const s = toScreen(st, p[0], p[1]); return { x: s.x + cs / 2, y: s.y + cs / 2 }; };
        const a = ctr(op.a), c = ctr(op.c), b = ctr(op.b);
        ctx.strokeStyle = st.palette.secondary; ctx.lineWidth = 1; ctx.setLineDash([3, 3]);
        ctx.beginPath(); ctx.moveTo(a.x, a.y); ctx.lineTo(c.x, c.y); ctx.lineTo(b.x, b.y); ctx.stroke();
        ctx.setLineDash([]);
        drawHandle(st, ctx, c.x, c.y, Math.max(4, cs * 0.3));
    }
    // Dans la coupe : la poignée seulement, et seulement si c est sur le plan de coupe.
    function drawSideCurveGuides(st, ctx, g, op) {
        const h = sideHandleCell(op, g);
        if (h) drawHandle(st, ctx, g.x0 + (h.c + 0.5) * g.colW, g.bottom - (h.z + 0.5) * g.rowH, Math.max(4, Math.min(g.colW, g.rowH) * 0.3));
    }
    function drawHandle(st, ctx, x, y, r) {
        ctx.beginPath(); ctx.arc(x, y, r, 0, Math.PI * 2);
        ctx.fillStyle = st.palette.secondary; ctx.fill();
        ctx.lineWidth = 1.5; ctx.strokeStyle = '#fff'; ctx.stroke();
    }

    function fillMask(st, ctx, mask, cs, fill, stroke) {
        const W = st.plan.grid.width;
        for (let i = 0; i < mask.length; i++) {
            if (!mask[i]) continue;
            const p = toScreen(st, i % W, (i / W) | 0);
            if (fill) { ctx.fillStyle = fill; ctx.fillRect(p.x + 1, p.y + 1, cs - 2, cs - 2); }
            if (stroke) ctx.strokeRect(p.x + 1, p.y + 1, cs - 2, cs - 2);
        }
    }

    // Aperçu : la forme telle qu'elle serait posée (cellules de la couche + boîte englobante), bleu = ajout, orange = soustraction.
    function drawPreviewArch(st, ctx, cs) {
        if (!st.hover) return;
        const W = st.plan.grid.width, D = st.plan.grid.depth;
        const drag = st.drag;
        if (drag && drag.kind === 'shape') {
            const op = shapeOpFromDrag(st, drag, st.hover);
            const color = drag.subtract ? 'rgba(255,152,0,0.45)' : 'rgba(100,181,246,0.45)';
            fillMask(st, ctx, opLayerMask(op, st.layer, W, D), cs, color, false);
            const b = opBounds(op), p = toScreen(st, b.x0, b.y0);
            ctx.strokeStyle = drag.subtract ? st.palette.warning : st.palette.primary; ctx.lineWidth = 1.5; ctx.setLineDash([5, 3]);
            ctx.strokeRect(p.x + 0.5, p.y + 0.5, (b.x1 - b.x0 + 1) * cs - 1, (b.y1 - b.y0 + 1) * cs - 1);
            ctx.setLineDash([]);
            const label = opLabel(st, op), hp = toScreen(st, st.hover.x + 1, st.hover.y);
            ctx.font = 'bold 11px sans-serif'; ctx.textAlign = 'left'; ctx.textBaseline = 'top';
            const tw = ctx.measureText(label).width;
            ctx.fillStyle = 'rgba(0,0,0,0.7)'; ctx.fillRect(hp.x + 2, hp.y - 2, tw + 8, 16);
            ctx.fillStyle = '#fff'; ctx.fillText(label, hp.x + 6, hp.y);
            return;
        }
        if (drag && drag.kind === 'cells') {
            ctx.fillStyle = 'rgba(100,181,246,0.5)';
            drag.cells.forEach(function (i) { const p = toScreen(st, i % W, (i / W) | 0); ctx.fillRect(p.x + 1, p.y + 1, cs - 2, cs - 2); });
            return;
        }
        if (drag && drag.kind === 'eraseRect') {
            const r = normRect(drag.start, st.hover);
            ctx.fillStyle = 'rgba(244,67,54,0.25)';
            for (let x = r.x0; x <= r.x1; x++) for (let y = r.y0; y <= r.y1; y++) { const p = toScreen(st, x, y); ctx.fillRect(p.x, p.y, cs, cs); }
            return;
        }
        if (drag && drag.kind === 'erase') return;
        if (drag && drag.kind === 'cut') {
            const a = drag.start, b = st.hover, horizontal = Math.abs(b.x - a.x) >= Math.abs(b.y - a.y);
            const origin = toScreen(st, 0, 0);
            ctx.strokeStyle = st.palette.primary; ctx.lineWidth = 2; ctx.setLineDash([8, 5]);
            ctx.beginPath();
            if (horizontal) { const py = origin.y + (a.y + 0.5) * cs; ctx.moveTo(origin.x - 12, py); ctx.lineTo(origin.x + W * cs + 12, py); }
            else { const px = origin.x + (a.x + 0.5) * cs; ctx.moveTo(px, origin.y - 12); ctx.lineTo(px, origin.y + D * cs + 12); }
            ctx.stroke(); ctx.setLineDash([]);
            return;
        }
        if (!inGrid(st, st.hover.x, st.hover.y)) return;
        if (st.tool !== 'select' && st.tool !== 'pan') {
            const p = toScreen(st, st.hover.x, st.hover.y);
            ctx.strokeStyle = st.tool === 'eraser' ? st.palette.warning : st.palette.primary; ctx.lineWidth = 2;
            ctx.strokeRect(p.x + 1, p.y + 1, cs - 2, cs - 2);
        }
    }

    // ---- Interaction ----------------------------------------------------------------------------------

    function bindEvents(st) {
        const c = st.dynamicCanvas;
        c.addEventListener('pointerdown', function (e) { onPointerDown(st, e); });
        c.addEventListener('pointermove', function (e) { onPointerMove(st, e); });
        c.addEventListener('pointerup', function (e) { onPointerUp(st, e); });
        c.addEventListener('pointercancel', function (e) { st.drag = null; requestRender(st); });
        c.addEventListener('pointerenter', function () { st.pointerOver = true; });
        c.addEventListener('pointerleave', function () { st.pointerOver = false; st.hover = null; requestRender(st); });
        c.addEventListener('wheel', function (e) { onWheel(st, e); }, { passive: false });
        c.addEventListener('contextmenu', function (e) { e.preventDefault(); });
        // Raccourcis au niveau document : actifs dès que la souris est sur le canvas (ou qu'il a le focus),
        // sauf si la frappe vise un champ éditable ou un dialog/popover MudBlazor.
        st.keyHandler = function (e) {
            if (!st.pointerOver && document.activeElement !== c) return;
            const t = e.target;
            if (t !== c && t instanceof Element &&
                t.closest('input, textarea, select, [contenteditable]:not([contenteditable="false"]), .mud-dialog, .mud-popover')) return;
            onKeyDown(st, e);
        };
        document.addEventListener('keydown', st.keyHandler);
        c.addEventListener('dblclick', function (e) {
            const rect = c.getBoundingClientRect();
            if (st.view3d.on) {
                // Double-clic en 3D : recentre sur le point visé, sans reculer ; la coupe face caméra y passe aussi.
                const pick = view3dPick(st, e.clientX - rect.left, e.clientY - rect.top), v = st.view3d;
                if (!pick) return;
                const eye = v3dBasis(v).eye;
                v.dist = Math.max(2, Math.min(v.dist, Math.hypot(pick.p[0] - eye[0], pick.p[1] - eye[1], pick.p[2] - eye[2])));
                v.target = pick.p; v.cutAt = pick.p.slice(); requestRender(st);
                return;
            }
            if (isArch(st)) return;
            const cell = toCell(st, e.clientX - rect.left, e.clientY - rect.top);
            const o = objectAt(st, cell.x, cell.y);
            const rid = roomAt(st, cell.x, cell.y);
            if (o) select(st, 'object', o.id); else if (rid) select(st, 'room', rid);
        });
    }

    function pointerCell(st, e) {
        const rect = st.dynamicCanvas.getBoundingClientRect();
        return { px: e.clientX - rect.left, py: e.clientY - rect.top, cell: toCell(st, e.clientX - rect.left, e.clientY - rect.top) };
    }

    function inRect(r, px, py) { return !!r && px >= r.x && px < r.x + r.w && py >= r.y && py < r.y + r.h; }
    function stripLayerAt(st, py) {
        const g = st.stripGeom; if (!g) return st.layer;
        return Math.floor((g.bottom - py) / g.rowH);
    }

    // Clic ou glisser dans la bande : couche en architecture / 3D, niveau contenant la couche visée en mode maison.
    function stripSetFromY(st, py) {
        const z = stripLayerAt(st, py);
        if (isArch(st) || st.view3d.on) setLayerInternal(st, z);
        else setLevelInternal(st, levelIndexAtY(st, Math.max(0, z)));
    }

    // Le pointeur est-il sur le trait de coupe (à 6 px près) ?
    function onCutLine(st, px, py) {
        const l = st.cutLine; if (!l) return false;
        return l.axis === 'x' ? Math.abs(px - l.at) <= 6 && py >= l.from && py <= l.to : Math.abs(py - l.at) <= 6 && px >= l.from && px <= l.to;
    }

    // Bande d'élévation (architecture et 3D) : flèches de l'en-tête, sinon clic/glisser = couche. True si consommé.
    function stripPointerDown(st, e, pc, b) {
        if (!inRect(b.strip, pc.px, pc.py)) return false;
        if (e.button !== 0) return true;
        // Flèches de l'en-tête : tourner la face regardée (sud → est → nord → ouest, ou l'inverse).
        for (const a of st.elevArrows || []) {
            if (Math.hypot(pc.px - a.x, pc.py - a.y) <= a.r) { st.elevSide = ELEV_ORDER[(ELEV_ORDER.indexOf(st.elevSide) + a.delta + 4) % 4]; st.staticDirty = true; requestRender(st); return true; }
        }
        if (pc.py < b.strip.y + 26) return true;   // reste de l'en-tête : rien
        st.drag = { kind: 'layer' }; stripSetFromY(st, pc.py);
        return true;
    }

    // Rendu 3D : bande d'élévation, sinon glisser gauche = orbite (clic sans bouger : sélection au relâché), droit ou milieu =
    // déplacer la cible. L'orbite tourne autour du point du bloc visé (sinon autour de la cible), le déplacement suit la
    // profondeur du point visé ; la cible n'est pas déplacée au clic.
    function onPointerDown3d(st, e, pc) {
        if (stripPointerDown(st, e, pc, bands(st))) return;
        if (!inRect(viewRect(st), pc.px, pc.py)) return;
        const v = st.view3d, pick = view3dPick(st, pc.px, pc.py), b = v3dBasis(v);
        const depth = pick ? (pick.p[0] - b.eye[0]) * b.fwd[0] + (pick.p[1] - b.eye[1]) * b.fwd[1] + (pick.p[2] - b.eye[2]) * b.fwd[2] : 0;
        st.drag = { kind: e.button === 0 ? 'orbit' : 'pan3d', px: pc.px, py: pc.py, sx: pc.px, sy: pc.py, moved: false, hit: pick && pick.hit,
            pivot: pick && pick.hit ? pick.p : null, depth: depth >= 2 && depth <= 2000 ? depth : v.dist };
    }

    // Clic en 3D : objet touché, sinon le volume visible qui a posé le bloc (jamais une soustraction), sinon rien.
    function select3d(st, hit) {
        if (hit && hit.object) { select(st, 'object', hit.object.id); return; }
        const op = hit ? ownerAt(st, hit.x, hit.y, hit.z) : null;
        if (op && !op.subtract) select(st, 'op', op.id); else select(st, null, null);
    }

    // Architecture : bandes et yeux d'abord, puis outils de forme. Renvoie true si l'événement est consommé.
    function onPointerDownArch(st, e, pc) {
        const cell = pc.cell, b = bands(st);
        if (st.pending) {
            // Courbe en attente : clic gauche = poser avec le point de contrôle courant, clic droit = abandonner.
            const p = st.pending; st.pending = null;
            if (e.button === 0) addOp(st, p.op); else requestRender(st);
            return true;
        }
        for (const eye of st.cutEyes) {
            if (Math.hypot(pc.px - eye.x, pc.py - eye.y) <= eye.r) {
                if (st.cut.dir === eye.dir) st.cut = null; else st.cut.dir = eye.dir;   // re-clic sur l'œil actif : fin de la coupe
                st.staticDirty = true; fit(st);
                return true;
            }
        }
        if (e.button === 0 && onCutLine(st, pc.px, pc.py)) { st.drag = { kind: 'cutMove' }; return true; }   // glisser le trait
        if (stripPointerDown(st, e, pc, b)) return true;
        const pan = e.button === 1 || st.tool === 'pan' || (e.button === 0 && e.altKey);
        if (inRect(b.section, pc.px, pc.py)) {
            // Déplacement de la vue depuis la coupe ; clic milieu sans bouger : pipette sur le bloc visible de la case
            // (même cible que la sélection et la gomme de la coupe, voir onPointerUp).
            if (pan) {
                const g = st.sectionGeom, sc = e.button === 1 ? sideCellAt(g, pc.px, pc.py) : null;
                st.drag = { kind: 'pan', startPx: pc.px, startPy: pc.py, ox: st.view.ox, oy: st.view.oy, pick: sc ? sideTarget(g, sc.c, sc.z, true) : null };
            }
            else startSideDrag(st, e, pc, st.sectionGeom);
            return true;
        }
        if (pan) return false;   // déplacement de la vue : branche commune

        if (e.button === 2) {
            // Clic droit maintenu : gomme au passage sur la couche ; Maj+clic droit : gomme rectangulaire.
            if (e.shiftKey) st.drag = { kind: 'eraseRect', start: cell };
            else { st.drag = newEraseDrag(cell); eraseAt(st, st.drag, cell.x, cell.y, st.layer); if (st.drag.dirtyVox) { refreshVox(st); st.drag.dirtyVox = false; } }
            st.staticDirty = true; requestRender(st);
            return true;
        }
        if (e.button !== 0) return true;
        const subtract = e.shiftKey ? !st.shape.subtract : !!st.shape.subtract;   // Maj capturé au pointerdown
        switch (st.tool) {
            case 'select': {
                const handle = selectedCurve(st);
                if (handle && handle.c[0] === cell.x && handle.c[1] === cell.y) { st.drag = { kind: 'curveHandle', id: handle.id, moved: false }; break; }
                const op = ownerAt(st, cell.x, cell.y, st.layer);
                if (op) { select(st, 'op', op.id); st.drag = { kind: 'moveOp', id: op.id, last: cell, moved: false }; }
                else select(st, null, null);
                break;
            }
            case 'box': case 'sphere': case 'cylinder': case 'disc': case 'line': case 'curve':
                if (!subtract && !st.material) break;
                st.drag = { kind: 'shape', tool: st.tool, start: cell, subtract: subtract };
                break;
            case 'room':   // pièce : toujours un ajout
                if (st.material) st.drag = { kind: 'shape', tool: 'room', start: cell, subtract: false };
                break;
            case 'pencil': case 'eraser': {
                const sub = st.tool === 'eraser' ? !e.shiftKey : subtract;
                if (sub) { st.drag = newEraseDrag(cell); eraseAt(st, st.drag, cell.x, cell.y, st.layer); if (st.drag.dirtyVox) { refreshVox(st); st.drag.dirtyVox = false; } st.staticDirty = true; break; }
                if (!st.material) break;
                st.drag = { kind: 'cells', subtract: false, cells: new Set(), last: cell };
                brushCell(st, st.drag, cell.x, cell.y);
                break;
            }
            case 'cut':
                st.drag = { kind: 'cut', start: cell };
                break;
        }
        requestRender(st);
        return true;
    }

    // Coupe : le clic gauche suit l'outil comme sur le plan — sélection = volume visible sous la case (bloc coupé, sinon
    // premier bloc au-delà), gomme = efface ce bloc, formes = tracé dans le plan de coupe, crayon = cubes du matériau
    // courant (ajout / soustraction et Maj comme sur le plan), coupe : rien. Clic droit : efface le bloc visible ;
    // maintien = trait. Les cubes posés forment une op « cells » au relâchement.
    function startSideDrag(st, e, pc, g) {
        const cell = sideCellAt(g, pc.px, pc.py);
        if (!g || !cell) return;
        const shapeTool = st.tool === 'box' || st.tool === 'sphere' || st.tool === 'cylinder' || st.tool === 'disc' || st.tool === 'line' || st.tool === 'curve' || st.tool === 'room';
        const subtract = st.tool !== 'room' && (e.shiftKey ? !st.shape.subtract : !!st.shape.subtract);   // pièce : toujours un ajout
        const erase = function () { st.drag = Object.assign(newEraseDrag(null), { kind: 'sideErase', geom: g, last: cell }); sideBrush(st, st.drag, cell); };
        const handle = st.tool === 'select' ? selectedCurve(st) : null, hc = handle ? sideHandleCell(handle, g) : null;
        if (e.button === 0 && hc && hc.c === cell.c && hc.z === cell.z) st.drag = { kind: 'sideCurveHandle', id: handle.id, geom: g, moved: false };
        else if (e.button === 2) erase();
        else if (e.button === 0 && st.tool === 'select') {
            const p = sideTarget(g, cell.c, cell.z, true), op = p ? ownerAt(st, p.x, p.y, p.z) : null;
            if (op) select(st, 'op', op.id); else select(st, null, null);
        }
        else if (e.button === 0 && shapeTool && (subtract || st.material)) st.drag = { kind: 'sideShape', tool: st.tool, geom: g, start: cell, cur: cell, subtract: subtract };
        else if (e.button === 0 && (st.tool === 'pencil' || st.tool === 'eraser')) {
            if (st.tool === 'eraser' ? !e.shiftKey : subtract) erase();
            else if (st.material) { st.drag = { kind: 'sideDraw', geom: g, cells: {}, last: cell }; sideBrush(st, st.drag, cell); }
        }
        requestRender(st);
    }

    function sideBrush(st, drag, cell) {
        const g = drag.geom;
        if (drag.kind === 'sideErase') {
            const p = sideTarget(g, cell.c, cell.z, true);
            if (p) { eraseAt(st, drag, p.x, p.y, p.z); if (drag.dirtyVox) { refreshVox(st); drag.dirtyVox = false; } if (drag.changed) st.staticDirty = true; }
            return;
        }
        const p = sideTarget(g, cell.c, cell.z, false);
        if (p && !voxAt(st.vox, p.x, p.y, p.z)) { drag.cells[p.x + ',' + p.y + ',' + p.z] = p; drag.cellsView = drag.cellsView || []; drag.cellsView.push(cell); }
    }

    // Courbe sélectionnée (son point de contrôle se tire à la souris), sinon null.
    function selectedCurve(st) {
        const op = st.selection && st.selection.kind === 'op' ? findOp(st, st.selection.id) : null;
        return op && op.kind === 'curve' && validOp(op) ? op : null;
    }
    // Cellule (colonne, couche) du point de contrôle dans la coupe, ou null s'il n'est pas sur le plan de coupe.
    function sideHandleCell(op, g) {
        const ax = g.axis === 'x' ? 0 : 1, au = 1 - ax, c = op.c;
        if (c[ax] !== g.index || c[au] < 0 || c[au] >= g.view.cols || c[2] < 0 || c[2] >= g.view.rows) return null;
        return { c: sideCol(g, c[au]), z: c[2] };
    }
    // Point du plan visé par la cellule (colonne, couche) de la coupe.
    function sidePoint(g, c, z) { const u = sideCol(g, c); return g.axis === 'x' ? [g.index, u, z] : [u, g.index, z]; }
    // Courbe en attente : le point de contrôle suit la souris (dans la coupe si elle y est née, sinon sur sa couche).
    function trackPending(st, pc) {
        const p = st.pending, op = p.op;
        if (p.side) {
            const g = st.sectionGeom, sec = bands(st).section;
            if (!g || !inRect(sec, pc.px, pc.py)) return;
            const cell = sideCellClamped(g, pc.px, pc.py);
            op.c = sidePoint(g, cell.c, cell.z);
        } else if (inRect(viewRect(st), pc.px, pc.py)) op.c = [pc.cell.x, pc.cell.y, op.a[2]];
    }
    // Déplacement du point de contrôle d'une courbe posée (première modification : entrée d'historique).
    function moveHandle(st, drag, c) {
        const op = findOp(st, drag.id);
        if (!op || (op.c[0] === c[0] && op.c[1] === c[1] && op.c[2] === c[2])) return;
        if (!drag.moved) { pushHistory(st); drag.moved = true; }
        op.c = c;
        refreshVox(st); st.staticDirty = true;
    }

    // Trait crayon (ajout) : une cellule de la couche par passage.
    function brushCell(st, drag, x, y) {
        if (!inGrid(st, x, y)) return;
        drag.cells.add(x + st.plan.grid.width * y);
    }

    function onPointerDown(st, e) {
        st.dynamicCanvas.focus({ preventScroll: true });
        const pc = pointerCell(st, e);
        const cell = pc.cell;
        st.dynamicCanvas.setPointerCapture(e.pointerId);

        // Chevron de la légende : avant tout le reste, dans les trois modes.
        if (e.button === 0 && inRect(st.legendHit, pc.px, pc.py)) { st.legend = !st.legend; requestRender(st); return; }
        if (st.view3d.on) { onPointerDown3d(st, e, pc); return; }
        if (isArch(st) && onPointerDownArch(st, e, pc)) return;
        if (!isArch(st) && stripPointerDown(st, e, pc, bands(st))) return;

        if (e.button === 1 || st.tool === 'pan' || (e.button === 0 && e.altKey)) {
            // Clic milieu relâché sans bouger : pipette (voir onPointerUp) ; glissé : déplacement de la vue.
            st.drag = { kind: 'pan', startPx: pc.px, startPy: pc.py, ox: st.view.ox, oy: st.view.oy, pick: e.button === 1 ? cell : null };
            return;
        }
        if (e.button === 2) {
            // Clic droit maintenu : gomme au passage quel que soit l'outil ; Maj+clic droit : gomme rectangulaire.
            if (e.shiftKey) { st.drag = { kind: 'rect', start: cell }; return; }
            st.drag = { kind: 'eraseBrush', pushed: false, changed: false, last: cell };
            brushErase(st, st.drag, cell.x, cell.y);
            requestRender(st);
            return;
        }
        if (e.button !== 0) return;

        switch (st.tool) {
            case 'select': {
                const o = objectAt(st, cell.x, cell.y);
                if (o) { select(st, 'object', o.id); st.drag = { kind: 'moveObject', id: o.id, start: cell, orig: { x: o.x, y: o.y }, moved: false }; }
                else {
                    const rid = roomAt(st, cell.x, cell.y);
                    if (rid) select(st, 'room', rid); else select(st, null, null);
                }
                break;
            }
            case 'object':
                if (st.objectType && inGrid(st, cell.x, cell.y)) {
                    addObject(st, cell.x, cell.y);
                    // Maj+clic : pose en série ; clic simple : on repose l'outil et on rend la main.
                    if (!e.shiftKey) { st.objectType = null; notifyObjectType(st); setTool(st, 'select'); }
                }
                break;
        }
        requestRender(st);
    }

    function onPointerMove(st, e) {
        const pc = pointerCell(st, e);
        st.hover = pc.cell;
        if (st.view3d.on) {
            const v = st.view3d, d = st.drag;
            st.dynamicCanvas.style.cursor = inRect(viewRect(st), pc.px, pc.py) ? (d ? 'grabbing' : 'grab') : 'default';
            if (d && d.sx !== undefined && !d.moved && Math.hypot(pc.px - d.sx, pc.py - d.sy) >= 4) d.moved = true;
            if (d && d.kind === 'orbit') {
                v3dOrbit(v, d.pivot || v.target.slice(), -(pc.px - d.px) * 0.01, (pc.py - d.py) * 0.01);
                d.px = pc.px; d.py = pc.py; requestRender(st);
            } else if (d && d.kind === 'pan3d') {
                // Déplacement de la cible dans le plan de l'écran : unités monde par pixel à la profondeur du point visé.
                const vr = viewRect(st), k = d.depth * 2 * Math.tan(Math.PI / 8) / Math.max(1, vr.h);
                // Repère plan : droite écran = (cos yaw, −sin yaw, 0), haut écran = (−sp·sin yaw, −sp·cos yaw, cp).
                const cy = Math.cos(v.yaw), sy = Math.sin(v.yaw), sp = Math.sin(v.pitch), cp = Math.cos(v.pitch);
                const dx = (pc.px - d.px) * k, dy = (pc.py - d.py) * k;
                v.target[0] += -dx * cy - dy * sp * sy; v.target[1] += dx * sy - dy * sp * cy; v.target[2] += dy * cp;
                d.px = pc.px; d.py = pc.py; requestRender(st);
            } else if (d && d.kind === 'layer') {
                stripSetFromY(st, pc.py);
            }
            return;
        }
        if (st.pending) { trackPending(st, pc); requestRender(st); return; }
        if (!st.drag && isArch(st) && st.cut) {
            // Curseur de déplacement au survol du trait de coupe, sinon celui de l'outil.
            const on = onCutLine(st, pc.px, pc.py);
            st.dynamicCanvas.style.cursor = on ? (st.cut.axis === 'x' ? 'ew-resize' : 'ns-resize') : (st.tool === 'pan' ? 'grab' : st.tool === 'select' ? 'default' : 'crosshair');
        }
        if (st.drag) {
            if (st.drag.kind === 'pan') {
                st.view.ox = st.drag.ox + (pc.px - st.drag.startPx);
                st.view.oy = st.drag.oy + (pc.py - st.drag.startPy);
                st.staticDirty = true;
            } else if (st.drag.kind === 'moveObject') {
                const f = findObject(st, st.drag.id);
                if (f) {
                    const o = f.item;
                    const nx = st.drag.orig.x + (pc.cell.x - st.drag.start.x), ny = st.drag.orig.y + (pc.cell.y - st.drag.start.y);
                    if (nx !== o.x || ny !== o.y) {
                        if (!st.drag.moved) { pushHistory(st); st.drag.moved = true; }
                        shiftObject(st.plan.levels[f.level].objects, o.id, nx - o.x, ny - o.y);
                    }
                }
            } else if (st.drag.kind === 'eraseBrush' || st.drag.kind === 'cells' || st.drag.kind === 'erase') {
                // Interpole entre la dernière cellule et la courante pour ne rien sauter quand le curseur va vite.
                const drag = st.drag;
                const brush = drag.kind === 'cells' ? brushCell : drag.kind === 'erase' ? function (s, d, x, y) { eraseAt(s, d, x, y, s.layer); } : brushErase;
                let x = drag.last.x, y = drag.last.y;
                while (x !== pc.cell.x || y !== pc.cell.y) {
                    if (x !== pc.cell.x) x += pc.cell.x > x ? 1 : -1;
                    if (y !== pc.cell.y) y += pc.cell.y > y ? 1 : -1;
                    brush(st, drag, x, y);
                }
                drag.last = pc.cell;
                if (drag.dirtyVox) { refreshVox(st); drag.dirtyVox = false; st.staticDirty = true; }
            } else if (st.drag.kind === 'layer') {
                stripSetFromY(st, pc.py);
            } else if (st.drag.kind === 'sideShape') {
                st.drag.cur = sideCellClamped(st.drag.geom, pc.px, pc.py);
            } else if (st.drag.kind === 'curveHandle') {
                const op = findOp(st, st.drag.id);
                if (op) moveHandle(st, st.drag, [pc.cell.x, pc.cell.y, op.c[2]]);
            } else if (st.drag.kind === 'sideCurveHandle') {
                const g = st.drag.geom, cell = sideCellAt(g, pc.px, pc.py);
                if (cell) moveHandle(st, st.drag, sidePoint(g, cell.c, cell.z));
            } else if (st.drag.kind === 'sideDraw' || st.drag.kind === 'sideErase') {
                // Interpolation dans la grille de la vue (colonne, couche), comme le trait du plan.
                const drag = st.drag, cell = sideCellAt(drag.geom, pc.px, pc.py);
                if (cell) {
                    let c = drag.last.c, z = drag.last.z;
                    while (c !== cell.c || z !== cell.z) {
                        if (c !== cell.c) c += cell.c > c ? 1 : -1;
                        if (z !== cell.z) z += cell.z > z ? 1 : -1;
                        sideBrush(st, drag, { c: c, z: z });
                    }
                    drag.last = cell;
                }
            } else if (st.drag.kind === 'cutMove' && st.cut) {
                const max = (st.cut.axis === 'x' ? st.plan.grid.width : st.plan.grid.depth) - 1;
                const next = Math.max(0, Math.min(max, st.cut.axis === 'x' ? pc.cell.x : pc.cell.y));
                if (next !== st.cut.index) { st.cut.index = next; st.staticDirty = true; }
            } else if (st.drag.kind === 'moveOp') {
                const op = findOp(st, st.drag.id);
                const dx = pc.cell.x - st.drag.last.x, dy = pc.cell.y - st.drag.last.y;
                if (op && (dx || dy)) {
                    if (!st.drag.moved) { pushHistory(st); st.drag.moved = true; }
                    translateOp(op, dx, dy);
                    st.drag.last = pc.cell;
                    refreshVox(st); st.staticDirty = true;
                }
            }
        }
        requestRender(st);
    }

    function onPointerUpArch(st, e, pc, drag) {
        if (drag.kind === 'shape') {
            const op = shapeOpFromDrag(st, drag, pc.cell);
            if (drag.tool === 'curve') st.pending = { op: op, side: false }; else addOp(st, op);
        } else if (drag.kind === 'sideShape' && drag.tool === 'curve') {
            st.pending = { op: sideShapeOp(st, drag, drag.cur), side: true };
        } else if (drag.kind === 'curveHandle' || drag.kind === 'sideCurveHandle') {
            if (drag.moved) commit(st, 'move');
        } else if (drag.kind === 'cells') {
            if (drag.cells.size) {
                const W = st.plan.grid.width, cells = [];
                drag.cells.forEach(function (i) { cells.push(i % W, (i / W) | 0, st.layer); });
                addOp(st, applyForm({ kind: 'cells', subtract: false, material: st.material, cells: cells }, st.form, st.formRot));
            }
        } else if (drag.kind === 'sideShape') {
            addOp(st, sideShapeOp(st, drag, drag.cur));
        } else if (drag.kind === 'sideDraw') {
            const cells = [];
            for (const k in drag.cells) { const p = drag.cells[k]; cells.push(p.x, p.y, p.z); }
            if (cells.length) addOp(st, applyForm({ kind: 'cells', subtract: false, material: st.material, cells: cells }, st.form, st.formRot), false);
        } else if (drag.kind === 'sideErase') {
            finishErase(st, drag);
        } else if (drag.kind === 'erase') {
            if (!finishErase(st, drag) && e.button === 2 && st.tool !== 'select') {
                // Clic droit dans le vide avec un outil actif : même effet qu'Échap.
                setTool(st, 'select');
                select(st, null, null);
            }
        } else if (drag.kind === 'eraseRect') {
            const r = normRect(drag.start, pc.cell), d = newEraseDrag(drag.start);
            for (let x = Math.max(0, r.x0); x <= Math.min(st.plan.grid.width - 1, r.x1); x++)
                for (let y = Math.max(0, r.y0); y <= Math.min(st.plan.grid.depth - 1, r.y1); y++) eraseAt(st, d, x, y, st.layer);
            if (d.dirtyVox) refreshVox(st);
            finishErase(st, d);
            st.staticDirty = true;
        } else if (drag.kind === 'moveOp' && drag.moved) {
            commit(st, 'move');
        } else if (drag.kind === 'cut') {
            const a = drag.start, b = pc.cell, horizontal = Math.abs(b.x - a.x) >= Math.abs(b.y - a.y);
            const W = st.plan.grid.width, D = st.plan.grid.depth;
            st.cut = horizontal ? { axis: 'y', index: Math.max(0, Math.min(D - 1, a.y)), dir: 1 } : { axis: 'x', index: Math.max(0, Math.min(W - 1, a.x)), dir: 1 };
            st.staticDirty = true;
            fit(st);
        }
    }

    function onPointerUp(st, e) {
        const pc = pointerCell(st, e);
        const drag = st.drag;
        st.drag = null;
        if (!drag) return;
        if (st.view3d.on) {   // orbite / déplacement / couche : rien à valider ; clic gauche sans bouger : sélection
            if (drag.kind === 'orbit' && !drag.moved) select3d(st, drag.hit);
            requestRender(st); return;
        }
        if (drag.kind === 'pan') {
            if (drag.pick && Math.abs(pc.px - drag.startPx) < 4 && Math.abs(pc.py - drag.startPy) < 4) pickAt(st, drag.pick.x, drag.pick.y, drag.pick.z);
        } else if (isArch(st)) {
            onPointerUpArch(st, e, pc, drag);
        } else if (drag.kind === 'rect') {
            applyRect(st, normRect(drag.start, pc.cell));
        } else if (drag.kind === 'moveObject' && drag.moved) {
            commit(st, 'move');
        } else if (drag.kind === 'eraseBrush' && drag.changed) {
            finishErase(st, drag);
        } else if (drag.kind === 'eraseBrush' && (st.tool !== 'select' || st.objectType)) {
            // Clic droit dans le vide avec un outil actif : même effet qu'Échap.
            if (st.objectType) { st.objectType = null; notifyObjectType(st); }
            setTool(st, 'select');
            select(st, null, null);
        }
        requestRender(st);
    }

    function onWheel(st, e) {
        e.preventDefault();
        const pc = pointerCell(st, e);
        const b = bands(st);
        if (inRect(b.strip, pc.px, pc.py)) {
            if (isArch(st) || st.view3d.on) setLayerInternal(st, st.layer + (e.deltaY < 0 ? 1 : -1));
            else setLevelInternal(st, st.level + (e.deltaY < 0 ? 1 : -1));
            return;
        }
        if (inRect(b.section, pc.px, pc.py)) return;
        if (st.view3d.on) {
            // Zoom vers le point sous le curseur (bloc, objet ou sol), sinon vers le pivot.
            const v = st.view3d, next = Math.max(2, Math.min(2000, v.dist * (e.deltaY < 0 ? 1 / 1.12 : 1.12))), pick = view3dPick(st, pc.px, pc.py);
            if (pick) v3dZoomAt(v, pick.p, next / v.dist); else v.dist = next;
            requestRender(st); return;
        }
        const factor = e.deltaY < 0 ? 1.12 : 1 / 1.12;
        zoomAt(st, factor, pc.px, pc.py);
    }

    function zoomAt(st, factor, px, py) {
        const old = st.view.scale;
        const next = Math.max(0.2, Math.min(4, old * factor));
        if (next === old) return;
        st.view.ox = px - (px - st.view.ox) * (next / old);
        st.view.oy = py - (py - st.view.oy) * (next / old);
        st.view.scale = next;
        st.staticDirty = true;
        requestRender(st);
    }

    function onKeyDown(st, e) {
        const ctrl = e.ctrlKey || e.metaKey;
        if (ctrl && e.key.toLowerCase() === 'z') { e.preventDefault(); if (e.shiftKey) redo(st); else undo(st); return; }
        if (ctrl && e.key.toLowerCase() === 'y') { e.preventDefault(); redo(st); return; }
        if (ctrl && e.key.toLowerCase() === 's') { e.preventDefault(); if (st.dotnetRef) st.dotnetRef.invokeMethodAsync('OnSaveRequested').catch(function () { }); return; }
        if (e.key === 'Delete' || e.key === 'Backspace') { e.preventDefault(); deleteSelection(st); return; }
        if (st.view3d.on) {
            // Échap vide d'abord la sélection, puis quitte la 3D.
            if (e.key === 'Escape') { e.preventDefault(); if (st.selection) select(st, null, null); else setView3dInternal(st, false); return; }
            if (e.key === 'PageUp' || e.key === 'PageDown') {   // sélection (Maj ou non) : elle monte / descend ; sinon la couche change
                e.preventDefault();
                const dz = e.key === 'PageUp' ? 1 : -1;
                if (!moveSelectedOp(st, 0, 0, dz) && !liftSelectedObject(st, dz)) setLayerInternal(st, st.layer + dz);
                return;
            }
            const nudge3d = cameraArrowStep(st.view3d.yaw, e.key);
            if (nudge3d && (moveSelectedOp(st, nudge3d[0], nudge3d[1], 0) || moveSelectedObject(st, nudge3d[0], nudge3d[1]))) { e.preventDefault(); return; }
            if (!ctrl && (e.code === 'KeyR' || e.code === 'KeyQ')) { e.preventDefault(); rotateCurrent(st, e.code === 'KeyQ' ? -1 : 1); return; }
            if (!ctrl && e.code === 'KeyC') { e.preventDefault(); setView3dCutawayInternal(st, !st.view3d.cutaway); return; }
            return;   // pas d'outils en 3D
        }
        if (e.key === 'Escape') { if (st.pending) { st.pending = null; requestRender(st); return; } if (st.objectType) { st.objectType = null; notifyObjectType(st); } setTool(st, 'select'); select(st, null, null); if (st.dotnetRef) st.dotnetRef.invokeMethodAsync('OnEscape').catch(function () { }); return; }
        const arch = isArch(st);
        if (arch && e.shiftKey && (e.key === 'PageUp' || e.key === 'PageDown') && moveSelectedOp(st, 0, 0, e.key === 'PageUp' ? 1 : -1)) { e.preventDefault(); return; }
        if (e.key === 'PageUp') { e.preventDefault(); if (arch) setLayerInternal(st, st.layer + 1); else setLevelInternal(st, st.level + 1); return; }
        if (e.key === 'PageDown') { e.preventDefault(); if (arch) setLayerInternal(st, st.layer - 1); else setLevelInternal(st, st.level - 1); return; }
        if (arch && e.key.toLowerCase() === 'i') { e.preventDefault(); st.bgVisible = !st.bgVisible; st.staticDirty = true; requestRender(st); return; }
        if (arch && ctrl && e.key.toLowerCase() === 'c') { if (copySelection(st, false)) e.preventDefault(); return; }
        if (arch && ctrl && e.key.toLowerCase() === 'x') { if (copySelection(st, true)) e.preventDefault(); return; }
        if (arch && ctrl && e.key.toLowerCase() === 'v') { if (pasteClipboard(st)) e.preventDefault(); return; }
        if (arch && st.selection && st.selection.kind === 'op') {
            const nudge = { ArrowLeft: [-1, 0, 0], ArrowRight: [1, 0, 0], ArrowUp: [0, -1, 0], ArrowDown: [0, 1, 0] }[e.key];
            if (nudge) { e.preventDefault(); moveSelectedOp(st, nudge[0], nudge[1], nudge[2]); return; }
        }
        if (!arch) {   // Aménagement : les flèches déplacent l'objet sélectionné d'une case
            const nudge = { ArrowLeft: [-1, 0], ArrowRight: [1, 0], ArrowUp: [0, -1], ArrowDown: [0, 1] }[e.key];
            if (nudge && moveSelectedObject(st, nudge[0], nudge[1])) { e.preventDefault(); return; }
        }
        // R / Q (A en AZERTY, lu sur e.code) : tourne la sélection si elle se tourne, sinon la rotation de pose.
        if (!ctrl && (e.code === 'KeyR' || e.code === 'KeyQ')) { e.preventDefault(); rotateCurrent(st, e.code === 'KeyQ' ? -1 : 1); return; }
        const tools = arch
            ? { '1': 'select', '2': 'box', '3': 'sphere', '4': 'cylinder', '5': 'disc', '6': 'line', '7': 'curve', '8': 'pencil', '9': 'eraser', '0': 'cut', 'p': 'room', 'h': 'pan' }
            : { '1': 'select', 'h': 'pan' };
        const t = tools[e.key.toLowerCase()];
        if (t) { setTool(st, t); }
    }

    // ---- Édition --------------------------------------------------------------------------------------

    // Gomme rectangulaire (Maj + clic droit, Aménagement) : les objets seulement.
    function applyRect(st, r) {
        const plan = st.plan;
        r.x0 = Math.max(0, r.x0); r.y0 = Math.max(0, r.y0);
        r.x1 = Math.min(plan.grid.width - 1, r.x1); r.y1 = Math.min(plan.grid.depth - 1, r.y1);
        if (r.x1 < r.x0 || r.y1 < r.y0) return;
        pushHistory(st);
        let changed = false;
        for (let x = r.x0; x <= r.x1; x++) for (let y = r.y0; y <= r.y1; y++) {
            const o = objectAt(st, x, y);
            if (o) { removeObject(st, o.id); changed = true; }
        }
        if (changed) commit(st, 'erase'); else { st.history.pop(); }
    }

    // Gomme d'une cellule pendant un drag au clic droit (Aménagement) : l'objet de la case, rien d'autre. L'historique
    // n'est poussé qu'au premier effacement du geste ; le commit arrive au pointerup.
    function brushErase(st, drag, x, y) {
        const o = objectAt(st, x, y);
        if (!o) return;
        if (!drag.pushed) { pushHistory(st); drag.pushed = true; }
        removeObject(st, o.id);
        drag.changed = true;
        st.staticDirty = true;
    }

    function removeObject(st, id) {
        const ids = new Set([id]);
        let grew = true;
        while (grew) {
            grew = false;
            st.plan.levels.forEach(function (level) {
                level.objects.forEach(function (o) { if (o.attachedTo && ids.has(o.attachedTo) && !ids.has(o.id)) { ids.add(o.id); grew = true; } });
            });
        }
        st.plan.levels.forEach(function (level) { level.objects = level.objects.filter(function (o) { return !ids.has(o.id); }); });
        if (st.selection && ids.has(st.selection.id)) select(st, null, null);
    }

    function addObject(st, x, y) {
        const info = st.objectsByName[st.objectType];
        const target = objectAt(st, x, y);
        const obj = { id: uid('o'), type: st.objectType, x: x, y: y, rotation: st.rotation & 3 };
        if (target && !target.attachedTo && info && info.canBeOnSurface && st.objectsByName[target.type] && st.objectsByName[target.type].hasTableSurface) {
            obj.attachedTo = target.id;
            obj.rotation = 0;
        }
        pushHistory(st);
        cur(st).objects.push(obj);
        commit(st, 'object');
        select(st, 'object', obj.id);
    }

    // Pipette : reprend le matériau de la couche (Structure ; z donné depuis la coupe) ou l'objet (type + rotation) sous
    // le curseur avec son outil.
    function pickAt(st, x, y, z) {
        if (isArch(st)) {
            const layer = z === undefined ? st.layer : z;
            const material = materialName(st, st.vox ? voxAt(st.vox, x, y, layer) : 0);
            if (!material) return;
            // Comme au marteau : matériau, forme et rotation du bloc visé.
            const d = decodeShape(st.vox.shapes[x + st.vox.W * (y + st.vox.D * layer)]);
            st.material = material; notifyMaterial(st);
            setFormInternal(st, d.form, d.rot, true);
            return;
        }
        const o = objectAt(st, x, y);
        if (o) {
            st.objectType = o.type; notifyObjectType(st);
            st.rotation = o.rotation || 0;
            if (st.dotnetRef) st.dotnetRef.invokeMethodAsync('OnRotationChanged', st.rotation).catch(function () { });
            setTool(st, 'object');
        }
    }

    // Même règle dans les deux modes : une sélection qui se tourne tourne (volume de forme tournable, objet non posé sur
    // un autre), sinon c'est la rotation de pose (forme de la palette si elle se tourne, objet à poser). En 3D, rien ne se pose :
    // seule la sélection tourne, volume ou objet quel que soit le mode.
    function rotateCurrent(st, dir) {
        const kind = st.selection && st.selection.kind, view3d = st.view3d.on;
        if (view3d ? kind === 'op' : isArch(st)) {
            const op = kind === 'op' ? findOp(st, st.selection.id) : null;
            if (op && !op.subtract && op.form && formRotatable(st, op.material, op.form)) { pushHistory(st); op.rot = rotStep(op.rot, dir); commit(st, 'rotate'); return; }
            if (!view3d && formRotatable(st, st.material, st.form)) setFormInternal(st, st.form, rotStep(st.formRot, dir), true);
            return;
        }
        if (kind === 'object') {
            const f = findObject(st, st.selection.id);
            if (f && !f.item.attachedTo) { pushHistory(st); f.item.rotation = rotStep(f.item.rotation, dir); commit(st, 'rotate'); return; }
        }
        if (view3d) return;
        st.rotation = rotStep(st.rotation, dir);
        if (st.dotnetRef) st.dotnetRef.invokeMethodAsync('OnRotationChanged', st.rotation).catch(function () { });
        requestRender(st);
    }

    function deleteSelection(st) {
        if (!st.selection) return;
        if (st.selection.kind === 'op') {
            const id = st.selection.id;
            pushHistory(st);
            st.plan.architecture.ops = st.plan.architecture.ops.filter(function (o) { return o.id !== id; });
            select(st, null, null);
            commit(st, 'delete');
            return;
        }
        // Les pièces sont auto-détectées : en supprimer une n'aurait aucun effet durable, on désélectionne.
        if (st.selection.kind !== 'object') { select(st, null, null); return; }
        pushHistory(st);
        removeObject(st, st.selection.id);
        select(st, null, null);
        commit(st, 'delete');
    }

    function select(st, kind, id) {
        const same = (st.selection === null && kind === null) || (st.selection && st.selection.kind === kind && st.selection.id === id);
        st.selection = kind ? { kind: kind, id: id } : null;
        if (!same) st.view3d.dirty = true;
        requestRender(st);
        if (!same) notifySelection(st);
    }

    function setTool(st, tool) {
        st.tool = tool;
        st.drag = null; st.pending = null;
        st.dynamicCanvas.style.cursor = tool === 'pan' ? 'grab' : tool === 'select' ? 'default' : 'crosshair';
        notifyTool(st);
        requestRender(st);
    }

    function restorePlan(st, json) {
        st.plan = normalizePlan(JSON.parse(json));
        clampLevel(st);
        st.selection = null; notifySelection(st);
        st.dirty = true; st.staticDirty = true;
        refreshVox(st); mapRoomsToPockets(st); recomputeFootprints(st); saveDraft(st); notifyPlan(st); requestRender(st);
    }

    // Remplace le plan (modification par l'IA) en une étape annulable, sans toucher à la vue ni à l'historique.
    function replacePlan(st, json) {
        pushHistory(st);
        st.plan = normalizePlan(JSON.parse(json));
        clampLevel(st);
        st.selection = null; notifySelection(st);
        commit(st, 'replace');
    }

    function undo(st) {
        if (!st.history.length) return;
        st.future.push(JSON.stringify(st.plan));
        restorePlan(st, st.history.pop());
    }

    function redo(st) {
        if (!st.future.length) return;
        st.history.push(JSON.stringify(st.plan));
        restorePlan(st, st.future.pop());
    }

    // Largeur à réserver à droite pour cadrer le plan comme si le volet de droite était ouvert (même fermé, il peut s'ouvrir
    // ou être restauré juste après) : --bp-panel-w du .bp-body + l'écart de la colonne ; 0 si un volet est déjà ouvert,
    // le conteneur étant alors déjà réduit d'autant.
    function reservedRight(st) {
        const body = st.container.closest('.bp-body');
        if (!body || body.querySelector('.bp-panel')) return 0;
        const v = parseFloat(getComputedStyle(body).getPropertyValue('--bp-panel-w'));
        return v > 0 ? v + 6 : 0;
    }

    function fit(st) {
        if (st.view3d.on) { view3dFit(st); return; }
        const vr = viewRect(st);
        const w = vr.w - reservedRight(st), h = vr.h;
        const gw = st.plan.grid.width, gh = st.plan.grid.depth;
        const scale = Math.max(0.2, Math.min(4, Math.min((w - 60) / (gw * CELL), (h - 60) / (gh * CELL))));
        st.view.scale = scale;
        st.view.ox = vr.x + (w - gw * CELL * scale) / 2;
        st.view.oy = vr.y + (h - gh * CELL * scale) / 2 + 6;
        st.staticDirty = true;
        requestRender(st);
    }

    function setPlanInternal(st, plan, markClean) {
        st.plan = normalizePlan(plan);
        st.level = st.plan.groundIndex;   // à l'ouverture, on affiche le niveau du sol (0), pas le sous-sol le plus bas
        st.layer = 0; st.cut = null;
        if (st.view3d.on) setView3dInternal(st, false);   // un plan s'ouvre sur le plan, pas sur la 3D
        st.history = []; st.future = [];
        st.selection = null;
        st.dirty = !markClean;
        st.analysis = null;
        st.staticDirty = true;
        // Purge/complète les pièces des plans chargés (graine hors poche, poche sans pièce) avant le premier
        // aller-retour d'analyse ; ne touche pas à markClean.
        refreshVox(st);
        reconcileRooms(st);
        recomputeFootprints(st);
        if (markClean) clearDraft(st); else saveDraft(st);   // le brouillon ne reflète que des modifications non sauvegardées
        fit(st);
        notifySelection(st);
        notifyLevel(st);
        notifyLayer(st);
        notifyPlan(st);
    }

    // ---- API ------------------------------------------------------------------------------------------

    function get(id) { return instances[id]; }

    function disposeInstance(id) {
        const st = get(id); if (!st) return;
        if (st.resizeObserver) st.resizeObserver.disconnect();
        if (st.raf) cancelAnimationFrame(st.raf);
        if (st.unloadHandler) window.removeEventListener('beforeunload', st.unloadHandler);
        if (st.keyHandler) document.removeEventListener('keydown', st.keyHandler);
        st.dotnetRef = null;
        st.container.innerHTML = '';
        st.container.classList.remove('bp-container');
        delete instances[id];
    }

    return {
        init: function (containerId, dotnetRef, options) {
            const container = document.getElementById(containerId);
            if (!container) return false;
            if (instances[containerId]) disposeInstance(containerId);
            const st = create(container, dotnetRef, options);
            instances[containerId] = st;
            return true;
        },
        dispose: function (id) { disposeInstance(id); },
        setCatalog: function (id, catalog) {
            const st = get(id); if (!st) return;
            st.catalog = catalog || { materials: [], objects: [], categories: [] };
            st.objectsByName = {};
            (st.catalog.objects || []).forEach(function (o) { st.objectsByName[o.name] = o; });
            st.materialsByName = {}; st.matRgb = {}; st.rotatable = new Map();
            (st.catalog.materials || []).forEach(function (m) { st.materialsByName[m.name] = m; });
            if (!st.material && st.catalog.materials && st.catalog.materials.length) st.material = st.catalog.materials[0].name;
            st.staticDirty = true; requestRender(st);
        },
        // planJson null → brouillon local s'il existe, sinon plan vide. Renvoie true si un brouillon a été restauré.
        setPlan: function (id, planJson, markClean) {
            const st = get(id); if (!st) return false;
            let plan = planJson ? JSON.parse(planJson) : null;
            let fromDraft = false;
            if (!plan) { plan = loadDraft(st); fromDraft = !!plan; }
            setPlanInternal(st, plan || emptyPlan(), planJson ? !!markClean : !fromDraft);
            return fromDraft;
        },
        getPlan: function (id) { const st = get(id); return st ? JSON.stringify(st.plan) : null; },
        setAnalysis: function (id, analysis) {
            const st = get(id); if (!st) return;
            st.analysis = analysis;
            st.view3d.dirty = true;   // arêtes vides en 3D
            st.staticDirty = true; requestRender(st);
        },
        setTool: function (id, tool) { const st = get(id); if (st) setTool(st, tool); },
        // Matériau de pose : la forme suit (formForMaterial) et la palette en est prévenue.
        setMaterial: function (id, material) {
            const st = get(id); if (!st) return;
            st.material = material;
            const m = material ? st.materialsByName[material] : null, f = formForMaterial(m && m.forms, st.formByMaterial[material], st.form, st.formRot);
            setFormInternal(st, f.form, f.rot, true);
        },
        setForm: function (id, form, rot) { const st = get(id); if (st) setFormInternal(st, form, rot, false); },
        rotate: function (id, dir) { const st = get(id); if (st) rotateCurrent(st, dir); },
        setObject: function (id, type) { const st = get(id); if (st) { st.objectType = type; if (type) setTool(st, 'object'); requestRender(st); } },
        setRotation: function (id, r) { const st = get(id); if (st) { st.rotation = r & 3; requestRender(st); } },
        setDefaults: function (id, defaults) {
            const st = get(id); if (!st) return;
            pushHistory(st);
            st.plan.defaults = Object.assign({}, st.plan.defaults, defaults);
            commit(st, 'defaults');
        },
        setAnalysisOptions: function (id, options) {
            const st = get(id); if (!st) return;
            st.plan.analysis = Object.assign({}, st.plan.analysis, options);
            commit(st, 'analysis');
        },
        // Prix unitaire saisi pour un matériau/objet (null = retour à la moyenne du serveur) ; pas d'historique, comme les options d'analyse.
        setPrice: function (id, name, value) {
            const st = get(id); if (!st) return;
            if (value == null) delete st.plan.prices[name]; else st.plan.prices[name] = value;
            commit(st, 'price');
        },
        setName: function (id, name) { const st = get(id); if (st) { st.plan.name = name; saveDraft(st); } },
        // Niveaux.
        setLevel: function (id, k) { const st = get(id); if (st) setLevelInternal(st, k); },
        addLevel: function (id) {
            const st = get(id); if (!st) return;
            pushHistory(st);
            st.plan.levels.push(emptyLevel());
            st.level = st.plan.levels.length - 1;
            select(st, null, null);
            commit(st, 'level');
            notifyLevel(st);
        },
        // Insère un sous-sol sous la pile : tous les indices glissent de +1, le numéro affiché du sol est préservé.
        addBasement: function (id) {
            const st = get(id); if (!st) return;
            pushHistory(st);
            st.plan.levels.unshift(emptyLevel());
            st.plan.groundIndex++;
            st.level = 0;
            select(st, null, null);
            commit(st, 'level');
            notifyLevel(st);
        },
        // Déplace un niveau dans la pile ; groundIndex reste une position (traverser le sol change le signe affiché).
        moveLevel: function (id, from, to) {
            const st = get(id); if (!st) return;
            const n = st.plan.levels.length;
            if (from === to || from < 0 || from >= n || to < 0 || to >= n) return;
            pushHistory(st);
            const lvl = st.plan.levels.splice(from, 1)[0];
            st.plan.levels.splice(to, 0, lvl);
            if (st.level === from) st.level = to;
            else if (from < st.level && to >= st.level) st.level--;
            else if (from > st.level && to <= st.level) st.level++;
            commit(st, 'level');
            notifyLevel(st);
        },
        removeLevel: function (id, k) {
            const st = get(id); if (!st || st.plan.levels.length <= 1 || k < 0 || k >= st.plan.levels.length) return;
            pushHistory(st);
            st.plan.levels.splice(k, 1);
            if (k < st.plan.groundIndex) st.plan.groundIndex--;
            st.plan.groundIndex = Math.min(st.plan.groundIndex, st.plan.levels.length - 1);
            if (k < st.level) st.level--; else st.level = Math.min(st.level, st.plan.levels.length - 1);
            select(st, null, null);
            commit(st, 'level');
            notifyLevel(st);
        },
        updateLevel: function (id, k, patch) {
            const st = get(id); if (!st || k < 0 || k >= st.plan.levels.length) return;
            pushHistory(st);
            const l = st.plan.levels[k];
            if (patch.name !== undefined) l.name = patch.name || '';
            if (patch.height !== undefined) l.height = patch.height > 0 ? patch.height : null;   // 0 = hauteur par défaut
            commit(st, 'level');
        },
        // Niveaux déduits des dalles du bâtiment (null : rien de construit) ; applyLevels pose les hauteurs choisies.
        detectLevels: function (id) { const st = get(id); return st ? detectLevels(st) : null; },
        applyLevels: function (id, heights, groundIndex) { const st = get(id); if (st) applyLevels(st, heights, groundIndex); },
        updateRoom: function (id, roomJson) {
            const st = get(id); if (!st) return;
            const room = JSON.parse(roomJson);
            const f = findRoom(st, room.id);
            if (!f) return;
            pushHistory(st);
            const rooms = st.plan.levels[f.level].rooms;
            rooms[rooms.indexOf(f.item)] = room;
            commit(st, 'room');
        },
        updateObject: function (id, objJson) {
            const st = get(id); if (!st) return;
            const obj = JSON.parse(objJson);
            const f = findObject(st, obj.id);
            if (!f) return;
            pushHistory(st);
            const objects = st.plan.levels[f.level].objects;
            objects[objects.indexOf(f.item)] = obj;
            commit(st, 'object');
        },
        deleteSelection: function (id) { const st = get(id); if (st) deleteSelection(st); },
        selectRoom: function (id, roomId) {
            const st = get(id); if (!st) return;
            const f = findRoom(st, roomId);
            if (f) { setLevelInternal(st, f.level); if (isArch(st)) setLayerInternal(st, levelBaseY(st, f.level) + f.item.seed.z); }   // Structure : la couche de la graine
            select(st, roomId ? 'room' : null, roomId);
        },
        selectObject: function (id, objId) {
            const st = get(id); if (!st) return;
            const f = findObject(st, objId);
            if (f) setLevelInternal(st, f.level);
            select(st, objId ? 'object' : null, objId);
        },
        focusCell: function (id, x, y, level) {
            const st = get(id); if (!st) return;
            if (level != null) setLevelInternal(st, level);
            const vr = viewRect(st), cs = cellSize(st);
            st.view.ox = vr.x + vr.w / 2 - (x + 0.5) * cs; st.view.oy = vr.y + vr.h / 2 - (y + 0.5) * cs;
            st.hover = { x: x, y: y }; st.staticDirty = true; requestRender(st);
        },
        // ---- Architecture ----
        setMode: function (id, mode) {
            const st = get(id); if (!st) return;
            mode = mode === 'architecture' ? 'architecture' : 'house';
            if (st.plan.mode === mode) return;
            pushHistory(st);
            st.plan.mode = mode;
            st.cut = null;
            if (st.objectType) { st.objectType = null; notifyObjectType(st); }
            select(st, null, null);
            setTool(st, 'select');
            commit(st, 'mode');
            fit(st);
        },
        setLayer: function (id, z) { const st = get(id); if (st) setLayerInternal(st, z); },
        setShapeOptions: function (id, options) { const st = get(id); if (st) { st.shape = Object.assign({}, st.shape, options); requestRender(st); } },
        setArchitecture: function (id, patch) {
            const st = get(id); if (!st) return;
            pushHistory(st);
            if (patch.height !== undefined) st.plan.architecture.height = Math.max(1, Math.min(MAX_ARCH_HEIGHT, patch.height | 0));
            commit(st, 'architecture');
        },
        selectOp: function (id, opId) {
            const st = get(id); if (!st) return;
            const op = opId ? findOp(st, opId) : null;
            if (op) setLayerInternal(st, op.kind === 'cells' ? (op.cells[2] || 0) : Math.min(op.a[2], op.b[2]));
            select(st, op ? 'op' : null, op ? op.id : null);
        },
        updateOp: function (id, opJson) {
            const st = get(id); if (!st) return;
            const op = JSON.parse(opJson);
            const ops = st.plan.architecture.ops, i = ops.findIndex(function (o) { return o.id === op.id; });
            if (i < 0) return;
            pushHistory(st);
            ops[i] = op;
            commit(st, 'op');
        },
        // Ops calculées côté C# (Combler les arêtes vides) : ajoutées en un seul geste, un Ctrl+Z les retire toutes.
        addOps: function (id, opsJson) {
            const st = get(id); if (!st) return;
            const ops = JSON.parse(opsJson);
            if (!ops.length) return;
            pushHistory(st);
            Array.prototype.push.apply(st.plan.architecture.ops, ops);
            commit(st, 'ops');
        },
        pickBackgroundImage: function (id) { const st = get(id); if (st) { st.fileInput.value = ''; st.fileInput.click(); } },
        pickPlanFile: function (id) { const st = get(id); if (st) { st.planInput.value = ''; st.planInput.click(); } },
        exportJson: function (id, filename) {
            const st = get(id); if (!st) return;
            const url = URL.createObjectURL(new Blob([JSON.stringify(st.plan, null, 2)], { type: 'application/json' }));
            const a = document.createElement('a');
            a.href = url;
            a.download = (filename || 'building-plan') + '.json';
            document.body.appendChild(a); a.click(); document.body.removeChild(a);
            URL.revokeObjectURL(url);
        },
        setBackgroundImage: function (id, dataUrl) { const st = get(id); if (st) loadBgImage(st, dataUrl); },
        // Placement : historique sauf pour l'opacité seule (glissière).
        updateBackgroundImage: function (id, patch) {
            const st = get(id); if (!st || !st.plan.architecture.image) return;
            const keys = Object.keys(patch);
            if (!(keys.length === 1 && keys[0] === 'opacity')) pushHistory(st);
            Object.assign(st.plan.architecture.image, patch);
            commit(st, 'image');
        },
        fitBackgroundImage: function (id) {
            const st = get(id); if (!st || !st.plan.architecture.image || !st.bgImage) return;
            pushHistory(st);
            Object.assign(st.plan.architecture.image, { x: 0, y: 0, width: fitImageWidth(st, st.bgImage) });
            commit(st, 'image');
        },
        clearBackgroundImage: function (id) {
            const st = get(id); if (!st) return;
            if (st.plan.architecture.image) { pushHistory(st); st.plan.architecture.image = null; commit(st, 'image'); }
            loadBgImage(st, null);
            if (st.dotnetRef) st.dotnetRef.invokeMethodAsync('OnBackgroundImageChanged', null).catch(function () { });
        },
        toggleBackgroundVisible: function (id) { const st = get(id); if (st) { st.bgVisible = !st.bgVisible; st.staticDirty = true; requestRender(st); } },
        getCut: function (id) { const st = get(id); return st ? st.cut : null; },
        setHouseElev: function (id, on) { const st = get(id); if (!st) return; st.houseElev = !!on; st.staticDirty = true; fit(st); },
        setView3d: function (id, on) { const st = get(id); if (st) setView3dInternal(st, on); },
        setView3dCap: function (id, on) { const st = get(id); if (!st) return; st.view3d.cap = !!on; st.view3d.dirty = true; notifyView3d(st); requestRender(st); },
        setView3dCutaway: function (id, on) { const st = get(id); if (st) setView3dCutawayInternal(st, on); },
        getView3d: function (id) { const st = get(id); if (!st) return null; const v = st.view3d; if (v.on && v.dirty && st.vox) view3dBuild(st); return { on: v.on, cap: v.cap, cutaway: v.cutaway, cutAt: v.cutAt.slice(), yaw: v.yaw, pitch: v.pitch, dist: v.dist, target: v.target.slice(), faces: v.faces, tooMany: v.tooMany }; },
        getView: function (id) { const st = get(id); return st ? { scale: st.view.scale, ox: st.view.ox, oy: st.view.oy, cell: CELL, layer: st.layer, level: st.level, elevSide: st.elevSide, houseElev: st.houseElev, strip: st.stripGeom && bands(st).strip ? { colW: st.stripGeom.colW, rowH: st.stripGeom.rowH, x0: st.stripGeom.x0, w: bands(st).strip.w } : null } : null; },
        // Fonctions pures exposées pour le test de parité avec ArchitectureEvaluator (Node) et le test des formes de blocs.
        evalOps: evalOps, countByMaterial: countByMaterial, elevation: elevation, section: section, normalizePlan: normalizePlan, solidAt: solidAt, detectPockets: detectPockets, ownerAt: ownerAt,
        FORMS: FORMS, shapeCode: shapeCode, decodeShape: decodeShape, shapeInfo: shapeInfo, lowSideTriangle: lowSideTriangle, applyForm: applyForm, rotStep: rotStep, formForMaterial: formForMaterial, shapeOpFromDrag: shapeOpFromDrag, sideShapeOp: sideShapeOp, formsPrepareSet: formsPrepareSet, formsRotateMesh: formsRotateMesh, formsNeighbor: formsNeighbor, formsPick: formsPick, formsMaterial: formsMaterial, formsObject: formsObject, v3dObjectCells: v3dObjectCells, view3dBuild: view3dBuild,
        voxRaycast: voxRaycast, v3dRay: v3dRay, v3dProject: v3dProject, v3dZoomAt: v3dZoomAt, v3dOrbit: v3dOrbit, v3dCutPlane: v3dCutPlane, v3dCutHidden: v3dCutHidden, v3dBasis: v3dBasis,
        shiftObject: shiftObject, canShiftObject: canShiftObject, cameraArrowStep: cameraArrowStep,
        resizePlan: function (id, width, depth) {
            const st = get(id); if (!st) return;
            pushHistory(st);
            st.plan.grid.width = width; st.plan.grid.depth = depth;
            st.cut = null;   // les formes hors grille sont rognées à l'évaluation, rien à filtrer
            st.plan.levels.forEach(function (level) {
                    level.objects = level.objects.filter(function (o) { return o.x < width && o.y < depth; });
            });
            commit(st, 'resize');
            fit(st);
        },
        replacePlan: function (id, planJson) { const st = get(id); if (st) replacePlan(st, planJson); },
        undo: function (id) { const st = get(id); if (st) undo(st); },
        redo: function (id) { const st = get(id); if (st) redo(st); },
        fit: function (id) { const st = get(id); if (st) fit(st); },
        zoomIn: function (id) { const st = get(id); if (!st) return; if (st.view3d.on) { st.view3d.dist = Math.max(2, st.view3d.dist / 1.25); requestRender(st); return; } const vr = viewRect(st); zoomAt(st, 1.25, vr.x + vr.w / 2, vr.y + vr.h / 2); },
        zoomOut: function (id) { const st = get(id); if (!st) return; if (st.view3d.on) { st.view3d.dist = Math.min(2000, st.view3d.dist * 1.25); requestRender(st); return; } const vr = viewRect(st); zoomAt(st, 1 / 1.25, vr.x + vr.w / 2, vr.y + vr.h / 2); },
        markSaved: function (id) { const st = get(id); if (st) { st.dirty = false; clearDraft(st); notifyPlan(st); } },
        markDirty: function (id) { const st = get(id); if (st) { st.dirty = true; saveDraft(st); notifyPlan(st); } },
        clearDraft: function (id) { const st = get(id); if (st) clearDraft(st); },
        hasDraft: function (id) { const st = get(id); return !!(st && loadDraft(st)); },
        // Brouillon local brut (null s'il n'y en a pas) : Blazor le fait migrer par le C# (PlanDocumentJson) avant setPlan.
        getDraft: function (id) { const st = get(id); return st ? rawDraft(st) : null; },
        // clean (optionnel) : la 3D est rendue sans grille, nappes ni teinte de couche (image à partager), puis restaurée au rendu suivant.
        exportPng: function (id, filename, clean) {
            const st = get(id); if (!st) return;
            const out = document.createElement('canvas');
            out.width = st.staticCanvas.width; out.height = st.staticCanvas.height;
            const ctx = out.getContext('2d');
            ctx.drawImage(st.staticCanvas, 0, 0);
            if (st.view3d.on) {
                st.view3d.clean = !!clean; view3dRender(st); st.view3d.clean = false;
                const vr = viewRect(st), d = st.dpr || 1; ctx.drawImage(st.view3d.canvas, Math.round(vr.x * d), Math.round(vr.y * d), Math.round(vr.w * d), Math.round(vr.h * d));
            }
            st.exporting = true; renderDynamic(st);
            ctx.drawImage(st.dynamicCanvas, 0, 0);
            st.exporting = false; renderDynamic(st);
            const a = document.createElement('a');
            a.href = out.toDataURL('image/png');
            a.download = (filename || 'building-plan') + '.png';
            document.body.appendChild(a); a.click(); document.body.removeChild(a);
        },
        setBeforeUnloadGuard: function (id, enabled) {
            const st = get(id); if (!st) return;
            if (st.unloadHandler) { window.removeEventListener('beforeunload', st.unloadHandler); st.unloadHandler = null; }
            if (enabled) {
                st.unloadHandler = function (e) { if (st.dirty) { e.preventDefault(); e.returnValue = ''; } };
                window.addEventListener('beforeunload', st.unloadHandler);
            }
        },
    };
})();
