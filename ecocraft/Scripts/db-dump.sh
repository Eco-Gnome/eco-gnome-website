#!/usr/bin/env bash
# Dump la BDD Postgres prod/dev (Docker sur eco-gnome.com) via SSH et restore
# dans le postgres local dockerisé (docker-compose.yml à la racine du repo).
#
# Usage:
#   ./db-dump.sh prod              # dump + restore
#   ./db-dump.sh dev               # idem pour dev
#   ./db-dump.sh prod dump         # dump seulement
#   ./db-dump.sh prod restore      # restore du dernier dump prod
#   ./db-dump.sh prod restore ./dumps/ecocraft-prod-20260414-1200.dump

set -euo pipefail

SSH_HOST="ecocraft@eco-gnome.com"
REMOTE_BASE="/home/ecocraft"

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
DUMP_DIR="$REPO_ROOT/dumps"
mkdir -p "$DUMP_DIR"

env="${1:-}"
action="${2:-all}"

case "$env" in
    prod) REMOTE_DIR="$REMOTE_BASE/ecognome-prod" ;;
    dev)  REMOTE_DIR="$REMOTE_BASE/ecognome-dev" ;;
    *)    echo "Usage: $0 {prod|dev} [dump|restore|all] [restore_file]" >&2; exit 1 ;;
esac

DUMP_FILE="$DUMP_DIR/ecocraft-$env-$(date +%Y%m%d-%H%M%S).dump"
LATEST_LINK="$DUMP_DIR/latest-$env.dump"

do_dump() {
    echo ">> Dump $env ($SSH_HOST:$REMOTE_DIR) -> $DUMP_FILE"
    ssh "$SSH_HOST" "
        set -euo pipefail
        PGPASSWORD=\$(grep -E '^POSTGRES_PASSWORD=' '$REMOTE_DIR/.env' | head -n1 | cut -d= -f2-)
        export PGPASSWORD
        container=\$(docker ps \
            --filter 'label=com.docker.compose.project=ecognome-$env' \
            --filter 'label=com.docker.compose.service=db' \
            --format '{{.Names}}' | head -n1)
        [ -n \"\$container\" ] || { echo 'container db introuvable' >&2; exit 1; }
        docker exec -i -e PGPASSWORD \"\$container\" \
            pg_dump -U ecocraft -d ecocraft -F c --no-owner --no-privileges
    " > "$DUMP_FILE"
    ln -sf "$(basename "$DUMP_FILE")" "$LATEST_LINK" 2>/dev/null || cp "$DUMP_FILE" "$LATEST_LINK"
    echo ">> Dump OK ($(du -h "$DUMP_FILE" | cut -f1))"
}

do_restore() {
    local file="${1:-$LATEST_LINK}"
    if [[ ! -f "$file" ]]; then
        echo "!! Fichier dump introuvable: $file" >&2
        exit 1
    fi
    echo ">> Restore $file -> docker local (service db, base ecocraft)"

    if ! (cd "$REPO_ROOT" && docker compose ps --status running --services 2>/dev/null | grep -qx db); then
        echo "!! Le service db local n'est pas démarré. Lance: (cd $REPO_ROOT && docker compose up -d db)" >&2
        exit 1
    fi

    # Drop + recreate la base pour partir propre
    (cd "$REPO_ROOT" && docker compose exec -T db \
        psql -U ecocraft -d postgres -v ON_ERROR_STOP=1) <<'SQL'
SELECT pg_terminate_backend(pid) FROM pg_stat_activity
  WHERE datname = 'ecocraft' AND pid <> pg_backend_pid();
DROP DATABASE IF EXISTS ecocraft;
CREATE DATABASE ecocraft OWNER ecocraft;
SQL

    # Restore via stdin dans le container
    (cd "$REPO_ROOT" && docker compose exec -T db \
        pg_restore -U ecocraft -d ecocraft --no-owner --no-privileges) < "$file"
    echo ">> Restore OK"
}

case "$action" in
    dump)    do_dump ;;
    restore) do_restore "${3:-}" ;;
    all)     do_dump; do_restore "$DUMP_FILE" ;;
    *)       echo "Usage: $0 {prod|dev} [dump|restore|all] [restore_file]" >&2; exit 1 ;;
esac
