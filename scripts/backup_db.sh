#!/bin/bash
set -euo pipefail
# Backup de RemesaSmartDB desde el contenedor Docker (formato custom de pg_dump).
# Uso: ./backup_db.sh
# Variables opcionales: CONTENEDOR (defecto remesasmart_postgres), DB (defecto RemesaSmartDB).

CONTENEDOR="${CONTENEDOR:-remesasmart_postgres}"
DB="${DB:-RemesaSmartDB}"
FECHA=$(date +"%Y%m%d_%H%M%S")
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ARCHIVO_BACKUP="$SCRIPT_DIR/backup_remesasmart_$FECHA.dump"

if ! docker ps --format '{{.Names}}' | grep -qx "$CONTENEDOR"; then
  echo "ERROR: el contenedor $CONTENEDOR no esta corriendo. Ejecuta 'docker compose up -d' primero." >&2
  exit 1
fi

echo "Generando backup de $DB en $ARCHIVO_BACKUP..."
docker exec "$CONTENEDOR" pg_dump -U postgres -d "$DB" -F c > "$ARCHIVO_BACKUP"

if [ ! -s "$ARCHIVO_BACKUP" ]; then
  echo "ERROR: el volcado quedo vacio." >&2
  rm -f "$ARCHIVO_BACKUP"
  exit 1
fi

sha256sum "$ARCHIVO_BACKUP" > "$ARCHIVO_BACKUP.sha256"
echo "Backup completado exitosamente: $ARCHIVO_BACKUP"
echo "Suma: $(cat "$ARCHIVO_BACKUP.sha256")"
