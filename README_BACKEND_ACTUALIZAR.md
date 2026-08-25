# RemesaSmartSV — Backend

API de finanzas familiares para El Salvador (gestión de remesas). ASP.NET Core Web API (.NET 8) con PostgreSQL.

## Stack

| Tecnología | Versión |
|---|---|
| C# / .NET | 8.0 (SDK 8.0.x) |
| ASP.NET Core Web API | 8.0 |
| Entity Framework Core | 8.0.30 |
| Npgsql (PostgreSQL) | 8.0.11 |
| Swagger (Swashbuckle) | 6.9.0 |
| PostgreSQL | 16 |

## Estructura del proyecto

```
Controllers/    → Endpoints de la API (Auth + 8 entidades) — 36 endpoints totales
Services/       → Lógica de negocio (AuthService, claims)
DTOs/           → Modelos de entrada/salida (registro, login, resumen dashboard)
Converters/     → Convertidor de fechas UTC
Entities/       → Modelos de datos (8 entidades)
Data/           → ApplicationDbContext + factory de diseño
Migrations/     → Migraciones de EF Core (InitialCreate)
```

## Entidades

| Entidad | Descripción |
|---|---|
| **Hogar** | Unidad central. Cada usuario pertenece a un hogar |
| **Usuario** | Miembros del hogar (Admin o Miembro) |
| **Categoria** | Categorías de ingreso/gasto |
| **Movimiento** | Transacciones (ingresos, gastos, remesas) |
| **Presupuesto** | Límites mensuales por categoría |
| **MetaAhorro** | Metas de ahorro con monto objetivo |
| **AporteMeta** | Aportes individuales a cada meta |
| **EducacionFinanciera** | Tips de educación financiera |

## Endpoints (36 totales)

### Autenticación (públicos)
| Método | Ruta | Descripción |
|---|---|---|
| POST | `/api/Auth/register` | Crea Hogar + usuario Admin, devuelve JWT |
| POST | `/api/Auth/login` | Inicia sesión, devuelve JWT |

### Hogares
| Método | Ruta | Auth | Descripción |
|---|---|---|---|
| GET | `/api/Hogares` | User | Obtiene el hogar del usuario |
| PUT | `/api/Hogares/{id}` | User | Actualiza nombre del hogar |
| DELETE | `/api/Hogares/{id}` | Admin | Elimina el hogar |

### Usuarios (miembros)
| Método | Ruta | Auth | Descripción |
|---|---|---|---|
| GET | `/api/Usuarios` | User | Lista miembros del hogar |
| POST | `/api/Usuarios` | Admin | Agrega miembro al hogar |
| PUT | `/api/Usuarios/{id}` | Admin | Actualiza nombre/rol |
| DELETE | `/api/Usuarios/{id}` | Admin | Elimina miembro |

### Categorías
| Método | Ruta | Descripción |
|---|---|---|
| GET/POST/PUT/DELETE | `/api/Categorias` | CRUD completo, aislado por hogar |

### Movimientos
| Método | Ruta | Descripción |
|---|---|---|
| GET | `/api/Movimientos` | Lista. Filtros: `?categoriaId=`, `?tipo=` |
| GET | `/api/Movimientos/resumen` | Dashboard. Filtros: `?anio=`, `?mes=` |
| POST/PUT/DELETE | `/api/Movimientos/{id}` | CRUD completo |

### Presupuestos
| Método | Ruta | Descripción |
|---|---|---|
| GET/POST/PUT/DELETE | `/api/Presupuestos` | CRUD. Filtros: `?anio=`, `?mes=` |

### Metas de Ahorro
| Método | Ruta | Descripción |
|---|---|---|
| GET/POST/PUT/DELETE | `/api/MetasAhorro` | CRUD. Estado se gestiona automáticamente |

### Aportes
| Método | Ruta | Descripción |
|---|---|---|
| GET | `/api/Aportes?metaId={id}` | Lista aportes de una meta |
| POST/DELETE | `/api/Aportes` | Crear/eliminar. Actualiza monto actual de la meta |

### Tips Financieros
| Método | Ruta | Auth | Descripción |
|---|---|---|---|
| GET | `/api/TipsFinancieros` | Público | Lista todos los tips |
| POST/PUT/DELETE | `/api/TipsFinancieros/{id}` | Admin | CRUD de tips |

## Requisitos

- .NET SDK 8.0
- PostgreSQL 16 local (puerto 5432 por defecto)

## Puesta en marcha (desarrollo local)

1. **Restaurar paquetes**
   ```bash
   dotnet restore
   ```

2. **Configurar la conexión a PostgreSQL**
   
   Edita `appsettings.Development.json` con tu contraseña:
   ```json
   {
     "ConnectionStrings": {
       "DefaultConnection": "Host=localhost;Port=5432;Database=RemesaSmartDb;Username=postgres;Password=TU_CONTRASEÑA"
     }
   }
   ```
   > **IMPORTANTE:** `appsettings.Development.json` está en `.gitignore` y nunca se sube al repo.

3. **Ejecutar** (las migraciones se aplican automáticamente)
   ```bash
   dotnet run
   ```

4. **Abrir Swagger**
   - HTTP: `http://localhost:5203/swagger`
   - HTTPS: `https://localhost:7254/swagger`

## Docker (entorno completo)

```bash
# Desde la carpeta backend/
docker compose up --build
```

| Servicio | URL | Descripción |
|---|---|---|
| Frontend React | http://localhost:5173 | Interfaz web |
| API .NET | http://localhost:8080 | Swagger en `/swagger` |
| PostgreSQL | localhost:5432 | DB `RemesaSmartDB` |

Las migraciones se aplican automáticamente al arrancar.

## Seguridad

- **JWT Bearer**: token con validez de 8 horas. Claims: `idUsuario`, `idHogar`, rol, email.
- **Contraseñas**: hash PBKDF2-HMAC-SHA256 (100,000+ iteraciones). Nunca se expone el hash.
- **Roles**: `Admin` y `Miembro`. Operaciones sensibles solo Admin.
- **Aislamiento por hogar**: cada usuario solo accede a datos de su hogar.
- **CORS**: habilitado para `http://localhost:5173`.

## Flujo de trabajo (Git)

- `main` → versión estable
- `develop` → rama de integración
- Tareas en ramas `feature/<nombre>` → PR hacia `develop`
- `develop` estable → PR hacia `main`

```bash
git checkout develop
git pull origin develop
git checkout -b feature/mi-tarea
# ... trabajar ...
git push origin feature/mi-tarea  # abrir PR hacia develop
```

## CI/CD

GitHub Actions (`.github/workflows/ci.yml`):
- Se ejecuta en push/PR a `main` y `develop`
- Restaura NuGet, compila en Release, valida Docker build
- No hay tests unitarios aún
