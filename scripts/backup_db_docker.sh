#!/usr/bin/env bash
#
# Respaldo de PostgreSQL del stack de PRODUCCIÓN (contenedor postgres_db).
# Formato "custom" de pg_dump (-Fc): restaurable con pg_restore.
#
# Uso:
#   ./scripts/backup_db_docker.sh [directorio_destino]
#   ./scripts/backup_db_docker.sh /var/backups/remesasmart
#
# Debe ejecutarse desde la carpeta backend/ (donde está docker-compose.prod.yml).

set -euo pipefail

DIR_DESTINO="${1:-./backups}"
COMPOSE_FILE="docker-compose.prod.yml"
DB="RemesaSmartDB"
USUARIO="postgres"
RETENCION_DIAS=14

mkdir -p "$DIR_DESTINO"

FECHA="$(date +%Y%m%d_%H%M%S)"
ARCHIVO="$DIR_DESTINO/remesasmart_${FECHA}.dump"

echo "[$(date -u +%FT%TZ)] Creando respaldo en $ARCHIVO ..."
docker compose -f "$COMPOSE_FILE" exec -T postgres_db \
    pg_dump -U "$USUARIO" -Fc "$DB" > "$ARCHIVO"

# Checksum para verificar integridad.
sha256sum "$ARCHIVO" > "$ARCHIVO.sha256"

TAMANO="$(du -h "$ARCHIVO" | cut -f1)"
echo "[$(date -u +%FT%TZ)] OK ($TAMANO). Checksum: $ARCHIVO.sha256"

# Retención: borra respaldos de más de N días.
find "$DIR_DESTINO" -type f -name 'remesasmart_*.dump*' -mtime "+$RETENCION_DIAS" -delete
echo "[$(date -u +%FT%TZ)] Retención aplicada: se conservan los últimos $RETENCION_DIAS días."
