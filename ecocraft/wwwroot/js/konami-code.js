(function () {
    "use strict";

    const SEQUENCE = [
        "ArrowUp", "ArrowUp",
        "ArrowDown", "ArrowDown",
        "ArrowLeft", "ArrowRight",
        "ArrowLeft", "ArrowRight",
        "b", "a"
    ];

    let progress = 0;
    let dotNetRef = null;

    window.konamiCode = {
        register: function (ref) {
            dotNetRef = ref;
        },
        unregister: function (ref) {
            if (dotNetRef === ref || ref === undefined) {
                dotNetRef = null;
            }
        }
    };

    document.addEventListener("keydown", function (event) {
        const expected = SEQUENCE[progress];
        const key = expected.length === 1 ? event.key.toLowerCase() : event.key;

        if (key === expected) {
            progress++;
            if (progress === SEQUENCE.length) {
                progress = 0;
                if (dotNetRef) {
                    dotNetRef.invokeMethodAsync("KonamiFillItemsToBuy");
                }
            }
        } else {
            progress = key === SEQUENCE[0] ? 1 : 0;
        }
    });
})();
