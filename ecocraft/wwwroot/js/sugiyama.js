// Layout hiérarchique de Sugiyama, autonome (sans dépendance), pour positionner les nœuds d'un
// graphe orienté de gauche à droite (flux producteur → consommateur). Plus soigné que le layout
// hiérarchique intégré de vis-network : minimisation des croisements par heuristique médiane et
// assignation des coordonnées par méthode des priorités (les arêtes longues sont gardées droites).
//
// Étapes classiques :
//   1. Suppression des cycles      (DFS, on inverse les arêtes retour pour obtenir un DAG)
//   2. Assignation des couches      (longest-path : sources en couche 0, +1 par arête)
//   3. Insertion de nœuds virtuels  (une arête qui saute des couches devient une chaîne)
//   4. Minimisation des croisements (heuristique médiane, balayages haut/bas, on garde le meilleur)
//   5. Assignation des coordonnées  (méthode des priorités, alignement sur la médiane des voisins)
//
// API : window.ecoSugiyama.layout(nodes, edges, opts) -> { [id]: { x, y } } (positions réelles
// uniquement, les nœuds virtuels sont retirés). `opts` : { layerSep, nodeGap, heightOf }.
window.ecoSugiyama = (function () {
    const DEFAULTS = {
        layerSep: 320, // distance entre deux couches (axe X, flux du graphe)
        nodeGap: 45,   // espace minimal entre deux nœuds d'une même couche (axe Y), en plus de leur taille
        // Hauteur (extension verticale) d'un nœud selon son type, sert au calcul de l'écartement.
        heightOf: function (node) {
            return node.type === 'crafting' ? 110 : 78;
        },
    };

    // -- 1. Suppression des cycles ------------------------------------------------------------
    // DFS itératif : une arête vers un nœud présent dans la pile courante est une arête retour,
    // on l'inverse pour casser le cycle (le DAG résultant suffit au reste de l'algorithme).
    function removeCycles(ids, outAdj) {
        const visited = new Set();
        const inStack = new Set();

        for (const start of ids) {
            if (visited.has(start)) {
                continue;
            }
            const stack = [{ id: start, i: 0 }];
            inStack.add(start);
            visited.add(start);

            while (stack.length > 0) {
                const top = stack[stack.length - 1];
                const neighbors = outAdj.get(top.id);
                if (top.i < neighbors.length) {
                    const next = neighbors[top.i++];
                    if (inStack.has(next)) {
                        // Arête retour next->… : on l'inverse en retirant top.id -> next.
                        // (On se contente d'ignorer l'arête fautive pour le calcul des couches.)
                        const back = outAdj.get(next);
                        const at = back.indexOf(top.id);
                        if (at === -1) {
                            back.push(top.id);
                        }
                        neighbors.splice(top.i - 1, 1);
                        top.i -= 1;
                    } else if (!visited.has(next)) {
                        visited.add(next);
                        inStack.add(next);
                        stack.push({ id: next, i: 0 });
                    }
                } else {
                    inStack.delete(top.id);
                    stack.pop();
                }
            }
        }
    }

    // -- 2. Assignation des couches (longest-path via tri topologique) ------------------------
    function assignLayers(ids, outAdj, inAdj) {
        const indeg = new Map();
        ids.forEach(function (id) { indeg.set(id, inAdj.get(id).length); });

        const queue = ids.filter(function (id) { return indeg.get(id) === 0; });
        const layer = new Map();
        ids.forEach(function (id) { layer.set(id, 0); });

        let head = 0;
        while (head < queue.length) {
            const id = queue[head++];
            const l = layer.get(id);
            for (const next of outAdj.get(id)) {
                if (layer.get(next) < l + 1) {
                    layer.set(next, l + 1);
                }
                indeg.set(next, indeg.get(next) - 1);
                if (indeg.get(next) === 0) {
                    queue.push(next);
                }
            }
        }
        return layer;
    }

    // -- 3. Nœuds virtuels + structure par couches -------------------------------------------
    // Construit, pour chaque arête réelle (u,v) avec layer(v)-layer(u) > 1, une chaîne de nœuds
    // virtuels traversant les couches intermédiaires. Renvoie les couches (tableaux d'ids ordonnés)
    // et l'adjacence du graphe « segmenté » (réel + virtuel) utilisée par les étapes 4 et 5.
    function buildLayers(ids, edges, layer, realSet) {
        const maxLayer = ids.reduce(function (m, id) { return Math.max(m, layer.get(id)); }, 0);
        const layers = [];
        for (let i = 0; i <= maxLayer; i++) {
            layers.push([]);
        }
        ids.forEach(function (id) { layers[layer.get(id)].push(id); });

        const up = new Map();   // voisins dans la couche précédente
        const down = new Map(); // voisins dans la couche suivante
        ids.forEach(function (id) { up.set(id, []); down.set(id, []); });

        let dummySeq = 0;
        edges.forEach(function (e) {
            let a = e.from;
            let b = e.to;
            let la = layer.get(a);
            let lb = layer.get(b);
            if (la === undefined || lb === undefined) {
                return;
            }
            if (la === lb) {
                return; // arête intra-couche : ignorée pour l'ordonnancement
            }
            if (la > lb) { const t = a; a = b; b = t; const tl = la; la = lb; lb = tl; }

            let prev = a;
            for (let l = la + 1; l < lb; l++) {
                const dummy = '__d' + (dummySeq++);
                layers[l].push(dummy);
                layer.set(dummy, l);
                up.set(dummy, []);
                down.set(dummy, []);
                down.get(prev).push(dummy);
                up.get(dummy).push(prev);
                prev = dummy;
            }
            down.get(prev).push(b);
            up.get(b).push(prev);
        });

        return { layers: layers, up: up, down: down };
    }

    // -- 4. Minimisation des croisements (heuristique médiane) -------------------------------
    function medianOf(neighborPositions) {
        const m = neighborPositions.length;
        if (m === 0) {
            return -1; // pas de voisin : position laissée fixe
        }
        neighborPositions.sort(function (x, y) { return x - y; });
        const mid = Math.floor(m / 2);
        if (m % 2 === 1) {
            return neighborPositions[mid];
        }
        if (m === 2) {
            return (neighborPositions[0] + neighborPositions[1]) / 2;
        }
        const left = neighborPositions[mid - 1] - neighborPositions[0];
        const right = neighborPositions[m - 1] - neighborPositions[mid];
        if (left + right === 0) {
            return (neighborPositions[mid - 1] + neighborPositions[mid]) / 2;
        }
        return (neighborPositions[mid - 1] * right + neighborPositions[mid] * left) / (left + right);
    }

    function indexMap(layerArr) {
        const idx = new Map();
        layerArr.forEach(function (id, i) { idx.set(id, i); });
        return idx;
    }

    // Réordonne `layerArr` selon la médiane calculée depuis `refIdx` (indices de la couche voisine).
    // Les nœuds sans voisin (médiane -1) conservent leur emplacement (façon Graphviz wmedian).
    function sortByMedian(layerArr, adj, refIdx) {
        const median = new Map();
        layerArr.forEach(function (id) {
            const positions = adj.get(id).map(function (n) { return refIdx.get(n); })
                .filter(function (p) { return p !== undefined; });
            median.set(id, medianOf(positions));
        });

        const fixedAt = new Set();
        layerArr.forEach(function (id, i) { if (median.get(id) < 0) { fixedAt.add(i); } });

        const movable = layerArr.filter(function (id) { return median.get(id) >= 0; })
            .sort(function (a, b) { return median.get(a) - median.get(b); });

        const result = new Array(layerArr.length);
        fixedAt.forEach(function (i) { result[i] = layerArr[i]; });
        let mi = 0;
        for (let i = 0; i < result.length; i++) {
            if (!fixedAt.has(i)) {
                result[i] = movable[mi++];
            }
        }
        return result;
    }

    // Nombre de croisements entre deux couches adjacentes (comptage direct O(E²), suffisant ici).
    function countCrossings(upperIdx, lowerLayer, upAdjOfLower) {
        const pairs = [];
        lowerLayer.forEach(function (id, lowPos) {
            upAdjOfLower.get(id).forEach(function (n) {
                const hi = upperIdx.get(n);
                if (hi !== undefined) {
                    pairs.push([hi, lowPos]);
                }
            });
        });
        let crossings = 0;
        for (let i = 0; i < pairs.length; i++) {
            for (let j = i + 1; j < pairs.length; j++) {
                const a = pairs[i];
                const b = pairs[j];
                if ((a[0] < b[0] && a[1] > b[1]) || (a[0] > b[0] && a[1] < b[1])) {
                    crossings++;
                }
            }
        }
        return crossings;
    }

    function totalCrossings(layers, up) {
        let total = 0;
        for (let l = 1; l < layers.length; l++) {
            total += countCrossings(indexMap(layers[l - 1]), layers[l], up);
        }
        return total;
    }

    function minimizeCrossings(layers, up, down, iterations) {
        let best = layers.map(function (l) { return l.slice(); });
        let bestCrossings = totalCrossings(layers, up);

        for (let it = 0; it < iterations && bestCrossings > 0; it++) {
            const downward = it % 2 === 0;
            if (downward) {
                for (let l = 1; l < layers.length; l++) {
                    layers[l] = sortByMedian(layers[l], up, indexMap(layers[l - 1]));
                }
            } else {
                for (let l = layers.length - 2; l >= 0; l--) {
                    layers[l] = sortByMedian(layers[l], down, indexMap(layers[l + 1]));
                }
            }

            const c = totalCrossings(layers, up);
            if (c < bestCrossings) {
                bestCrossings = c;
                best = layers.map(function (l) { return l.slice(); });
            }
        }

        return best;
    }

    // -- 5. Assignation des coordonnées (méthode des priorités) ------------------------------
    // Pour chaque couche, on aligne les nœuds sur la médiane de leurs voisins de la couche de
    // référence, en respectant l'ordre et l'écartement minimal. Les nœuds de plus haute priorité
    // (nœuds virtuels surtout, pour garder les arêtes longues droites) bougent en premier et
    // poussent les nœuds de priorité inférieure.
    function assignCoordinates(layers, up, down, layerToId, opts) {
        const pos = new Map();   // position sur l'axe transversal (Y)
        const sep = new Map();   // écartement minimal requis entre nœud[i] et nœud[i+1] d'une couche

        const heightOf = function (id) {
            return id.indexOf('__d') === 0 ? 1 : opts.heightOf(layerToId.get(id));
        };

        // Initialisation : empilement par ordre courant avec écartement minimal.
        layers.forEach(function (layerArr) {
            let y = 0;
            for (let i = 0; i < layerArr.length; i++) {
                if (i > 0) {
                    const s = (heightOf(layerArr[i - 1]) + heightOf(layerArr[i])) / 2 + opts.nodeGap;
                    sep.set(layerArr[i - 1] + '|' + layerArr[i], s);
                    y += s;
                }
                pos.set(layerArr[i], y);
            }
        });

        const priority = function (id) {
            if (id.indexOf('__d') === 0) {
                return 1e6; // nœud virtuel : priorité maximale (arêtes longues droites)
            }
            return up.get(id).length + down.get(id).length;
        };

        function placeLayer(layerArr, adj, refPos) {
            const n = layerArr.length;
            const desired = layerArr.map(function (id) {
                const positions = adj.get(id).map(function (nb) { return refPos.get(nb); })
                    .filter(function (p) { return p !== undefined; });
                return positions.length ? medianOf(positions) : null;
            });
            const sepOf = function (i) { return sep.get(layerArr[i] + '|' + layerArr[i + 1]); };

            const order = layerArr.map(function (_, i) { return i; })
                .sort(function (a, b) { return priority(layerArr[b]) - priority(layerArr[a]); });

            order.forEach(function (i) {
                const d = desired[i];
                if (d === null) {
                    return;
                }
                const cur = pos.get(layerArr[i]);
                if (d > cur) {
                    // Descendre : limité par le premier nœud de priorité ≥ situé plus bas.
                    let gapSum = 0;
                    let limit = Infinity;
                    for (let j = i + 1; j < n; j++) {
                        gapSum += sepOf(j - 1);
                        if (priority(layerArr[j]) >= priority(layerArr[i])) {
                            limit = pos.get(layerArr[j]) - gapSum;
                            break;
                        }
                    }
                    const np = Math.min(d, limit);
                    pos.set(layerArr[i], np);
                    let prev = np;
                    for (let j = i + 1; j < n; j++) {
                        const need = prev + sepOf(j - 1);
                        if (pos.get(layerArr[j]) < need) {
                            pos.set(layerArr[j], need);
                        }
                        prev = pos.get(layerArr[j]);
                    }
                } else if (d < cur) {
                    // Monter : symétrique.
                    let gapSum = 0;
                    let limit = -Infinity;
                    for (let j = i - 1; j >= 0; j--) {
                        gapSum += sepOf(j);
                        if (priority(layerArr[j]) >= priority(layerArr[i])) {
                            limit = pos.get(layerArr[j]) + gapSum;
                            break;
                        }
                    }
                    const np = Math.max(d, limit);
                    pos.set(layerArr[i], np);
                    let prev = np;
                    for (let j = i - 1; j >= 0; j--) {
                        const need = prev - sepOf(j);
                        if (pos.get(layerArr[j]) > need) {
                            pos.set(layerArr[j], need);
                        }
                        prev = pos.get(layerArr[j]);
                    }
                }
            });
        }

        // Balayages alternés : vers le bas (référence = couche précédente), puis vers le haut.
        for (let it = 0; it < 8; it++) {
            const downward = it % 2 === 0;
            if (downward) {
                for (let l = 1; l < layers.length; l++) {
                    placeLayer(layers[l], up, pos);
                }
            } else {
                for (let l = layers.length - 2; l >= 0; l--) {
                    placeLayer(layers[l], down, pos);
                }
            }
        }

        return pos;
    }

    function layout(nodes, edges, options) {
        const opts = Object.assign({}, DEFAULTS, options || {});
        const result = {};
        if (!nodes || nodes.length === 0) {
            return result;
        }

        const ids = nodes.map(function (n) { return n.id; });
        const idSet = new Set(ids);
        const layerToId = new Map();
        nodes.forEach(function (n) { layerToId.set(n.id, n); });

        const outAdj = new Map();
        const inAdj = new Map();
        ids.forEach(function (id) { outAdj.set(id, []); inAdj.set(id, []); });

        const cleanEdges = [];
        edges.forEach(function (e) {
            if (e.from === e.to || !idSet.has(e.from) || !idSet.has(e.to)) {
                return;
            }
            outAdj.get(e.from).push(e.to);
            inAdj.get(e.to).push(e.from);
            cleanEdges.push({ from: e.from, to: e.to });
        });

        removeCycles(ids, outAdj);

        // Reconstruit inAdj cohérent avec outAdj après suppression des cycles.
        ids.forEach(function (id) { inAdj.set(id, []); });
        outAdj.forEach(function (targets, src) {
            targets.forEach(function (t) { inAdj.get(t).push(src); });
        });

        const layer = assignLayers(ids, outAdj, inAdj);
        const built = buildLayers(ids, cleanEdges, layer, idSet);

        const ordered = minimizeCrossings(built.layers, built.up, built.down, 24);
        const pos = assignCoordinates(ordered, built.up, built.down, layerToId, opts);

        ids.forEach(function (id) {
            result[id] = { x: layer.get(id) * opts.layerSep, y: pos.get(id) || 0 };
        });
        return result;
    }

    return { layout: layout };
})();
