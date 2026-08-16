#!/usr/bin/env bash
# À lancer SUR LE SERVEUR. À placer dans le dossier scripts/,
# à côté des dossiers ecognome-prod/ et ecognome-dev/.
set -euo pipefail

ENV="${1:?Usage: ./restore.sh <prod|dev> <fichier.dump>}"
FILE="${2:?Usage: ./restore.sh <prod|dev> <fichier.dump>}"
FILE="$(realpath "$FILE")"
[ -f "$FILE" ] || { echo "Fichier introuvable : $FILE"; exit 1; }

cd "$(dirname "$0")/../ecognome-$ENV"

echo "ATTENTION : la base '$ENV' va être ÉCRASÉE par $FILE"
read -r -p "Continuer ? (oui/non) " CONFIRM
[ "$CONFIRM" = "oui" ] || { echo "Annulé."; exit 1; }

# On coupe l'app pour éviter des écritures pendant la restauration
docker compose stop app

# On repart d'un schéma vide : évite les états hybrides quand le dump
# vient d'une version plus ancienne (ex: prod -> dev). Les migrations
# manquantes seront appliquées par l'app au redémarrage (MigrateAsync).
docker compose exec -T db psql -U ecocraft -d ecocraft \
  -c "DROP SCHEMA public CASCADE; CREATE SCHEMA public;"

docker compose exec -T db pg_restore -U ecocraft -d ecocraft \
  --no-owner --no-privileges < "$FILE"

docker compose start app

echo "Restauration terminée sur '$ENV'. L'app applique les migrations manquantes au démarrage."
