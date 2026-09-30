#!/bin/bash
set -euo pipefail
# Script para hacer backup de la BD RemesaSmartDB desde el contenedor Docker
FECHA=$(date +"%Y%m%d_%H%M%S")
ARCHIVO_BACKUP="backup_remesasmart_$FECHA.sql"

if ! docker ps --format '{{.Names}}' | grep -qx 'remesasmart_postgres'; then
  echo "ERROR: el contenedor remesasmart_postgres no esta corriendo. Ejecuta 'docker compose up -d' primero." >&2
  exit 1
fi

echo "Iniciando volcado de la base de datos..."
docker exec -t remesasmart_postgres pg_dump -U postgres -d RemesaSmartDB -F c > "scripts/$ARCHIVO_BACKUP"
echo "Backup completado: scripts/$ARCHIVO_BACKUP"
