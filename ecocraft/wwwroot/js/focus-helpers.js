(function () {
    "use strict";

    // Sort de la tabulation des éléments que MudBlazor rend focusables sans paramètre pour l'éviter (ex. en-têtes
    // de MudExpansionPanel) : Tab passe alors directement d'une case de saisie à la suivante.
    window.removeFromTabOrder = function removeFromTabOrder(selector) {
        document.querySelectorAll(selector).forEach(function (element) {
            element.tabIndex = -1;
        });
    };
})();
