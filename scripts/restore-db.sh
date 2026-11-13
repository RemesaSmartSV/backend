#!/bin/bash
set -euo pipefail
# Restaura RemesaSmartDB desde un respaldo generado por backup_db.sh (custom)
# o desde un volcado SQL plano. Requiere confirmación porque sobrescribe la BD.
# Uso: ./restore-db.sh <ruta_al_respaldo> [--force]

CONTENEDOR="${CONTENEDOR:-remesasmart_postgres}"
DB="${DB:-RemesaSmartDB}"

if [ -z "${1:-}" ]; then
    echo "Uso: ./restore-db.sh <ruta_al_respaldo> [--force]" >&2
    exit 1
fi

BACKUP_FILE=$1
FORCE="${2:-}"

if [ ! -f "$BACKUP_FILE" ]; then
    echo "Error: El archivo $BACKUP_FILE no existe." >&2
    exit 1
fi

if ! docker ps --format '{{.Names}}' | grep -qx "$CONTENEDOR"; then
    echo "ERROR: el contenedor $CONTENEDOR no esta corriendo." >&2
    exit 1
fi

if [ "$FORCE" != "--force" ]; then
    read -r -p "Esto sobrescribe la base de datos $DB con $BACKUP_FILE. ¿Continuar? [s/N] " respuesta
    if [ "$respuesta" != "s" ] && [ "$respuesta" != "S" ]; then
        echo "Operacion cancelada."
        exit 0
    fi
fi

if [ "$(head -c 5 "$BACKUP_FILE")" = "PGDMP" ]; then
    echo "Formato custom detectado: restaurando con pg_restore..."
    docker exec -i "$CONTENEDOR" pg_restore -U postgres -d "$DB" --clean --if-exists < "$BACKUP_FILE"
else
    echo "Formato SQL detectado: restaurando con psql..."
    docker exec -i "$CONTENEDOR" psql -U postgres -d "$DB" -v ON_ERROR_STOP=1 < "$BACKUP_FILE"
fi

echo "Restauracion completada exitosamente en $DB."
