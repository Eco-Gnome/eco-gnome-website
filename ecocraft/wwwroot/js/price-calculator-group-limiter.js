(function () {
    "use strict";

    const instances = {};

    function isGroupHeaderRow(row) {
        return row && row.querySelector("th[colspan]") !== null;
    }

    function isExpanded(button, queue) {
        const ariaExpanded = button.getAttribute("aria-expanded");
        if (ariaExpanded === "true") {
            return true;
        }

        if (ariaExpanded === "false") {
            return false;
        }

        return !queue.includes(button);
    }

    function getScrollContainer(element) {
        let parent = element ? element.parentElement : null;
        while (parent) {
            const style = window.getComputedStyle(parent);
            const overflowY = style.overflowY;
            const isScrollable = (overflowY === "auto" || overflowY === "scroll" || overflowY === "overlay")
                && parent.scrollHeight > parent.clientHeight + 1;
            if (isScrollable) {
                return parent;
            }

            parent = parent.parentElement;
        }

        return window;
    }

    function applyScrollDelta(anchorElement, delta) {
        if (!anchorElement || Math.abs(delta) < 1) {
            return;
        }

        const scrollContainer = getScrollContainer(anchorElement);
        if (scrollContainer === window) {
            window.scrollBy(0, delta);
            return;
        }

        scrollContainer.scrollTop += delta;
    }

    // Keep the clicked header at the same visual Y position while rows open/close.
    function keepAnchorStable(anchorElement, anchorTop) {
        if (!anchorElement) {
            return;
        }

        let frame = 0;
        const maxFrames = 8;

        const tick = () => {
            if (!anchorElement.isConnected) {
                return;
            }

            const currentTop = anchorElement.getBoundingClientRect().top;
            applyScrollDelta(anchorElement, currentTop - anchorTop);

            frame += 1;
            if (frame < maxFrames) {
                window.requestAnimationFrame(tick);
            }
        };

        window.requestAnimationFrame(tick);
    }

    function pruneQueue(state) {
        const uniqueButtons = [];
        for (const button of state.queue) {
            if (!button || !button.isConnected || uniqueButtons.includes(button)) {
                continue;
            }

            if (button.getAttribute("aria-expanded") === "false") {
                continue;
            }

            uniqueButtons.push(button);
        }

        state.queue = uniqueButtons;
    }

    function removeFromQueue(state, button) {
        state.queue = state.queue.filter((queuedButton) => queuedButton !== button);
    }

    function addToQueue(state, button) {
        removeFromQueue(state, button);
        state.queue.push(button);
    }

    // Queue policy: keep the most recently opened groups and close older ones when limit is reached.
    function enforceQueueLimit(state, currentButton) {
        pruneQueue(state);

        while (state.queue.length > state.maxOpenGroups) {
            const oldestButton = state.queue.shift();
            if (!oldestButton || oldestButton === currentButton || !oldestButton.isConnected) {
                continue;
            }

            state.suppress = true;
            oldestButton.click();
            window.requestAnimationFrame(() => {
                state.suppress = false;
                pruneQueue(state);
            });
        }
    }

    function createInstance(root, maxOpenGroups) {
        const state = {
            root,
            maxOpenGroups: Math.max(1, Number(maxOpenGroups) || 3),
            enabled: false,
            suppress: false,
            queue: []
        };

        state.onClick = (event) => {
            if (!state.enabled || state.suppress) {
                return;
            }

            const button = event.target.closest("button");
            if (!button || !state.root.contains(button)) {
                return;
            }

            const row = button.closest("tr");
            if (!isGroupHeaderRow(row)) {
                return;
            }

            const anchorElement = row || button;
            const anchorTop = anchorElement.getBoundingClientRect().top;

            window.requestAnimationFrame(() => {
                if (!state.enabled || state.suppress || !button.isConnected) {
                    return;
                }

                const expanded = isExpanded(button, state.queue);
                if (expanded) {
                    addToQueue(state, button);
                    enforceQueueLimit(state, button);
                    keepAnchorStable(anchorElement, anchorTop);
                    return;
                }

                removeFromQueue(state, button);
                keepAnchorStable(anchorElement, anchorTop);
            });
        };

        root.addEventListener("click", state.onClick, true);
        return state;
    }

    function dispose(tableId) {
        const existing = instances[tableId];
        if (!existing) {
            return;
        }

        existing.root.removeEventListener("click", existing.onClick, true);
        delete instances[tableId];
    }

    function configure(tableId, enabled, maxOpenGroups) {
        const root = document.getElementById(tableId);
        const existing = instances[tableId];

        if (!root) {
            dispose(tableId);
            return;
        }

        let state = existing;
        if (!state || state.root !== root) {
            if (state) {
                dispose(tableId);
            }
            state = createInstance(root, maxOpenGroups);
            instances[tableId] = state;
        }

        state.enabled = !!enabled;
        state.maxOpenGroups = Math.max(1, Number(maxOpenGroups) || 3);

        if (!state.enabled) {
            state.queue = [];
            state.suppress = false;
            return;
        }

        pruneQueue(state);
        enforceQueueLimit(state, null);
    }

    window.ecoPriceCalculatorGroupLimiter = {
        configure,
        dispose
    };
})();
