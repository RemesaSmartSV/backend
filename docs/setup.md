# Guía de Configuración del Entorno (Setup)

Esta guía describe los pasos necesarios para levantar el entorno de desarrollo de RemesaSmartSV desde cero.

## Requisitos Previos
- .NET 8 SDK
- Docker Desktop
- Node.js 18+ y Git

## Puesta en Marcha (Docker Compose)
1. Clonar el repositorio: `git clone https://github.com/RemesaSmartSV/backend.git`
2. Configurar el `.env` en la raíz de la carpeta `backend/` con la variable `POSTGRES_PASSWORD=TuPasswordSeguro123!`.
3. Levantar los servicios: `docker compose up -d --build`

## Puertos Clave
- **API Swagger:** `http://localhost:8080/swagger`
- **Healthcheck:** `http://localhost:8080/health`
- **Frontend:** `http://localhost:5173`
- **DB Dev/Test:** `5432` y `5433` respectivamente.