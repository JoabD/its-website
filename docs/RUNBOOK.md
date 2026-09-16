# RUNBOOK.md — Operación

## Arranque normal en desarrollo (sin Docker)

Necesitas MongoDB Community instalado directamente en tu máquina (sin contenedores — ver
`PROMPT-MAESTRO.md §3-bis` sobre por qué este proyecto no usa Docker) y corriendo como servicio.

```bash
# Backend
cd backend
cp src/Shekinah.Api/appsettings.Development.json.example src/Shekinah.Api/appsettings.Development.json
# edita el archivo: Jwt:SigningKey, EmailSettings:SenderAppPassword, Seed:AdminPassword
dotnet run --project src/Shekinah.Api

# Frontend (en otra terminal)
cd frontend
npm install --legacy-peer-deps
npx ng serve
```

- API: http://localhost:5080 (Scalar en `/scalar/v1`)
- Frontend: http://localhost:4200 (usa `proxy.conf.json` para reenviar `/api` a la API local)
- MongoDB: `mongodb://localhost:27017/`

## Replica set de MongoDB (por qué es obligatorio, y por qué la cadena simple sigue funcionando)

Las transacciones multi-documento de MongoDB (usadas por `TransactionBehavior` para operaciones
que tocan más de un agregado, p. ej. aprobar una solicitud y crear el usuario en la misma
operación — RN-05 y RN-14) **solo existen sobre un replica set**, incluso de un solo nodo. Sin
esto, cualquier comando que abra una `IClientSessionHandle` con transacción falla con
`MongoCommandException: Transaction numbers are only allowed on a replica set member`.

- **En producción (MongoDB Atlas M0):** no hay nada que configurar — un clúster M0 gratuito ya es,
  internamente, un replica set de 3 nodos.
- **En desarrollo local:** si instalas un `mongod` standalone (sin `--replSet`), las transacciones
  fallarán con el error de arriba. Dos opciones:
  1. Correr tu `mongod` local como replica set de un nodo:
     ```bash
     mongod --replSet rs0 --dbpath /tu/ruta/data
     # en otra terminal, una sola vez:
     mongosh --eval "rs.initiate({_id: 'rs0', members: [{_id: 0, host: 'localhost:27017'}]})"
     ```
     La cadena de conexión sigue siendo `mongodb://localhost:27017/` — el driver detecta la
     topología de replica set automáticamente al conectar, sin necesidad de `?replicaSet=rs0` en
     la URL.
  2. O, más simple para desarrollo del día a día, apuntar `Mongo:ConnectionString` directo a un
     clúster Atlas de desarrollo (la cadena SRV que entrega el panel de Atlas) y no instalar Mongo
     localmente en absoluto.

## Seed inicial de datos

`Infrastructure/Seeding/DatabaseSeeder.cs` corre al iniciar la API (solo si la colección `users`
está vacía) y crea:
- Un usuario `Administrator` con la contraseña de `Seed:AdminPassword` y `mustChangePassword=true`
  (RN-24 obliga a cambiarla en el primer login).
- Las regiones y el catálogo de materias base descritos en el spec técnico.

Para forzar un re-seed en desarrollo: borra la base `shekinah` (`mongosh shekinah --eval
"db.dropDatabase()"`) y reinicia la API. En Atlas, usa el botón de eliminar/crear colecciones desde
el panel, o `mongosh` con la cadena SRV.

## Índices y validadores de Mongo

`Infrastructure/Persistence/Migrations/M001_CreateIndexesAndValidators.cs` corre al iniciar la API
(`MigrationRunner`) y crea, entre otros:
- Índice único parcial en `academicPeriods` donde `status = "Active"` — es lo que garantiza RN-13
  ("solo un periodo activo a la vez") a nivel de base de datos, no solo en el dominio en memoria.
- Índices de unicidad en `users.enrollmentNumber` y `users.email`.

Estas migraciones son idempotentes (usan `createIndex` con los mismos nombres); correr la API
varias veces contra la misma base no falla ni duplica índices.

## Backups (MongoDB Atlas)

El clúster gratuito M0 **no incluye backups automáticos** (ver `docs/DECISIONS.md`). Para un dump
manual desde tu máquina, con la cadena SRV real del clúster:

