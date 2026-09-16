#!/bin/bash
# Script para generar respaldo de la base de datos RemesaSmartDB en Docker

TIMESTAMP=$(date +"%Y%m%d_%H%M%S")
BACKUP_DIR="./backups"
BACKUP_FILE="${BACKUP_DIR}/remesasmart_backup_${TIMESTAMP}.sql"

mkdir -p "$BACKUP_DIR"

echo "Generando backup de RemesaSmartDB..."
docker exec -t remesasmart_postgres pg_dump -U postgres -d RemesaSmartDB > "$BACKUP_FILE"

if [ $? -eq 0 ]; then
    echo "Backup completado exitosamente en: $BACKUP_FILE"
else
    echo "Error al generar el backup."
    exit 1
fi
