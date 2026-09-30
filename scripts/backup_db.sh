#!/bin/bash
# Script para hacer backup de la BD RemesaSmartDB desde el contenedor Docker
FECHA=$(date +"%Y%m%d_%H%M%S")
ARCHIVO_BACKUP="backup_remesasmart_$FECHA.sql"

echo "Iniciando volcado de la base de datos..."
docker exec -t remesasmart_db pg_dump -U postgres -d RemesaSmartDb -F c > "scripts/$ARCHIVO_BACKUP"
echo "Backup completado: scripts/$ARCHIVO_BACKUP"