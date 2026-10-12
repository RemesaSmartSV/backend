# Google OAuth / Google Identity Services — RemesaSmart SV

## Flujo implementado

RemesaSmart usa **Google Identity Services (GIS) con JavaScript callback**:

1. GIS renderiza el botón oficial en el frontend.
2. Google entrega un ID token (`credential`) al callback JavaScript.
3. El frontend solicita `GET /api/Auth/google-nonce`; el backend genera un nonce aleatorio, temporal (5 minutos) y de un solo uso.
4. GIS incluye el nonce en el ID token; el frontend lo envía con el token a `POST /api/Auth/google`.
5. El backend valida firma, emisor, expiración y audiencia con `Google.Apis.Auth`, compara/consume el nonce, exige `email_verified=true` y `@gmail.com`, vincula por `sub` estable y emite el JWT de RemesaSmart.

No se usa `AddGoogle`, `AddOpenIdConnect`, `login_uri` ni un redirect OAuth del middleware ASP.NET. `POST /api/Auth/google` es una API REST receptora del ID token, **no es una URI de redireccionamiento OAuth**.

## Google Cloud Console — desarrollo local

- **Tipo de cliente OAuth:** Aplicación web.
- **Orígenes autorizados de JavaScript:** `http://localhost:5173`.
- **URI de redireccionamiento autorizados:** ninguno para el callback GIS actual; dejar vacío. No registrar `POST /api/Auth/google` aquí.
- Si accedés explícitamente por `http://127.0.0.1:5173`, agregá ese origen aparte.
- El backend `http://localhost:8080` no es origen JavaScript del login; el navegador carga GIS desde el frontend.
- En producción, agregar el origen HTTPS real del frontend; no inventar el dominio.

## Configuración

- `GOOGLE_CLIENT_ID` en `backend/.env` se entrega al backend para validación de audiencia.
- Compose pasa el mismo valor al frontend como build arg `VITE_GOOGLE_CLIENT_ID`.
- Vite local toma solo `GOOGLE_CLIENT_ID` de `backend/.env` y lo define como `import.meta.env.VITE_GOOGLE_CLIENT_ID`; no expone SMTP/JWT secrets.
- GIS script: `https://accounts.google.com/gsi/client`.
- Nginx CSP permite GIS en `script-src`, `connect-src`, `frame-src` y `style-src`, incluso en `location /` y assets.

## Seguridad y límites

- Las cuentas existentes solo se vinculan tras validar el ID token y el correo Gmail verificado.
- `GoogleSubject` guarda `sub` con índice único nullable.
- Nonce backend de un uso y 5 minutos; su store en memoria corresponde a la instancia única del Docker Compose actual. Para múltiples réplicas debe usarse un store compartido.
- No se piden scopes de Gmail para iniciar sesión.
- Diez cuentas legacy no-Gmail fueron detectadas; se conservan sin borrar datos, pero la nueva política las bloquea al iniciar sesión.

## Pruebas realizadas

- `GET /api/Auth/google-nonce` devuelve nonce y `Cache-Control: no-store`.
- ID token inventado obtiene HTTP 401.
- Registro no-Gmail obtiene HTTP 400.
- Build frontend contiene el Client ID configurado; Nginx devuelve CSP que permite GIS.
- No se automatizó un login exitoso con una cuenta Google real: requiere interacción del usuario y, si aplica, que la cuenta esté en la lista de testers de Google Cloud.