```bash
mongodump --uri "mongodb+srv://usuario:password@cluster.xxxxx.mongodb.net/shekinah" --archive=backup-$(date +%F).archive
```

Restaurar:
```bash
mongorestore --uri "mongodb+srv://usuario:password@cluster.xxxxx.mongodb.net/shekinah" --archive=backup-2026-09-16.archive --drop
```

Si el volumen de datos crece y se necesitan backups automáticos programados, es una de las razones
documentadas en `docs/DECISIONS.md` para subir de M0 a un clúster de pago (M10+).

## Correo (Outbox, Gmail SMTP)

Ningún endpoint envía correo dentro de la misma transacción de negocio (spec técnico: "nunca
enviar correo dentro de la transacción de negocio"). `OutboxEmailSender` inserta un documento
pendiente en la colección `outboxMessages`; `OutboxProcessor` (`BackgroundService`) lo recoge y lo
envía por SMTP de Gmail (`smtp.gmail.com:587`, STARTTLS) usando `EmailSettings:SenderAppPassword`
— una **contraseña de aplicación** de Gmail, no la contraseña real de la cuenta. Si un correo no
sale, revisa primero la colección `outboxMessages` (campos `status`/`lastError`) antes de sospechar
de Gmail; el error más común es una contraseña de aplicación caducada o revocada, que se regenera
en la configuración de seguridad de la cuenta de Google.

`EmailSettings.DefaultCcAddress` se copia (Cc) únicamente en correos **administrativos** (por
ejemplo RN-04, notificación de nueva solicitud a los administradores) — nunca en correos
**personales** del alumno (credenciales de acceso, avisos de morosidad), para no filtrar datos de
un alumno hacia otra bandeja.

Gmail limita el envío desde una cuenta normal a aproximadamente 500 correos/día — de sobra para el
volumen de este instituto. Si el volumen crece, migrar a un proveedor transaccional (SendGrid,
Resend, Amazon SES) es un cambio aislado a `OutboxProcessor`/`EmailSettings`, sin tocar el resto
del sistema (el puerto `IEmailSender` no cambia).

## Rotar secretos

| Secreto | Dónde vive | Cómo rotarlo |
|---|---|---|
| `Jwt:SigningKey` | Variable de entorno del host (`Jwt__SigningKey`) | Genera una cadena aleatoria ≥32 caracteres, actualízala en el panel del host y reinicia la app. Invalida todos los access/refresh tokens vigentes — los usuarios deberán volver a iniciar sesión. |
| `EmailSettings:SenderAppPassword` | Variable de entorno del host (`EmailSettings__SenderAppPassword`) | Revoca la contraseña de aplicación actual en la configuración de seguridad de la cuenta de Gmail, genera una nueva, actualízala en el panel del host. |
| Cadena de conexión de Atlas | Variable de entorno del host (`Mongo__ConnectionString`) | Rota la contraseña del usuario de base de datos desde el panel de Atlas (Database Access), actualiza la cadena en el panel del host. |
| `Seed:AdminPassword` | Solo se usa una vez, al primer arranque con la base vacía | No es rotable después del seed inicial: el cambio de contraseña real del admin se hace desde la propia app (RN-24 ya lo fuerza en el primer login). |

## Despliegue (Vercel + Azure App Service o MonsterASP.NET + MongoDB Atlas)

Ver la tabla completa de opciones y límites reales en `PROMPT-MAESTRO.md §3-bis`. Pasos concretos:

### 1. MongoDB Atlas
1. Crea una cuenta gratuita en https://www.mongodb.com/cloud/atlas y un clúster **M0** (gratis para
   siempre).
2. En *Database Access*, crea un usuario de base de datos con password.
3. En *Network Access*, permite el acceso desde `0.0.0.0/0` (o, si tu host de API lo soporta, solo
   sus IPs salientes) — el M0 no ofrece peering privado.
4. Copia la cadena de conexión SRV desde *Connect → Drivers*.

### 2. API → Azure App Service (F1 gratuito)
1. Crea un App Service Plan tier **F1** y una Web App con stack **.NET 10**, región cercana a tus
   usuarios (México → `Mexico Central` o `South Central US`, según disponibilidad).
2. En *Configuration → Application settings*, agrega: `Mongo__ConnectionString` (la cadena SRV de
   Atlas), `Jwt__SigningKey`, `EmailSettings__SenderAppPassword`, `Seed__AdminPassword`,
   `Cors__AllowedOrigins__0` (la URL de tu proyecto en Vercel).
