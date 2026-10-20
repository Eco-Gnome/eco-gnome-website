(function () {
    "use strict";

    // Texte sur une ligne rogné par « … » (classe ellipsis-title) : au survol, son texte complet en infobulle,
    // seulement s'il est effectivement rogné.
    document.addEventListener("mouseover", function (event) {
        const element = event.target.closest && event.target.closest(".ellipsis-title");
        if (element) {
            element.title = element.scrollWidth > element.clientWidth ? element.textContent.trim() : "";
        }
    });
})();
