// Îlot Canvas2D du planificateur de bâtiment. Le JS est la source de vérité du plan pendant l'édition
// (dessin, drag, zoom/pan, historique) et renvoie le document complet à Blazor à chaque commit ;
// Blazor analyse (règles du jeu) et renvoie le résultat, affiché ici en surimpression.
// Axes : x → colonne, y → ligne (Eco Z) ; z = hauteur (Eco Y). Le plan est une pile de niveaux ; un seul est
// affiché et édité à la fois (st.level), avec les murs du niveau inférieur en filigrane et la couverture de la
// dalle (sol peint, plafond du dessous, ouverture). Les pièces sont détectées automatiquement : toute zone
// 4-connexe fermée par des murs porte une pièce (reconcileRooms) ; la graine reste un détail interne conservé
// pour le schéma et l'analyse C#. L'aperçu des pièces (flood fill 2D 4-connexe) est indicatif : seule
// l'analyse C# connaît les diagonales, arêtes vides et plafonds.
window.ecoBuildingPlanner = (function () {
    'use strict';

    const instances = {};
    const CELL = 26;
    const MAX_HISTORY = 100;
    const KIND = { OCCUPIED: 0, WALL: 1, SOLID: 2, WATER: 3, NONE: 4 };
    const TIER_COLORS = ['#7d7d7d', '#c2a26a', '#8fa3b5', '#b0784a', '#6f8f9c', '#d4af37'];
    const N8 = [[-1, -1], [0, -1], [1, -1], [-1, 0], [1, 0], [-1, 1], [0, 1], [1, 1]];

    function emptyLevel() {
        return { name: '', height: null, walls: {}, floors: {}, holes: {}, rooms: [], objects: [] };
    }

    function emptyPlan(width, depth) {
        return {
            schemaVersion: 2,
            name: '',
            grid: { width: width || 25, depth: depth || 20 },
            defaults: { wallHeight: 3, floorMaterial: null, ceilingMaterial: null },
            levels: [emptyLevel()],
            groundIndex: 0,
            analysis: { residents: 1, targetHousing: null, propertyType: 'Residence' },
        };
    }

    // Schéma 1 (collections à la racine) → niveaux ; même résultat que PlanDocument.Migrate côté C#.
    function normalizePlan(plan) {
        plan = plan || emptyPlan();
        if (!plan.grid) plan.grid = { width: 25, depth: 20 };
        if (!plan.defaults) plan.defaults = { wallHeight: 3, floorMaterial: null, ceilingMaterial: null };
        if (!plan.analysis) plan.analysis = { residents: 1, targetHousing: null, propertyType: 'Residence' };
        if (!plan.levels || !plan.levels.length) {
            plan.levels = [{ name: '', height: null, walls: plan.walls || {}, floors: plan.floors || {}, holes: {}, rooms: plan.rooms || [], objects: plan.objects || [] }];
        }
        delete plan.walls; delete plan.floors; delete plan.rooms; delete plan.objects;
        plan.levels.forEach(function (l) {
            l.name = l.name || '';
            if (l.height === undefined) l.height = null;
            l.walls = l.walls || {}; l.floors = l.floors || {}; l.holes = l.holes || {};
            l.rooms = l.rooms || []; l.objects = l.objects || [];
        });
        plan.groundIndex = Math.max(0, Math.min(plan.levels.length - 1, plan.groundIndex || 0));
        plan.schemaVersion = 2;
        return plan;
    }

    function key(x, y) { return x + ',' + y; }
    function parseKey(k) { const p = k.split(','); return { x: parseInt(p[0], 10), y: parseInt(p[1], 10) }; }
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
        container.appendChild(dynamicCanvas);

        const st = {
            container, staticCanvas, dynamicCanvas, dotnetRef,
            options: options || {},
            plan: emptyPlan(),
            level: 0,                 // niveau affiché / édité (état de vue, hors historique)
            catalog: { materials: [], objects: [], categories: [], serverId: '' },
            objectsByName: {},
            analysis: null,
            tool: 'select',
            material: null,
            objectType: null,
            rotation: 0,
            view: { scale: 1, ox: 20, oy: 20 },
            history: [], future: [],
            dirty: false,
            selection: null,          // { kind: 'object'|'room', id }
            hover: null,              // { x, y }
            pointerOver: false,       // souris réellement au-dessus du canvas (hover peut être posé par focusCell)
            drag: null,
            footprints: {},           // roomId → { cells:Set, enclosed, seedInWall, level }
            icons: {},
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

        bindEvents(st);
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
        notifyLevel(st);
        requestRender(st);
    }

    function notifyLevel(st) {
        if (!st.dotnetRef) return;
        st.dotnetRef.invokeMethodAsync('OnLevelChanged', st.level).catch(function () { });
    }

    // Ajoute au niveau affiché les murs du niveau source qui n'y sont pas (matériau seul), sans rien écraser.
    function copyWallsFrom(st, source) {
        const level = cur(st);
        const missing = Object.keys(source.walls).filter(function (k) { return !level.walls[k]; });
        if (!missing.length) return;
        pushHistory(st);
        missing.forEach(function (k) { level.walls[k] = { material: source.walls[k].material }; });
        commit(st, 'copyWalls');
    }

    // Couverture de la dalle du niveau affiché (étages seulement) : sol peint > plafond des pièces du dessous
    // (empreinte + murs adjacents, si leur plafond est à la hauteur du niveau) ; les ouvertures restent vides.
    // « low » : cellules d'une pièce du dessous dont le plafond est plus bas que le niveau (vide sous la dalle).
    function slabCells(st) {
        const out = { slab: {}, low: {} };
        const k = st.level;
        if (k === 0) return out;
        const plan = st.plan, below = plan.levels[k - 1], lh = levelHeight(st, k - 1);
        below.rooms.forEach(function (room) {
            const mat = room.ceilingMaterial || plan.defaults.ceilingMaterial;
            const fp = st.footprints[room.id];
            if (!mat || !fp) return;
            const h = room.height || lh;
            if (h > lh) return;
            const target = h === lh ? out.slab : out.low;
            fp.cells.forEach(function (kk) {
                target[kk] = { material: mat, inherited: true };
                const c = parseKey(kk);
                N8.forEach(function (d) { const nk = key(c.x + d[0], c.y + d[1]); if (below.walls[nk]) target[nk] = { material: mat, inherited: true }; });
            });
        });
        const level = cur(st);
        for (const kk in level.floors) { out.slab[kk] = { material: level.floors[kk], inherited: false }; delete out.low[kk]; }
        for (const kk in level.holes) { delete out.slab[kk]; delete out.low[kk]; }
        return out;
    }

    function pushHistory(st) {
        st.history.push(JSON.stringify(st.plan));
        if (st.history.length > MAX_HISTORY) st.history.shift();
        st.future = [];
    }

    function commit(st, label) {
        st.dirty = true;
        st.staticDirty = true;
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

    function draftKey(st) { return 'ecoBuildingPlanner.draft.' + (st.catalog.serverId || 'default'); }
    function saveDraft(st) { try { localStorage.setItem(draftKey(st), JSON.stringify(st.plan)); } catch (e) { /* quota / privé */ } }
    function loadDraft(st) { try { const s = localStorage.getItem(draftKey(st)); return s ? JSON.parse(s) : null; } catch (e) { return null; } }
    function clearDraft(st) { try { localStorage.removeItem(draftKey(st)); } catch (e) { } }

    // ---- Pièces (aperçu 2D) ---------------------------------------------------------------------------

    // Détection automatique des pièces : toute zone 4-connexe fermée par des murs (sans contact avec le
    // bord de la grille) porte exactement une pièce. Une pièce existante dont la graine reste dans une zone
    // garde id/nom/réglages (en cas de fusion, la première — ordre de création — gagne) ; une zone orpheline
    // reçoit une pièce neuve (graine au centroïde) ; le reste est supprimé. Mêmes règles de blocage que
    // recomputeFootprints et GridBuilder.FloodFill2D côté C# : les murs seulement.
    // Appelé par commit et setPlanInternal — PAS par restorePlan : undo/redo restaurent la liste exacte.
    const MIN_ROOM_CELLS = 1;   // taille minimale d'une zone pour créer une pièce (1 = toutes)
    const MAX_ROOMS = 200;      // = PlanValidator.MaxRooms (Error bloquante côté C#)

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

    function reconcileRooms(st) {
        const W = st.plan.grid.width, D = st.plan.grid.depth;
        let total = st.plan.levels.reduce(function (a, l) { return a + l.rooms.length; }, 0);
        let nextNum = 0;   // calculé paresseusement, seulement si une pièce est créée

        st.plan.levels.forEach(function (level) {
            // labels : 0 libre, -1 mur, -2 extérieur, n > 0 région fermée n.
            const labels = new Int32Array(W * D);
            for (const k in level.walls) {
                const c = parseKey(k);
                if (c.x >= 0 && c.y >= 0 && c.x < W && c.y < D) labels[c.y * W + c.x] = -1;
            }
            const stack = [];
            function flood(start, lbl) {
                labels[start] = lbl;
                stack.push(start);
                const rg = lbl > 0 ? { cells: [], sx: 0, sy: 0, count: 0 } : null;
                while (stack.length) {
                    const i = stack.pop();
                    const x = i % W, y = (i / W) | 0;
                    if (rg) { rg.cells.push(i); rg.sx += x; rg.sy += y; rg.count++; }
                    if (x > 0 && labels[i - 1] === 0) { labels[i - 1] = lbl; stack.push(i - 1); }
                    if (x < W - 1 && labels[i + 1] === 0) { labels[i + 1] = lbl; stack.push(i + 1); }
                    if (y > 0 && labels[i - W] === 0) { labels[i - W] = lbl; stack.push(i - W); }
                    if (y < D - 1 && labels[i + W] === 0) { labels[i + W] = lbl; stack.push(i + W); }
                }
                return rg;
            }
            for (let x = 0; x < W; x++) {
                if (labels[x] === 0) flood(x, -2);
                if (labels[(D - 1) * W + x] === 0) flood((D - 1) * W + x, -2);
            }
            for (let y = 0; y < D; y++) {
                if (labels[y * W] === 0) flood(y * W, -2);
                if (labels[y * W + W - 1] === 0) flood(y * W + W - 1, -2);
            }
            const regions = [];
            for (let i = 0; i < W * D; i++) if (labels[i] === 0) regions.push(flood(i, regions.length + 1));

            const claimed = new Array(regions.length).fill(false);
            const kept = [];
            level.rooms.forEach(function (room) {
                const x = room.seed.x, y = room.seed.y;
                const lbl = x >= 0 && y >= 0 && x < W && y < D ? labels[y * W + x] : -2;
                if (lbl > 0 && !claimed[lbl - 1]) { claimed[lbl - 1] = true; kept.push(room); }
                else total--;
            });

            regions.forEach(function (rg, i) {
                if (claimed[i] || rg.count < MIN_ROOM_CELLS || total >= MAX_ROOMS) return;
                if (!nextNum) nextNum = nextRoomNumber(st);
                // Graine au plus près du centroïde (déterministe ; loin des bords → pas de faux RoomTooBig).
                const cx = rg.sx / rg.count, cy = rg.sy / rg.count;
                let best = rg.cells[0], bestD = Infinity;
                rg.cells.forEach(function (idx) {
                    const dx = idx % W - cx, dy = ((idx / W) | 0) - cy;
                    const d = dx * dx + dy * dy;
                    if (d < bestD) { bestD = d; best = idx; }
                });
                kept.push({ id: uid('r'), name: (st.options.roomLabel || 'Room') + ' ' + (nextNum++), seed: { x: best % W, y: (best / W) | 0 }, ceilingMaterial: null, lockCategory: null });
                total++;
            });
            level.rooms = kept;
        });

        if (st.selection && st.selection.kind === 'room' && !findRoom(st, st.selection.id)) select(st, null, null);
    }

    function recomputeFootprints(st) {
        st.footprints = {};
        st.plan.levels.forEach(function (level, k) {
            level.rooms.forEach(function (room) {
                const cells = new Set();
                let enclosed = true;
                const seedKey = key(room.seed.x, room.seed.y);
                if (level.walls[seedKey] || !inGrid(st, room.seed.x, room.seed.y)) { st.footprints[room.id] = { cells, enclosed: false, seedInWall: true, level: k }; return; }
                const stack = [[room.seed.x, room.seed.y]];
                cells.add(seedKey);
                while (stack.length) {
                    const c = stack.pop();
                    [[1, 0], [-1, 0], [0, 1], [0, -1]].forEach(function (d) {
                        const nx = c[0] + d[0], ny = c[1] + d[1];
                        if (!inGrid(st, nx, ny)) { enclosed = false; return; }
                        const kk = key(nx, ny);
                        if (level.walls[kk] || cells.has(kk)) return;
                        cells.add(kk);
                        stack.push([nx, ny]);
                    });
                }
                st.footprints[room.id] = { cells, enclosed, seedInWall: false, level: k };
            });
        });
    }

    function roomAt(st, x, y) {
        const k = key(x, y);
        for (const id in st.footprints) if (st.footprints[id].level === st.level && st.footprints[id].cells.has(k)) return id;
        return null;
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
        img.onload = function () { st.icons[name] = img; requestRender(st); };
        img.onerror = function () { st.icons[name] = false; };
        img.src = '/assets/eco-icons/' + name + '.png?serverId=' + (st.catalog.serverId || '');
        return null;
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

    function drawHatch(ctx, p, cs) {
        ctx.save();
        ctx.beginPath(); ctx.rect(p.x, p.y, cs, cs); ctx.clip();
        ctx.fillStyle = 'rgba(0,0,0,0.35)';
        ctx.fillRect(p.x, p.y, cs, cs);
        ctx.strokeStyle = 'rgba(255,255,255,0.35)'; ctx.lineWidth = 1;
        for (let d = -cs; d < cs; d += 6) {
            ctx.beginPath(); ctx.moveTo(p.x + d, p.y); ctx.lineTo(p.x + d + cs, p.y + cs); ctx.stroke();
        }
        ctx.restore();
    }

    function renderStatic(st) {
        const ctx = setupCtx(st.staticCanvas, st);
        const w = st.container.clientWidth, h = st.container.clientHeight;
        const cs = cellSize(st);
        const plan = st.plan;
        const level = cur(st);
        ctx.fillStyle = st.palette.bg;
        ctx.fillRect(0, 0, w, h);

        const origin = toScreen(st, 0, 0);
        const gw = plan.grid.width * cs, gh = plan.grid.depth * cs;

        // Terrain de la grille.
        ctx.fillStyle = 'rgba(255,255,255,0.03)';
        ctx.fillRect(origin.x, origin.y, gw, gh);

        // Filigrane : murs du niveau inférieur.
        if (st.level > 0) {
            const below = plan.levels[st.level - 1];
            for (const k in below.walls) {
                const c = parseKey(k); const p = toScreen(st, c.x, c.y);
                ctx.fillStyle = tierColor(st, below.walls[k].material, 0.18);
                ctx.fillRect(p.x + 1, p.y + 1, cs - 2, cs - 2);
            }
        }

        // Empreintes des pièces du niveau (teinte selon l'analyse).
        level.rooms.forEach(function (room) {
            const fp = st.footprints[room.id];
            if (!fp) return;
            const ar = analysisRoom(st, room.id);
            let color = 'rgba(100,181,246,0.14)';
            if (ar) color = ar.contained ? 'rgba(76,175,80,0.16)' : 'rgba(244,67,54,0.16)';
            else if (!fp.enclosed) color = 'rgba(244,67,54,0.12)';
            ctx.fillStyle = color;
            fp.cells.forEach(function (k) {
                const c = parseKey(k); const p = toScreen(st, c.x, c.y);
                ctx.fillRect(p.x, p.y, cs, cs);
            });
        });

        if (st.level === 0) {
            // Sols surchargés.
            for (const k in level.floors) {
                const c = parseKey(k); const p = toScreen(st, c.x, c.y);
                ctx.fillStyle = tierColor(st, level.floors[k], 0.35);
                ctx.fillRect(p.x, p.y, cs, cs);
            }
        } else {
            // Dalle : sol peint (plein), plafond du dessous (léger — pas sur les murs du dessous, déjà en filigrane),
            // plafond du dessous plus bas que le niveau (hachures fines), manquant sous une pièce (rouge), ouverture (hachures).
            const cover = slabCells(st), slab = cover.slab, below = plan.levels[st.level - 1];
            for (const k in slab) {
                if (slab[k].inherited && below.walls[k]) continue;
                const c = parseKey(k); const p = toScreen(st, c.x, c.y);
                ctx.fillStyle = tierColor(st, slab[k].material, slab[k].inherited ? 0.18 : 0.35);
                ctx.fillRect(p.x, p.y, cs, cs);
            }
            ctx.strokeStyle = 'rgba(255,255,255,0.12)'; ctx.lineWidth = 1;
            for (const k in cover.low) {
                if (below.walls[k]) continue;
                const c = parseKey(k); const p = toScreen(st, c.x, c.y);
                ctx.beginPath(); ctx.moveTo(p.x, p.y + cs); ctx.lineTo(p.x + cs, p.y); ctx.stroke();
            }
            ctx.fillStyle = 'rgba(244,67,54,0.12)';
            level.rooms.forEach(function (room) {
                const fp = st.footprints[room.id];
                if (!fp) return;
                fp.cells.forEach(function (k) {
                    if (slab[k] || level.holes[k] || level.walls[k]) return;
                    const c = parseKey(k); const p = toScreen(st, c.x, c.y);
                    ctx.fillRect(p.x, p.y, cs, cs);
                });
            });
            for (const k in level.holes) {
                const c = parseKey(k);
                drawHatch(ctx, toScreen(st, c.x, c.y), cs);
            }
        }

        // Grille.
        ctx.lineWidth = 1;
        for (let x = 0; x <= plan.grid.width; x++) {
            ctx.strokeStyle = x % 5 === 0 ? st.palette.gridStrong : st.palette.grid;
            ctx.beginPath(); ctx.moveTo(origin.x + x * cs + 0.5, origin.y); ctx.lineTo(origin.x + x * cs + 0.5, origin.y + gh); ctx.stroke();
        }
        for (let y = 0; y <= plan.grid.depth; y++) {
            ctx.strokeStyle = y % 5 === 0 ? st.palette.gridStrong : st.palette.grid;
            ctx.beginPath(); ctx.moveTo(origin.x, origin.y + y * cs + 0.5); ctx.lineTo(origin.x + gw, origin.y + y * cs + 0.5); ctx.stroke();
        }

        // Ouvertures de l'étage au-dessus (trous dans le plafond de ce niveau) : contour pointillé.
        if (st.level + 1 < plan.levels.length) {
            const holes = plan.levels[st.level + 1].holes;
            ctx.save();
            ctx.strokeStyle = 'rgba(255,255,255,0.7)'; ctx.lineWidth = 2; ctx.setLineDash([4, 4]);
            ctx.beginPath();
            for (const k in holes) {
                const c = parseKey(k); const p = toScreen(st, c.x, c.y);
                if (!holes[key(c.x, c.y - 1)]) { ctx.moveTo(p.x, p.y); ctx.lineTo(p.x + cs, p.y); }
                if (!holes[key(c.x, c.y + 1)]) { ctx.moveTo(p.x, p.y + cs); ctx.lineTo(p.x + cs, p.y + cs); }
                if (!holes[key(c.x - 1, c.y)]) { ctx.moveTo(p.x, p.y); ctx.lineTo(p.x, p.y + cs); }
                if (!holes[key(c.x + 1, c.y)]) { ctx.moveTo(p.x + cs, p.y); ctx.lineTo(p.x + cs, p.y + cs); }
            }
            ctx.stroke();
            ctx.restore();
        }

        // Murs du niveau.
        for (const k in level.walls) {
            const c = parseKey(k); const p = toScreen(st, c.x, c.y);
            const wall = level.walls[k];
            ctx.fillStyle = tierColor(st, wall.material, 0.9);
            ctx.fillRect(p.x + 1, p.y + 1, cs - 2, cs - 2);
            ctx.strokeStyle = 'rgba(0,0,0,0.5)';
            ctx.strokeRect(p.x + 1.5, p.y + 1.5, cs - 3, cs - 3);
            if (wall.height && cs >= 18) {
                ctx.fillStyle = 'rgba(0,0,0,0.75)';
                ctx.font = Math.max(9, cs * 0.38) + 'px sans-serif';
                ctx.textAlign = 'right'; ctx.textBaseline = 'bottom';
                ctx.fillText('h' + wall.height, p.x + cs - 2, p.y + cs - 1);
            }
        }

        // Coordonnées.
        if (cs >= 14) {
            ctx.fillStyle = 'rgba(255,255,255,0.35)';
            ctx.font = '10px sans-serif'; ctx.textAlign = 'center'; ctx.textBaseline = 'bottom';
            for (let x = 0; x < plan.grid.width; x += 5) ctx.fillText(x, origin.x + x * cs + cs / 2, origin.y - 2);
            ctx.textAlign = 'right'; ctx.textBaseline = 'middle';
            for (let y = 0; y < plan.grid.depth; y += 5) ctx.fillText(y, origin.x - 4, origin.y + y * cs + cs / 2);
        }
    }

    function materialTier(st, name) {
        const m = st.catalog.materials.find(function (x) { return x.name === name; });
        return m ? m.tier : 0;
    }
    function tierColor(st, material, alpha) {
        const t = Math.max(0, Math.min(5, materialTier(st, material)));
        const hex = TIER_COLORS[t];
        const r = parseInt(hex.slice(1, 3), 16), g = parseInt(hex.slice(3, 5), 16), b = parseInt(hex.slice(5, 7), 16);
        return 'rgba(' + r + ',' + g + ',' + b + ',' + alpha + ')';
    }

    function renderDynamic(st) {
        const ctx = setupCtx(st.dynamicCanvas, st);
        const w = st.container.clientWidth, h = st.container.clientHeight;
        ctx.clearRect(0, 0, w, h);
        const cs = cellSize(st);
        const level = cur(st);

        // Objets au sol puis empilés.
        const ordered = level.objects.slice().sort(function (a, b) { return (a.attachedTo ? 1 : 0) - (b.attachedTo ? 1 : 0); });
        ordered.forEach(function (o) { drawObject(st, ctx, o, cs); });

        // Graines de pièce et noms.
        level.rooms.forEach(function (room) {
            const p = toScreen(st, room.seed.x, room.seed.y);
            const ar = analysisRoom(st, room.id);
            const fp = st.footprints[room.id];
            const color = ar ? (ar.contained ? st.palette.success : st.palette.error) : (fp && fp.enclosed ? st.palette.primary : st.palette.warning);
            ctx.strokeStyle = color; ctx.lineWidth = 2;
            ctx.beginPath(); ctx.arc(p.x + cs / 2, p.y + cs / 2, Math.max(4, cs * 0.28), 0, Math.PI * 2); ctx.stroke();
            ctx.fillStyle = color;
            ctx.beginPath(); ctx.arc(p.x + cs / 2, p.y + cs / 2, Math.max(2, cs * 0.1), 0, Math.PI * 2); ctx.fill();
            const showLabel = (st.selection && st.selection.kind === 'room' && st.selection.id === room.id)
                || (st.hover && st.hover.x === room.seed.x && st.hover.y === room.seed.y);
            if (cs >= 12 && showLabel) {
                ctx.font = 'bold ' + Math.max(10, cs * 0.45) + 'px sans-serif';
                ctx.textAlign = 'left'; ctx.textBaseline = 'middle';
                ctx.fillStyle = 'rgba(0,0,0,0.6)';
                const label = room.name + (ar ? '  ' + ar.volume + 'm³ T' + ar.averageTier.toFixed(2) : '');
                const tw = ctx.measureText(label).width;
                ctx.fillRect(p.x + cs + 2, p.y + cs / 2 - cs * 0.3, tw + 6, cs * 0.6);
                ctx.fillStyle = '#fff';
                ctx.fillText(label, p.x + cs + 5, p.y + cs / 2);
            }
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
            } else if (st.selection.kind === 'room') {
                const fp = st.footprints[st.selection.id];
                if (fp && fp.level === st.level) fp.cells.forEach(function (k) { const c = parseKey(k); const p = toScreen(st, c.x, c.y); ctx.strokeRect(p.x + 0.5, p.y + 0.5, cs - 1, cs - 1); });
            }
            ctx.setLineDash([]);
        }

        // Aperçu de l'outil.
        drawPreview(st, ctx, cs);
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
        if (st.drag && st.drag.kind === 'rect') {
            const r = normRect(st.drag.start, st.hover);
            const tool = st.drag.tool;
            ctx.fillStyle = tool === 'erase' ? 'rgba(244,67,54,0.25)' : (tool === 'wall' ? 'rgba(255,255,255,0.3)' : 'rgba(255,255,255,0.15)');
            for (let x = r.x0; x <= r.x1; x++) for (let y = r.y0; y <= r.y1; y++) {
                const hollow = tool === 'wall' && !(x === r.x0 || x === r.x1 || y === r.y0 || y === r.y1);
                if (hollow) continue;
                const p = toScreen(st, x, y); ctx.fillRect(p.x, p.y, cs, cs);
            }
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
            const walls = cur(st).walls;
            cells.forEach(function (c) {
                const p = toScreen(st, c.x, c.y);
                const blocked = !canAttach && (!inGrid(st, c.x, c.y) || (walls[key(c.x, c.y)] && c.kind !== KIND.WALL) || (objectAt(st, c.x, c.y) && c.kind !== KIND.WALL));
                ctx.fillStyle = canAttach ? 'rgba(255,183,77,0.8)' : blocked ? 'rgba(244,67,54,0.7)' : (c.kind === KIND.WALL ? 'rgba(255,183,77,0.9)' : 'rgba(100,181,246,0.7)');
                ctx.fillRect(p.x + 2, p.y + 2, cs - 4, cs - 4);
            });
            ctx.globalAlpha = 1;
        } else if (st.tool === 'wall' || st.tool === 'hole') {
            const p = toScreen(st, hx, hy);
            ctx.strokeStyle = st.palette.primary; ctx.lineWidth = 2;
            ctx.strokeRect(p.x + 1, p.y + 1, cs - 2, cs - 2);
        }
    }

    function normRect(a, b) {
        return { x0: Math.min(a.x, b.x), x1: Math.max(a.x, b.x), y0: Math.min(a.y, b.y), y1: Math.max(a.y, b.y) };
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

    function onPointerDown(st, e) {
        st.dynamicCanvas.focus({ preventScroll: true });
        const pc = pointerCell(st, e);
        const cell = pc.cell;
        st.dynamicCanvas.setPointerCapture(e.pointerId);

        if (e.button === 1 || st.tool === 'pan' || (e.button === 0 && e.altKey)) {
            st.drag = { kind: 'pan', startPx: pc.px, startPy: pc.py, ox: st.view.ox, oy: st.view.oy };
            return;
        }
        if (e.button === 2) {
            // Clic droit maintenu : gomme au passage quel que soit l'outil ; Maj+clic droit : gomme rectangulaire.
            if (e.shiftKey) { st.drag = { kind: 'rect', start: cell, tool: 'erase' }; return; }
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
            case 'hole':
                if (st.level === 0) break;   // pas d'ouverture dans le sol du rez-de-chaussée
                st.drag = { kind: 'rect', start: cell, tool: 'hole' };
                break;
            case 'wall':
                // Maj capturé au pointerdown : le relâcher en cours de tracé ne change pas le drag.
                st.drag = { kind: 'rect', start: cell, tool: e.shiftKey ? 'floor' : 'wall' };
                break;
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
                        const dx = nx - o.x, dy = ny - o.y;
                        o.x = nx; o.y = ny;
                        // Les objets empilés suivent leur support.
                        st.plan.levels[f.level].objects.forEach(function (child) { if (child.attachedTo === o.id) { child.x += dx; child.y += dy; } });
                    }
                }
            } else if (st.drag.kind === 'eraseBrush') {
                // Interpole entre la dernière cellule et la courante pour ne rien sauter quand le curseur va vite.
                let x = st.drag.last.x, y = st.drag.last.y;
                while (x !== pc.cell.x || y !== pc.cell.y) {
                    if (x !== pc.cell.x) x += pc.cell.x > x ? 1 : -1;
                    if (y !== pc.cell.y) y += pc.cell.y > y ? 1 : -1;
                    brushErase(st, st.drag, x, y);
                }
                st.drag.last = pc.cell;
            }
        }
        requestRender(st);
    }

    function onPointerUp(st, e) {
        const pc = pointerCell(st, e);
        const drag = st.drag;
        st.drag = null;
        if (!drag) return;
        if (drag.kind === 'rect') {
            const r = normRect(drag.start, pc.cell);
            applyRect(st, r, drag.tool);
        } else if (drag.kind === 'moveObject' && drag.moved) {
            commit(st, 'move');
        } else if (drag.kind === 'eraseBrush' && drag.changed) {
            commit(st, 'erase');
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
        if (e.key === 'Escape') { if (st.objectType) { st.objectType = null; notifyObjectType(st); } setTool(st, 'select'); select(st, null, null); return; }
        if (e.key === 'PageUp') { e.preventDefault(); setLevelInternal(st, st.level + 1); return; }
        if (e.key === 'PageDown') { e.preventDefault(); setLevelInternal(st, st.level - 1); return; }
        if (e.key.toLowerCase() === 'r') { e.preventDefault(); rotateCurrent(st); return; }
        const tools = { '1': 'select', '2': 'wall', '3': 'hole', 'h': 'pan' };
        const t = tools[e.key.toLowerCase()];
        if (t === 'hole' && st.level === 0) return;   // pas d'ouverture au rez-de-chaussée
        if (t) { setTool(st, t); }
    }

    // ---- Édition --------------------------------------------------------------------------------------

    function applyRect(st, r, tool) {
        const plan = st.plan;
        const level = cur(st);
        r.x0 = Math.max(0, r.x0); r.y0 = Math.max(0, r.y0);
        r.x1 = Math.min(plan.grid.width - 1, r.x1); r.y1 = Math.min(plan.grid.depth - 1, r.y1);
        if (r.x1 < r.x0 || r.y1 < r.y0) return;
        pushHistory(st);
        let changed = false;
        for (let x = r.x0; x <= r.x1; x++) for (let y = r.y0; y <= r.y1; y++) {
            const k = key(x, y);
            if (tool === 'wall') {
                const edge = x === r.x0 || x === r.x1 || y === r.y0 || y === r.y1;
                if (!edge) continue;
                if (!st.material) continue;
                level.walls[k] = { material: st.material, height: level.walls[k] ? level.walls[k].height : null };
                if (level.walls[k].height === null) delete level.walls[k].height;
                changed = true;
            } else if (tool === 'floor') {
                if (!st.material) continue;
                level.floors[k] = st.material; delete level.holes[k]; changed = true;
            } else if (tool === 'hole') {
                if (level.holes[k]) continue;
                level.holes[k] = true; delete level.floors[k]; changed = true;
            } else if (tool === 'erase') {
                if (level.walls[k]) { delete level.walls[k]; changed = true; }
                if (level.floors[k]) { delete level.floors[k]; changed = true; }
                if (level.holes[k]) { delete level.holes[k]; changed = true; }
                const o = objectAt(st, x, y);
                if (o) { removeObject(st, o.id); changed = true; }
            }
        }
        if (changed) commit(st, tool); else { st.history.pop(); }
    }

    // Gomme d'une cellule pendant un drag au clic droit : objet en priorité, sinon mur/sol/trou.
    // L'historique n'est poussé qu'au premier effacement du geste ; le commit arrive au pointerup.
    function brushErase(st, drag, x, y) {
        const level = cur(st);
        const k = key(x, y);
        const o = objectAt(st, x, y);
        if (!level.walls[k] && !level.floors[k] && !level.holes[k] && !o) return;
        if (!drag.pushed) { pushHistory(st); drag.pushed = true; }
        if (o) removeObject(st, o.id);
        else { delete level.walls[k]; delete level.floors[k]; delete level.holes[k]; }
        drag.changed = true;
        st.staticDirty = true;
        recomputeFootprints(st);
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

    function rotateCurrent(st) {
        if (st.selection && st.selection.kind === 'object') {
            const f = findObject(st, st.selection.id);
            if (f && !f.item.attachedTo) { pushHistory(st); f.item.rotation = ((f.item.rotation || 0) + 1) & 3; commit(st, 'rotate'); return; }
        }
        st.rotation = (st.rotation + 1) & 3;
        if (st.dotnetRef) st.dotnetRef.invokeMethodAsync('OnRotationChanged', st.rotation).catch(function () { });
        requestRender(st);
    }

    function deleteSelection(st) {
        if (!st.selection) return;
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
        requestRender(st);
        if (!same) notifySelection(st);
    }

    function setTool(st, tool) {
        st.tool = tool;
        st.drag = null;
        st.dynamicCanvas.style.cursor = tool === 'pan' ? 'grab' : tool === 'select' ? 'default' : 'crosshair';
        notifyTool(st);
        requestRender(st);
    }

    function restorePlan(st, json) {
        st.plan = normalizePlan(JSON.parse(json));
        clampLevel(st);
        st.selection = null; notifySelection(st);
        st.dirty = true; st.staticDirty = true;
        recomputeFootprints(st); saveDraft(st); notifyPlan(st); requestRender(st);
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

    function fit(st) {
        const w = st.container.clientWidth, h = st.container.clientHeight;
        const gw = st.plan.grid.width, gh = st.plan.grid.depth;
        const scale = Math.max(0.2, Math.min(4, Math.min((w - 60) / (gw * CELL), (h - 60) / (gh * CELL))));
        st.view.scale = scale;
        st.view.ox = (w - gw * CELL * scale) / 2;
        st.view.oy = (h - gh * CELL * scale) / 2 + 6;
        st.staticDirty = true;
        requestRender(st);
    }

    function setPlanInternal(st, plan, markClean) {
        st.plan = normalizePlan(plan);
        st.level = st.plan.groundIndex;   // à l'ouverture, on affiche le niveau du sol (0), pas le sous-sol le plus bas
        st.history = []; st.future = [];
        st.selection = null;
        st.dirty = !markClean;
        st.analysis = null;
        st.staticDirty = true;
        // Purge/complète les pièces des plans chargés (graine dans un mur, zone ouverte, zone sans pièce)
        // avant le premier aller-retour d'analyse ; ne touche pas à markClean.
        reconcileRooms(st);
        recomputeFootprints(st);
        if (markClean) clearDraft(st); else saveDraft(st);   // le brouillon ne reflète que des modifications non sauvegardées
        fit(st);
        notifySelection(st);
        notifyLevel(st);
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
            st.analysis = analysis; st.staticDirty = true; requestRender(st);
        },
        setTool: function (id, tool) { const st = get(id); if (st) setTool(st, tool); },
        setMaterial: function (id, material) { const st = get(id); if (st) { st.material = material; requestRender(st); } },
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
        // Ajoute au niveau affiché les murs du niveau inférieur qui n'y sont pas (matériau seul), sans rien écraser.
        copyWallsFromBelow: function (id) {
            const st = get(id); if (!st || st.level === 0) return;
            copyWallsFrom(st, st.plan.levels[st.level - 1]);
        },
        // Idem depuis le niveau supérieur (utile après l'ajout d'un sous-sol).
        copyWallsFromAbove: function (id) {
            const st = get(id); if (!st || st.level >= st.plan.levels.length - 1) return;
            copyWallsFrom(st, st.plan.levels[st.level + 1]);
        },
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
            if (f) setLevelInternal(st, f.level);
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
            const w = st.container.clientWidth, h = st.container.clientHeight, cs = cellSize(st);
            st.view.ox = w / 2 - (x + 0.5) * cs; st.view.oy = h / 2 - (y + 0.5) * cs;
            st.hover = { x: x, y: y }; st.staticDirty = true; requestRender(st);
        },
        resizePlan: function (id, width, depth) {
            const st = get(id); if (!st) return;
            pushHistory(st);
            st.plan.grid.width = width; st.plan.grid.depth = depth;
            const inside = function (k) { const c = parseKey(k); return c.x < width && c.y < depth; };
            st.plan.levels.forEach(function (level) {
                Object.keys(level.walls).forEach(function (k) { if (!inside(k)) delete level.walls[k]; });
                Object.keys(level.floors).forEach(function (k) { if (!inside(k)) delete level.floors[k]; });
                Object.keys(level.holes).forEach(function (k) { if (!inside(k)) delete level.holes[k]; });
                    level.objects = level.objects.filter(function (o) { return o.x < width && o.y < depth; });
            });
            commit(st, 'resize');
            fit(st);
        },
        undo: function (id) { const st = get(id); if (st) undo(st); },
        redo: function (id) { const st = get(id); if (st) redo(st); },
        fit: function (id) { const st = get(id); if (st) fit(st); },
        zoomIn: function (id) { const st = get(id); if (st) zoomAt(st, 1.25, st.container.clientWidth / 2, st.container.clientHeight / 2); },
        zoomOut: function (id) { const st = get(id); if (st) zoomAt(st, 1 / 1.25, st.container.clientWidth / 2, st.container.clientHeight / 2); },
        markSaved: function (id) { const st = get(id); if (st) { st.dirty = false; clearDraft(st); notifyPlan(st); } },
        markDirty: function (id) { const st = get(id); if (st) { st.dirty = true; saveDraft(st); notifyPlan(st); } },
        clearDraft: function (id) { const st = get(id); if (st) clearDraft(st); },
        hasDraft: function (id) { const st = get(id); return !!(st && loadDraft(st)); },
        exportPng: function (id, filename) {
            const st = get(id); if (!st) return;
            const out = document.createElement('canvas');
            out.width = st.staticCanvas.width; out.height = st.staticCanvas.height;
            const ctx = out.getContext('2d');
            ctx.drawImage(st.staticCanvas, 0, 0);
            ctx.drawImage(st.dynamicCanvas, 0, 0);
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
