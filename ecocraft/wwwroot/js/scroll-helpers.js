(function () {
    "use strict";

    window.scrollElementIntoViewCenter = function scrollElementIntoViewCenter(element) {
        if (!element || typeof element.scrollIntoView !== "function") {
            return;
        }

        element.scrollIntoView({
            behavior: "smooth",
            block: "center",
            inline: "nearest"
        });
    };
})();
