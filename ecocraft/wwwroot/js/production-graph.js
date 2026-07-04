// Rendu de la chaîne de production de la shopping list avec vis-network (même moteur que les
// planners type satisfactory-calculator) : layout hiérarchique gauche → droite, nœuds ronds à
// image pour les tables de craft, nœuds image pour les items, arêtes droites fléchées et drag.
window.ecoProductionGraph = (function () {
    const instances = {};

    function buildNodes(data, positions) {
        return data.nodes.map(function (n) {
            const p = positions[n.id] || { x: 0, y: 0 };
            const common = {
                id: n.id,
                label: n.label,
                image: n.image,
                brokenImage: data.fallbackImage,
                // Positions calculées par le layout de Sugiyama : on fige le placement initial
                // sans contraindre le déplacement manuel (physique désactivée plus bas).
                x: p.x,
                y: p.y,
                shapeProperties: { useBorderWithImage: true },
                font: { color: '#ffffff', size: 15, multi: false, vadjust: 4 },
            };

            if (n.type === 'crafting') {
                return Object.assign(common, {
                    shape: 'circularImage',
                    size: 32,
                    borderWidth: 3,
                    color: {
                        border: '#2ec26b',
                        background: '#1e2429',
                        highlight: { border: '#5be39a', background: '#1e2429' },
                    },
                });
            }

            // matières à acheter (source) / produits finaux (puits)
            const leaf = Object.assign(common, {
                shape: 'image',
                size: 26,
            });

            // entrée goulot (matière première plafonnée bridant la chaîne) : bordure rouge.
            if (n.type === 'buy' && n.bottleneck) {
                return Object.assign(leaf, {
                    borderWidth: 3,
                    color: { border: '#e5484d', background: '#1e2429' },
                });
            }

            return leaf;
        });
    }

    function formatNumber(value) {
        return (Math.round(value * 100) / 100).toString();
    }

    function edgeLabel(e, mode) {
        if (mode === 'perHour') {
            return formatNumber(e.perMinute * 60) + '/h ' + e.item;
        }
        if (mode === 'perMinute') {
            return formatNumber(e.perMinute) + '/min ' + e.item;
        }
        return formatNumber(e.quantity) + ' ' + e.item;
    }

    function buildEdges(data, mode) {
        return data.edges.map(function (e, i) {
            return {
                id: 'e' + i,
                from: e.from,
                to: e.to,
                label: edgeLabel(e, mode),
                arrows: { to: { enabled: true, scaleFactor: 0.7, type: 'arrow' } },
                color: { color: '#8d99a1', highlight: '#ffffff', hover: '#ffffff' },
                font: {
                    color: '#d7dde2',
                    size: 12,
                    strokeWidth: 4,
                    strokeColor: '#15191d',
                    align: 'horizontal',
                },
                smooth: false, // traits droits
            };
        });
    }

    function render(containerId, data, initialMode) {
        const container = document.getElementById(containerId);
        if (!container || typeof vis === 'undefined') {
            return;
        }

        dispose(containerId);

        const mode = initialMode || 'quantity';
        const positions = window.ecoSugiyama
            ? window.ecoSugiyama.layout(data.nodes, data.edges)
            : {};
        const nodes = new vis.DataSet(buildNodes(data, positions));
        const edges = new vis.DataSet(buildEdges(data, mode));

        const options = {
            // Placement assuré par notre layout de Sugiyama (positions x/y fournies aux nœuds) :
            // le layout hiérarchique de vis-network est désactivé, la physique aussi, ce qui laisse
            // les nœuds librement déplaçables en X et Y.
            layout: { hierarchical: { enabled: false } },
            physics: { enabled: false },
            interaction: {
                dragNodes: true,
                dragView: true,
                zoomView: true,
                hover: true,
                multiselect: true,
                navigationButtons: false,
            },
            nodes: {
                labelHighlightBold: false,
                shadow: false,
            },
            edges: {
                selectionWidth: 1.5,
            },
        };

        const network = new vis.Network(container, { nodes: nodes, edges: edges }, options);
        instances[containerId] = { network: network, nodes: nodes, edges: edges, data: data, mode: mode };

        network.once('afterDrawing', function () {
            network.fit({ animation: false });
        });
    }

    function setMode(containerId, mode) {
        const inst = instances[containerId];
        if (!inst) {
            return;
        }
        inst.mode = mode;
        const updates = inst.data.edges.map(function (e, i) {
            return { id: 'e' + i, label: edgeLabel(e, mode) };
        });
        inst.edges.update(updates);
    }

    // Met à jour en place les libellés des nœuds (nombre de tables) et des arêtes (débit /min) à
    // partir de données recalculées de MÊME topologie (mêmes ids/ordre), sans re-layout : les
    // positions des nœuds sont conservées. Utilisé quand l'utilisateur change un débit cible.
    function updateLabels(containerId, data, mode) {
        const inst = instances[containerId];
        if (!inst) {
            return;
        }

        // Topologie modifiée (ex. apparition/disparition de nœuds de surplus quand une limite d'entrée
        // change) : la mise à jour en place suppose des nœuds/arêtes identiques, on relance un rendu complet.
        const sameTopology = inst.data.nodes.length === data.nodes.length
            && inst.data.edges.length === data.edges.length
            && inst.data.nodes.every(function (n, i) { return data.nodes[i] && data.nodes[i].id === n.id; });
        if (!sameTopology) {
            render(containerId, data, mode || inst.mode);
            return;
        }

        inst.data = data;
        inst.mode = mode || inst.mode;

        inst.nodes.update(data.nodes.map(function (n) {
            const update = { id: n.id, label: n.label };
            // (Dé)marque les entrées goulots sans toucher aux nœuds tables (bordure verte).
            if (n.type === 'buy') {
                update.borderWidth = n.bottleneck ? 3 : 0;
                update.color = n.bottleneck
                    ? { border: '#e5484d', background: '#1e2429' }
                    : { border: '#2ec26b', background: '#1e2429' };
            }
            return update;
        }));
        inst.edges.update(data.edges.map(function (e, i) {
            return { id: 'e' + i, label: edgeLabel(e, inst.mode) };
        }));
    }

    function fit(containerId) {
        const inst = instances[containerId];
        if (inst) {
            inst.network.fit({ animation: { duration: 300 } });
        }
    }

    function dispose(containerId) {
        const inst = instances[containerId];
        if (inst) {
            inst.network.destroy();
            delete instances[containerId];
        }
    }

    return { render: render, setMode: setMode, updateLabels: updateLabels, fit: fit, dispose: dispose };
})();
