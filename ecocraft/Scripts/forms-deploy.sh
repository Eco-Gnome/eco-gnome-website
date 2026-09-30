#!/usr/bin/env bash
# Envoie les bundles 3D (ecocraft/wwwroot/assets/forms, gitignorés : assets SLG) dans le volume
# app-assets du site dev ou prod, via SSH. À relancer après chaque extraction de formes ou d'objets.
#
# Usage:
#   ECO_SSH_HOST=user@host ./forms-deploy.sh dev
#   ECO_SSH_HOST=user@host ./forms-deploy.sh prod

set -euo pipefail

SSH_HOST="${ECO_SSH_HOST:?ECO_SSH_HOST manquant (user@host du serveur)}"

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
ASSETS_DIR="$SCRIPT_DIR/../wwwroot/assets"

env="${1:-}"
case "$env" in
    prod|dev) ;;
    *) echo "Usage: $0 {prod|dev}" >&2; exit 1 ;;
esac

[ -f "$ASSETS_DIR/forms/index.json" ] || { echo "!! $ASSETS_DIR/forms/index.json introuvable" >&2; exit 1; }

echo ">> Envoi de forms/ ($(du -sh "$ASSETS_DIR/forms" | cut -f1)) -> ecognome-$env"
tar cf - -C "$ASSETS_DIR" forms | ssh -C "$SSH_HOST" "
    set -euo pipefail
    container=\$(docker ps \
        --filter 'label=com.docker.compose.project=ecognome-$env' \
        --filter 'label=com.docker.compose.service=app' \
        --format '{{.Names}}' | head -n1)
    [ -n \"\$container\" ] || { echo 'container app introuvable' >&2; exit 1; }
    docker exec -i \"\$container\" tar xf - -C /app/wwwroot/assets
    docker exec \"\$container\" sh -c 'ls /app/wwwroot/assets/forms | wc -l'
"
echo ">> OK (Ctrl+Shift+R côté navigateur la première fois)"
