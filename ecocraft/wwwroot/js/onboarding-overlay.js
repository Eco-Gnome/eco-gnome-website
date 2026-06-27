(function () {
    "use strict";

    let dotnetRef = null;
    let scheduled = false;

    function notifyLayoutChanged() {
        // Coalesce les événements scroll/resize en un seul appel par frame.
        if (!dotnetRef || scheduled) {
            return;
        }
        scheduled = true;
        requestAnimationFrame(function () {
            scheduled = false;
            if (dotnetRef) {
                dotnetRef.invokeMethodAsync("OnLayoutChanged");
            }
        });
    }

    window.onboardingOverlay = {
        // Mesure la position/taille de chaque zone cible (par id) dans le repère du viewport.
        measure: function (ids) {
            const appBar = document.querySelector(".mud-appbar");
            const headerBottom = appBar ? appBar.getBoundingClientRect().bottom : 0;
            const viewport = { width: window.innerWidth, height: window.innerHeight, headerBottom: headerBottom, targets: [] };

            ids.forEach(function (id) {
                // Cible par id d'élément, sinon par classe. Suffixe ":N" optionnel pour viser le
                // N-ième élément portant cette classe (1-based) et éviter les superpositions.
                let selector = id;
                let index = 0;
                const colon = id.lastIndexOf(":");
                if (colon !== -1) {
                    const n = parseInt(id.substring(colon + 1), 10);
                    if (!isNaN(n)) {
                        index = n - 1;
                        selector = id.substring(0, colon);
                    }
                }

                let el = document.getElementById(selector);
                if (!el) {
                    el = document.querySelectorAll("." + selector)[index] || null;
                }
                if (!el) {
                    return;
                }
                const r = el.getBoundingClientRect();
                if (r.width <= 0 || r.height <= 0) {
                    return;
                }
                viewport.targets.push({
                    id: id,
                    x: r.left,
                    y: r.top,
                    width: r.width,
                    height: r.height
                });
            });

            return viewport;
        },

        register: function (ref) {
            dotnetRef = ref;
            window.addEventListener("resize", notifyLayoutChanged);
            window.addEventListener("scroll", notifyLayoutChanged, true);
        },

        unregister: function () {
            window.removeEventListener("resize", notifyLayoutChanged);
            window.removeEventListener("scroll", notifyLayoutChanged, true);
            dotnetRef = null;
        }
    };
})();
