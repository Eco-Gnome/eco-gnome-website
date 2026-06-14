// Rendu de la chaîne de production de la shopping list avec vis-network (même moteur que les
// planners type satisfactory-calculator) : layout hiérarchique gauche → droite, nœuds ronds à
// image pour les tables de craft, nœuds image pour les items, arêtes droites fléchées et drag.
window.ecoProductionGraph = (function () {
    const instances = {};

    function buildNodes(data) {
        return data.nodes.map(function (n) {
            const common = {
                id: n.id,
                label: n.label,
                image: n.image,
                brokenImage: data.fallbackImage,
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
            return Object.assign(common, {
                shape: 'image',
                size: 26,
            });
        });
    }

    function formatNumber(value) {
        return (Math.round(value * 100) / 100).toString();
    }

    function edgeLabel(e, mode) {
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

    function render(containerId, data) {
        const container = document.getElementById(containerId);
        if (!container || typeof vis === 'undefined') {
            return;
        }

        dispose(containerId);

        const mode = 'quantity';
        const nodes = new vis.DataSet(buildNodes(data));
        const edges = new vis.DataSet(buildEdges(data, mode));

        const options = {
            layout: {
                hierarchical: {
                    enabled: true,
                    direction: 'LR',
                    sortMethod: 'directed',
                    shakeTowards: 'leaves',
                    levelSeparation: 260,
                    nodeSpacing: 170,
                    treeSpacing: 220,
                    blockShifting: true,
                    edgeMinimization: true,
                    parentCentralization: true,
                },
            },
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
        instances[containerId] = { network: network, edges: edges, data: data, mode: mode };

        network.once('afterDrawing', function () {
            // Le layout hiérarchique ne sert qu'au placement initial : on fige les positions
            // calculées puis on le désactive, ce qui libère le déplacement des nœuds en X et Y
            // (sinon vis-network les contraint à leur colonne et seul l'axe Y est déplaçable).
            const positions = network.getPositions();
            network.setOptions({ layout: { hierarchical: { enabled: false } } });
            const updates = Object.keys(positions).map(function (id) {
                return { id: id, x: positions[id].x, y: positions[id].y };
            });
            nodes.update(updates);
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

    return { render: render, setMode: setMode, fit: fit, dispose: dispose };
})();
