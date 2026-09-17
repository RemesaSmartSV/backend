#!/bin/bash
# Script para restaurar la base de datos RemesaSmartDB desde un respaldo

if [ -z "$1" ]; then
    echo "Uso: ./restore-db.sh <ruta_al_archivo_backup.sql>"
    exit 1
fi

BACKUP_FILE=$1

if [ ! -f "$BACKUP_FILE" ]; then
    echo "Error: El archivo $BACKUP_FILE no existe."
    exit 1
fi

echo "Restaurando base de datos desde $BACKUP_FILE..."
docker exec -i remesasmart_postgres psql -U postgres -d RemesaSmartDB < "$BACKUP_FILE"

if [ $? -eq 0 ]; then
    echo "Restauracion completada exitosamente."
else
    echo "Error al restaurar la base de datos."
    exit 1
fi