3. Descarga el "Publish profile" de la Web App y guárdalo como el secreto
   `AZURE_WEBAPP_PUBLISH_PROFILE` en GitHub (Settings → Secrets and variables → Actions).
4. Push a `main`: `.github/workflows/deploy-api.yml` publica automáticamente.
5. **Ten presente el tier F1**: sin "Always On", la app duerme tras inactividad y el primer request
   tras dormir tarda varios segundos — no es un bug, es el límite gratuito.

*(Alternativa: MonsterASP.NET anuncia soporte de .NET 10 en su plan gratuito. Confirma sus límites
reales dentro del panel al crear la cuenta y adapta el paso de publicación — su mecanismo típico es
FTP/Web Deploy en vez de la acción de GitHub Actions de arriba.)*

### 3. Frontend → Vercel
1. Conecta el repositorio en https://vercel.com (Framework preset: puede quedar en "Other", ya que
   `frontend/vercel.json` define el build).
2. Antes del primer deploy, edita `frontend/src/environments/environment.production.ts` con la URL
   real de tu API (paso 2).
3. Cada push a `main` dispara un deploy automático.

## Migración de datos legados (MySQL → MongoDB)

**Estado actual**: el `Migrator` corre en modo dry-run y solo lee `Preregistros` y `Usuarios` (ver
`docs/DECISIONS.md`, ADR-011). No escribe a Mongo todavía.

```bash
cd backend
dotnet run --project src/Shekinah.Migrator -- --mysql-connection "Server=...;Database=...;Uid=...;Pwd=..." --dry-run
```

El reporte de reconciliación (`Reports/ReconciliationReport.cs`) imprime cuántos registros se
leyeron, cuántos se mapearían con éxito y cuáles fallarían con su motivo — antes de escribir nada.
El MySQL legado en `C:\laragon\www\shekinah` sigue siendo solo lectura (R1): el migrador se conecta
por configuración a lo que ya existe, no requiere que subas ese MySQL a ningún lado. Si necesitas
una copia remota de trabajo (por ejemplo para no depender de VPN durante el desarrollo del
migrador), ver la opción de Aiven for MySQL en `PROMPT-MAESTRO.md §3-bis`.

## Troubleshooting

| Síntoma | Causa probable | Acción |
|---|---|---|
| `MongoCommandException: Transaction numbers are only allowed on a replica set member` | Tu Mongo local no es un replica set | Ver la sección de replica set arriba: corre `mongod --replSet rs0` + `rs.initiate()`, o apunta a Atlas directamente |
| El frontend no recibe respuesta de `/api/*` en `ng serve` | `proxy.conf.json` apunta a un puerto distinto al de la API local | Confirma que la API corre en `http://localhost:5080` o ajusta `proxy.conf.json` |
| El frontend desplegado en Vercel no recibe respuesta de la API | `environment.production.ts` tiene la URL vieja/incorrecta, o CORS no incluye el dominio de Vercel | Verifica `apiBaseUrl` en el build y `Cors__AllowedOrigins__0` en el App Service |
| Login falla con 401 aunque la contraseña es correcta | Usuario bloqueado por intentos fallidos (RN-07) | Revisa `credentials.lockedUntilUtc` en el documento del usuario en Mongo |
| Los correos no llegan | App password de Gmail revocada/incorrecta, o el mensaje quedó "Dead" en el outbox | Revisa `outboxMessages` (`status`, `lastError`); regenera la app password si es necesario |
| `npm install` falla con `Cannot read properties of null (reading 'edgesOut')` | Bug conocido del resolver de npm con las peer deps de Angular 21 + vitest 4 + msw | Usa `npm install --legacy-peer-deps` (ya documentado en README) |
| `dotnet build` falla justo después de clonar | Es la primera vez que se restaura contra NuGet real en este proyecto (ver README) | Corre `dotnet restore Shekinah.sln` y revisa el primer error; probablemente una versión de paquete que cambió |
| La API en Azure F1 tarda varios segundos en el primer request | Tier F1 sin "Always On" — la app duerme tras inactividad | Esperado en el tier gratuito; considera MonsterASP.NET o un tier de pago si el cold-start es inaceptable |
