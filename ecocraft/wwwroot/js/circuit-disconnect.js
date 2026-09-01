// Blazor (.NET 9) n'envoie son beacon `_blazor/disconnect` que sur l'événement `unload`, que Chrome
// a déprécié puis désactivé. Résultat : un F5 ou une fermeture d'onglet ne prévient pas le serveur,
// et le circuit (avec sa copie du graphe serveur) reste en RAM jusqu'à la fin de la rétention.
// `pagehide` est l'équivalent moderne et fiable ; `Blazor.disconnect` est l'API publique qui envoie
// ce même beacon. On ne l'appelle pas si la page part en bfcache (persisted), car elle peut revenir.
window.addEventListener('pagehide', function (event) {
    if (event.persisted) {
        return;
    }

    if (window.Blazor && typeof window.Blazor.disconnect === 'function') {
        window.Blazor.disconnect();
    }
});
