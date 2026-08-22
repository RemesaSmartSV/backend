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
Controllers/    → Endpoints de la API (Auth + 8 entidades)
Services/       → Lógica de negocio (AuthService, claims)
DTOs/           → Modelos de entrada/salida (registro, login, etc.)
Converters/     → Convertidor de fechas UTC
Entities/       → Modelos de datos (Hogar, Usuario, Categoria, Movimiento,
                  Presupuesto, MetaAhorro, AporteMeta, EducacionFinanciera)
Data/           → ApplicationDbContext + factory de diseño
Migrations/     → Migraciones de EF Core (InitialCreate)
```

## Requisitos

- .NET SDK 8.0
- PostgreSQL 16 local (puerto 5432 por defecto)

## Puesta en marcha (cada desarrollador)

1. **Restaurar paquetes**
   ```
   dotnet restore
   ```

2. **Configurar la conexión a tu PostgreSQL** (sin exponer tu contraseña en el repo)
   ```
   dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=RemesaSmartDb;Username=postgres;Password=TU_CONTRASEÑA"
   ```

3. **Crear/actualizar la base de datos** (aplica las migraciones)
   ```
   dotnet ef database update
   ```

4. **Ejecutar**
   ```
   dotnet run --launch-profile http
   ```
   La API queda en `http://localhost:5203` y Swagger en `http://localhost:5203/swagger`.

> La contraseña de PostgreSQL **nunca** se sube al repositorio: la conexión real va por
> *user-secrets* (o variable de entorno `ConnectionStrings__DefaultConnection`).
> `appsettings.json` solo tiene un placeholder genérico.

## Puesta en marcha con Docker (todo el entorno)

Levanta PostgreSQL + API + Frontend con un solo comando. Requiere tener los repos
`backend` y `frontend` clonados **en la misma carpeta padre**:

```
RemesasSmart_SV/
├── backend/    ← este repo (contiene el docker-compose.yml)
└── frontend/
```

```
docker compose up --build
```

| Servicio | URL | Descripción |
|---|---|---|
| Frontend React | http://localhost:5173 | Interfaz; llama a la API vía proxy de Nginx (`/api`) |
| API .NET | http://localhost:8080 | Swagger en `http://localhost:8080/swagger` |
| PostgreSQL | localhost:5432 | DB `RemesaSmartDB` (credenciales de desarrollo en el compose) |

- Las **migraciones se aplican automáticamente** al arrancar la API: no hace falta
  `dotnet ef database update`.
- Para detener todo: `docker compose down` (agrega `-v` si quieres borrar también los datos).

## Endpoints

### Autenticación (públicos)
| Método | Ruta | Descripción |
|---|---|---|
| POST | `/api/auth/register` | Crea un Hogar + usuario Admin y devuelve JWT |
| POST | `/api/auth/login` | Inicia sesión y devuelve JWT |

Cuerpo de `register`:
```json
{
  "nombre": "Branham",
  "correo": "branham@correo.com",
  "contrasena": "Clave12345",
  "nombreFamiliar": "Familia Branham"
}
```

### Recursos (requieren `Authorization: Bearer <token>`)
| Método | Ruta | Notas |
|---|---|---|
| GET/PUT/DELETE | `/api/hogares` | Hogar del usuario; DELETE solo Admin |
| GET/POST/PUT/DELETE | `/api/usuarios` | Miembros del hogar; crear/editar/borrar solo Admin |
| GET/POST/PUT/DELETE | `/api/categorias` | Categorías de ingreso/gasto del hogar |
| GET/POST/PUT/DELETE | `/api/movimientos` | Transacciones; filtros `?categoriaId=` y `?tipo=` |
| GET/POST/PUT/DELETE | `/api/presupuestos` | Límites por categoría; filtros `?anio=` y `?mes=` |
| GET/POST/PUT/DELETE | `/api/metasahorro` | Metas de ahorro (montoActual y estado se gestionan solos) |
| GET/POST/DELETE | `/api/aportes` | Aportes a metas (`?metaId=`); actualizan el montoActual |
| GET (público) / POST / PUT / DELETE | `/api/tipsfinancieros` | Contenido de educación financiera; escritura solo Admin |

## Seguridad

- **JWT Bearer**: token con validez de 8 horas. Claims: `idUsuario`, `idHogar`, rol, email.
- **Contraseñas**: con hash (PBKDF2 vía `PasswordHasher`); el hash **nunca** se expone en las respuestas.
- **Roles**: `Admin` y `Miembro`. Operaciones sensibles (borrar hogar, gestionar usuarios/tips) solo `Admin`.
- **Aislamiento por hogar**: cada usuario solo accede a datos de su propio hogar (el `IdHogar` se toma del token, no del cuerpo).
- **Swagger**: botón *Authorize* para probar con token.
- **CORS**: habilitado para `http://localhost:5173` (frontend React/Vite).

## Flujo de trabajo (Git)

- `main` → versión estable (ramas por defecto de GitHub).
- `develop` → rama de **integración**: de hoy en adelante las tareas se integran aquí.
- Cada tarea en una rama `feature/<nombre>` que se fusiona a `develop` mediante **Pull Request**.
- Cuando `develop` está estable, se publica a `main` con otra PR.

Ejemplo:
```
git checkout develop
git pull origin develop
git checkout -b feature/mi-tarea
git push origin feature/mi-tarea   # luego abrir PR hacia develop
```

## Pendientes (fuera de este repo)

- Pantallas del MVP 1 en React (hogares, movimientos, tablero) — repo `frontend/`.
- Recordatorios programados y notificaciones (opcional).