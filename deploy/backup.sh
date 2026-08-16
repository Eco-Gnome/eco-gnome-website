#!/usr/bin/env bash
# À lancer SUR LE SERVEUR. À placer dans le dossier scripts/,
# à côté des dossiers ecognome-prod/ et ecognome-dev/.
set -euo pipefail

ENV="${1:?Usage: ./backup.sh <prod|dev>}"
cd "$(dirname "$0")/../ecognome-$ENV"

mkdir -p backups
FILE="backups/ecocraft_${ENV}_$(date +%Y%m%d_%H%M%S).dump"

docker compose exec -T db pg_dump -U ecocraft -d ecocraft --format=custom > "$FILE"

echo "Backup créé : $(pwd)/$FILE"
