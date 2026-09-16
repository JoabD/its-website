# Instituto Teológico Shekinah (ITS) — Sistema de Gestión

Migración del sistema legado PHP/MySQL a una arquitectura moderna: **.NET 10 (C# 14)** con Clean
Architecture + DDD sobre **MongoDB**, y un frontend **Angular 21** (standalone, zoneless, signals).
Construido a partir de `PROMPT-MAESTRO.md` y `ESPECIFICACION-TECNICA.md`.

**Este proyecto no usa Docker.** El despliegue es **Vercel** (frontend) + **Azure App Service o
MonsterASP.NET** (API, ambos con capa gratuita) + **MongoDB Atlas** (base de datos, capa gratuita
M0). Ver `PROMPT-MAESTRO.md §3-bis` para el detalle completo.

## Antes de empezar: qué está verificado y qué no

Este entregable se construyó en un entorno de nube sin acceso a `api.nuget.org` (solo `npm` estaba
disponible). Eso definió qué se pudo verificar realmente con un build/test real y qué se escribió
con cuidado pero sin ejecutar:

| Capa | Estado |
|---|---|
| `Shekinah.Domain` | ✅ `dotnet build` verificado — 0 warnings, 0 errores, cero paquetes NuGet (regla de oro R3) |
| `Shekinah.Application`, `Infrastructure`, `Api`, `Migrator` | ⚠️ Código completo, revisado a mano, **no compilado en este entorno** (requiere `dotnet restore` con acceso a NuGet) |
| 5 proyectos de test de backend | ⚠️ Escritos, **no ejecutados** aquí (misma razón) |
| Frontend Angular | ✅ `npm install --legacy-peer-deps`, `ng build --configuration production` y `ng test` verificados en este entorno |
| Despliegue real a Vercel/Azure-o-MonsterASP/Atlas | ⚠️ Configurado (`vercel.json`, `deploy-api.yml`, `environment.production.ts`) pero **no ejecutado**: eso requiere que tú crees las cuentas reales y conectes el repositorio |

**La primera tarea al recibir este proyecto en tu máquina (con acceso normal a internet) es:**

```bash
cd backend && dotnet build Shekinah.sln
```

y resolver cualquier error de compilación que NuGet hubiera revelado. El Domain ya está probado;
es la superficie más grande (Application/Infrastructure/Api) la que necesita esa primera pasada.

## Estructura del repositorio

```
shekinah-next/
├── backend/                  # .NET 10 — Clean Architecture + DDD
│   ├── src/
│   │   ├── Shekinah.Domain/          # Cero dependencias NuGet (R3)
│   │   ├── Shekinah.Application/     # CQRS propio (sin MediatR), casos de uso, puertos
│   │   ├── Shekinah.Infrastructure/  # MongoDB, JWT, BCrypt, Outbox de correo (Gmail SMTP), Excel/CSV
│   │   ├── Shekinah.Api/             # Minimal APIs agrupadas por módulo
│   │   └── Shekinah.Migrator/        # Herramienta de migración MySQL → MongoDB (parcial, ver abajo)
│   └── tests/                # 5 proyectos: Domain, Application, Architecture, Infrastructure, Api
├── frontend/                  # Angular 21 standalone + zoneless + signals
│   ├── vercel.json            # config de despliegue a Vercel
│   ├── src/environments/      # environment.ts (dev, relativo) / environment.production.ts (URL real de la API)
│   └── src/app/
│       ├── core/              # http, auth (SignalStore), guards
│       ├── shared/            # componentes ui reutilizables, SubSink base
│       ├── layouts/           # PublicShell, AdminShell
│       └── features/
│           ├── public/        # home, planes, catálogo, contacto, admisión
│           └── admin/         # login, dashboard, inscripciones, usuarios, académico,
│                               # calificaciones, mis-materias, mi-perfil, pagos, cambiar-password
├── docs/                      # DECISIONS.md, API.md, RUNBOOK.md
├── .github/workflows/         # ci.yml (build+test) y deploy-api.yml (Azure App Service)
└── .env.example               # referencia de las variables de entorno reales (no se lee directo)
```

## Arrancar en desarrollo local (menos de 5 comandos)

Necesitas MongoDB instalado localmente (sin Docker — instala el binario de MongoDB Community
directo en tu máquina) corriendo en `mongodb://localhost:27017/`.

Backend:
```bash
cd backend
dotnet dev-certs https --trust   # una sola vez: genera y confía en el certificado local de desarrollo
dotnet build Shekinah.sln
cp src/Shekinah.Api/appsettings.Development.json.example src/Shekinah.Api/appsettings.Development.json
# edita el archivo con tu Jwt:SigningKey, EmailSettings:SenderAppPassword y Seed:AdminPassword
dotnet run --project src/Shekinah.Api
# perfil "https" (Properties/launchSettings.json): https://localhost:5081 y http://localhost:5080
```

Frontend:
```bash
cd frontend
npm install --legacy-peer-deps   # el flag es necesario: ver docs/DECISIONS.md
npx ng serve                     # usa proxy.conf.json → https://localhost:5081
```

## Desplegar a producción

Resumen (ver `docs/RUNBOOK.md` para el paso a paso completo):

1. **MongoDB Atlas**: crea un clúster M0 gratuito, copia la cadena SRV, y ponla como
   `Mongo__ConnectionString` en el panel de tu host de API.
2. **API → Azure App Service (F1 gratuito) o MonsterASP.NET**: publica `backend/src/Shekinah.Api`
   (el workflow `.github/workflows/deploy-api.yml` ya está listo para Azure; solo falta el secreto
   `AZURE_WEBAPP_PUBLISH_PROFILE` en tu repositorio de GitHub). Configura ahí mismo
   `Jwt__SigningKey`, `EmailSettings__SenderAppPassword` y `Seed__AdminPassword`.
3. **Frontend → Vercel**: conecta el repositorio; Vercel usa `frontend/vercel.json` automáticamente.
   Antes de publicar, edita `frontend/src/environments/environment.production.ts` con la URL real
   de tu API desplegada.

## Reglas de oro del proyecto (resumen)

- **R3**: `Shekinah.Domain` no tiene ni un solo `<PackageReference>`.
- **CQRS propio**: sin MediatR (licenciamiento) — ver `Application/Abstractions/Dispatcher.cs`.
- **`Result<T>`** para fallas de negocio esperadas; excepciones solo para lo inesperado.
- **R7 (Angular)**: toda suscripción manual a un Observable se registra en un `SubSink`. Prioridad:
  `signal > toSignal > async pipe > rxMethod > subscribe+SubSink`.
- **R8**: ningún secreto real se sube al repositorio (`appsettings.Development.json` está en
  `.gitignore`; en producción los secretos van en las variables de entorno del panel del host).
- **RN-01 a RN-24**: reglas de negocio numeradas del spec técnico, cada una con al menos una prueba
  con nombre explícito en `Shekinah.Domain.UnitTests` (ver cobertura real en
  [`docs/DECISIONS.md`](docs/DECISIONS.md)).

## Qué falta / alcance honesto

- El `Migrator` (MySQL → MongoDB) lee `Preregistros` y `Usuarios` de la base legada; los lectores
  de las demás tablas (pagos, materias, grupos) están marcados como `TODO` explícito en
  `Legacy/LegacyMySqlReader.cs` — no hay un `NotImplementedException` oculto, el dry-run actual
  reporta claramente qué procesó y qué no.
- El interceptor de refresh de token (`refreshInterceptor`) cierra sesión ante un 401 en vez de
  reintentar automáticamente con el refresh token; el flujo completo con cola de peticiones en
  vuelo queda documentado como siguiente paso en `docs/DECISIONS.md`.
- No se configuró ESLint en este entorno (conflicto de peer-deps de `@angular/cdk` con Angular 21.2
  en el momento de construir esto); el proyecto ya tiene TypeScript en modo estricto
  (`noImplicitAny`, `noUncheckedIndexedAccess`, etc.) como primera línea de defensa.
- Los límites reales de MonsterASP.NET (disco, ancho de banda, duración del plan gratuito) no están
  documentados públicamente — confírmalos dentro de tu panel antes de decidirte por esa opción en
  vez de Azure App Service F1.

## Documentación

- [`docs/DECISIONS.md`](docs/DECISIONS.md) — ADRs, incluyendo las 4 decisiones cerradas del spec y
  las que surgieron al construir esto.
- [`docs/API.md`](docs/API.md) — contrato de la API por módulo.
- [`docs/RUNBOOK.md`](docs/RUNBOOK.md) — operación: seed, backups, despliegue, troubleshooting.
