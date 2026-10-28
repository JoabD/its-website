# AGENTS.md — guía para asistentes de IA (Copilot, Claude, u otro)

Este archivo existe para que, si cambias de asistente de IA a mitad de este proyecto, el nuevo
asistente no pierda el hilo. Está escrito para ser leído por una IA, no por una persona — es denso
a propósito. Antes de tocar código en este repositorio, lee este archivo completo.

Si usas GitHub Copilot, también existe `.github/copilot-instructions.md` — es un archivo corto que
Copilot carga automáticamente y que apunta de vuelta a este documento, para no mantener dos copias
del mismo contenido.

## 0. Qué es este proyecto

Migración del sistema legado PHP/MySQL del Instituto Teológico Shekinah (ITS) a **.NET 10 (C# 14)**
+ Clean Architecture/DDD sobre **MongoDB**, con frontend **Angular 21** (standalone, zoneless,
signals). Construido a partir de `PROMPT-MAESTRO.md` y `ESPECIFICACION-TECNICA.md` (documentos del
cliente, no siempre presentes en todos los checkouts del repo — si no los encuentras, trata
`README.md` + `docs/DECISIONS.md` como la fuente de verdad disponible).

**Documento a leer primero, siempre, en este orden:**

1. `README.md` — arquitectura, estructura de carpetas, qué está realmente verificado (build/test)
   y qué no, cómo levantar el entorno local, cómo se despliega.
2. `docs/DECISIONS.md` — el registro de decisiones de arquitectura (ADR-001 a la fecha). **Esto es
   lo más importante de este archivo**: cada decisión no obvia tomada en este proyecto está
   documentada aquí con su contexto, la decisión y las consecuencias. Si vas a cambiar algo que ya
   tiene un ADR, lee ese ADR completo antes de tocarlo — probablemente existe una razón de negocio
   o una restricción del cliente detrás, no solo una preferencia técnica.
3. `docs/API.md` — contrato de endpoints por módulo, agrupado igual que `AcademicEndpoints.cs` y
   sus hermanos.
4. `docs/RUNBOOK.md` — operación: seed de datos, backups, despliegue, troubleshooting.

**El ADR más reciente y más relevante para seguir trabajando en el módulo académico/calificaciones
es ADR-015** (`docs/DECISIONS.md`). Léelo antes de tocar cualquier cosa relacionada con
`CourseOffering`, `grading-roster`, "Calificar por alumno", o promoción de alumnos — ahí está el
porqué de que `CourseOffering.Teacher` sea nullable y de cómo funciona la elegibilidad de
promoción.

## 1. Cultura de este proyecto: "alcance honesto"

Esta es la regla de comportamiento más importante, más que cualquier regla técnica de abajo.

Este proyecto se ha construido en sesiones con herramientas de distinta capacidad (algunas con
acceso a `dotnet build`/`npm test` reales, otras sin acceso a NuGet, por ejemplo). **Nunca afirmes
que algo está compilado, probado o verificado si no lo ejecutaste tú mismo en esta sesión.** El
README y cada ADR distinguen explícitamente:

- ✅ "verificado" = se ejecutó un comando real (`dotnet build`, `dotnet test`, `ng build`, `ng
  test`) en el entorno donde se escribió el cambio, y el resultado se registró.
- ⚠️ "código completo, revisado a mano, no compilado/probado en este entorno" = se escribió con
  cuidado (se revisaron tipos, se grep-eó por call sites afectados, se verificó balance de
  llaves/paréntesis si no había compilador disponible) pero nadie lo ejecutó.

Si terminas un cambio sin poder compilarlo, dilo explícitamente en tu respuesta al usuario y en el
ADR/commit correspondiente — nunca lo disfraces de "listo". El cliente de este proyecto confía en
que se le diga la verdad sobre el estado del código, incluso cuando la verdad es "no sé si
compila".

## 2. Reglas de oro (no negociables sin discutirlo con el cliente)

- **R3 — Domain sin dependencias**: `Shekinah.Domain` no tiene ni un solo `<PackageReference>` a
  ningún paquete NuGet, **incluyendo el propio driver de MongoDB**. Por eso el mapeo
  BsonDocument↔dominio es manual (`ToBson`/`ToDomain` en cada repositorio de `Infrastructure`), no
  vía `BsonClassMap` — ver ADR-006. Si vas a persistir un campo nuevo de un agregado, el patrón es:
  añade el campo al agregado con una factory/método con nombre (nunca un setter público suelto), y
  actualiza el `ToBson`/`ToDomain` del repositorio correspondiente a mano.
- **CQRS propio, no MediatR**: por licenciamiento (MediatR v11+ requiere licencia comercial en uso
  empresarial). El dispatcher propio vive en `Shekinah.Application/Abstractions/Dispatcher.cs`, con
  un pipeline `IPipelineBehavior<TRequest,TResponse>` en este orden fijo: **Logging → Authorization
  → Validation → Transaction → Handler**. Un caso de uso nuevo es un `record` que implementa
  `ICommand<TResponse>`/`IQuery<TResponse>` (+ `ITransactionalCommand` si escribe a Mongo y necesita
  atomicidad), su `Handler`, y opcionalmente un `[RequireRole(...)]` en el record del comando.
- **`Result<T>`/`Error` para fallas de negocio esperadas**: una validación de negocio que falla
  (alumno no elegible, oferta duplicada, etc.) devuelve `Result.Failure(Error.X(...))`, nunca lanza
  una excepción. Las excepciones son solo para lo verdaderamente inesperado (bug, infraestructura
  caída). Esto se traduce automáticamente a HTTP vía `ResultExtensions.ToApiResult()` según
  `ErrorType` (Validation→400, NotFound→404, Conflict→409, Forbidden→403, Unauthorized→401,
  Failure→500).
- **R7 — disciplina de suscripciones en Angular**: prioridad estricta
  `signal > toSignal > async pipe > rxMethod > subscribe+SubSink`. Toda suscripción manual a un
  Observable (el último recurso de la lista) se registra en un `SubSink` (patrón propio del
  proyecto, no una librería) para no fugar memoria. No se usa NgRx ni Signal Forms (ver ADR-008 —
  Signal Forms no era estable en Angular 21.2 al construir esto; si ya es estable cuando leas esto,
  confírmalo con el cliente antes de migrar, no asumas).
- **R8 — sin secretos reales en el repositorio**: `appsettings.Development.json` está en
  `.gitignore`. En producción, los secretos (`Jwt__SigningKey`, `EmailSettings__SenderAppPassword`,
  `Seed__AdminPassword`) van como variables de entorno en el panel del host (Azure App
  Service/MonsterASP.NET), nunca hardcodeados ni en `appsettings.json` versionado.
- **Sin Docker** (ADR-012, decisión explícita del cliente, no técnica): el despliegue real es
  Vercel (frontend) + Azure App Service o MonsterASP.NET (API) + MongoDB Atlas M0 (base de datos).
  El CI (`ci.yml`) usa Testcontainers solo como herramienta de testing en el runner de GitHub
  Actions — eso no contradice "sin Docker", que se refiere al mecanismo de despliegue a producción.
  No reintroduzcas `docker-compose.yml`/Dockerfiles a menos que el cliente lo pida explícitamente de
  nuevo.
- **RN-01 a RN-24**: reglas de negocio numeradas del spec técnico del cliente. Cada una debería
  tener al menos una prueba con nombre explícito (`RN_XX_Descripcion`) en
  `Shekinah.Domain.UnitTests`. El estado real de cobertura (cuáles tienen prueba y cuáles no) está
  documentado en ADR-009 — no asumas que las 24 están cubiertas sin revisar esa tabla primero.

## 3. Dónde está cada cosa (mapa rápido)

```
backend/src/
  Shekinah.Domain/          # agregados, value objects, políticas puras — CERO NuGet
  Shekinah.Application/     # casos de uso (CQRS), puertos (interfaces de repos/servicios externos)
  Shekinah.Infrastructure/  # MongoDB (mapeo manual), JWT, BCrypt, Outbox de correo (Gmail SMTP), Excel/CSV
  Shekinah.Api/             # Minimal APIs agrupadas por módulo (*Endpoints.cs), Program.cs
  Shekinah.Migrator/        # migración MySQL → MongoDB, parcial (ver README "qué falta")
backend/tests/              # Domain, Application, Architecture, Infrastructure, Api (5 proyectos)
frontend/src/app/
  core/                     # http client (ApiClient), auth (SignalStore), guards, interceptors
  shared/                   # componentes UI reutilizables (ButtonComponent, CardComponent...), SubSink base
  layouts/                  # PublicShell, AdminShell
  features/public/          # home, planes, catálogo, contacto, admisión (sin auth)
  features/admin/           # login, dashboard, inscripciones, usuarios, académico, calificaciones,
                             # mis-materias, mi-perfil, pagos, cambiar-password
docs/                       # DECISIONS.md (ADRs), API.md (contrato), RUNBOOK.md (operación)
```

Módulo "académico" (el más activo recientemente — calificaciones, promoción, ofertas de materia):
backend en `Shekinah.Application/Academics/*` (un folder por caso de uso) +
`Shekinah.Api/Endpoints/AcademicEndpoints.cs`; frontend en
`frontend/src/app/features/admin/academic/` (gestión de ofertas/profesores, promoción masiva) y
`frontend/src/app/features/admin/grades/` (grades.component.ts — "Calificar por alumno", incluye
el drawer con "Avanzar de cuatrimestre/semestre").

## 4. Cómo trabajar en este repo con un asistente de IA (restricciones del entorno)

Estas sesiones normalmente no tienen una terminal con `dotnet build`/`npm test` reales conectada
directamente al checkout del cliente — el acceso al código es a través de un bridge a la máquina
del cliente (en Claude, vía herramientas `mcp__remote-devices__*`: listar, "stage" el archivo a un
entorno de trabajo, editar ahí, "commit" de vuelta). Si tu entorno es distinto (por ejemplo,
GitHub Copilot corriendo directo sobre el checkout local del cliente con acceso a terminal), esta
sección no aplica tal cual, pero el principio sí: **confirma qué puedes ejecutar realmente antes de
afirmar que algo está compilado o probado**, y dilo explícitamente si no puedes (sección 1).

Si no tienes compilador/test runner disponible:

- Antes de dar por terminado un cambio en C#, haz un grep manual de cualquier sitio que llame al
  miembro que acabas de cambiar de forma incompatible (ejemplo real: al volver `CourseOffering.
  Teacher` nullable, un grep de `\.Teacher\.` y `\.Teacher\b` en todo `backend/src` encontró 4
  call sites que hubieran lanzado NullReferenceException — los 4 se corrigieron antes de "terminar"
  el cambio; ver ADR-015).
- Verifica manualmente el balance de llaves/paréntesis/corchetes de cada archivo que edites a mano
  (un heurístico simple en Python basta) antes de darlo por bueno — un error de sintaxis silencioso
  es el fallo más caro de no detectar sin compilador.
- Déjalo anotado como no verificado (sección 1) en tu respuesta final y, si el cambio es
  arquitectónicamente significativo, en un nuevo ADR.

## 5. Convención de documentación continua (este archivo, los ADRs, y Copilot)

- **Nuevo ADR** cuando tomes una decisión no obvia (algo que no es simplemente "seguir el patrón
  existente"): añade una entrada `## ADR-0XX — Título` en `docs/DECISIONS.md`, siguiendo
  exactamente el formato de las entradas anteriores — **Contexto** (qué problema o pedido del
  cliente lo motivó, citando sus palabras cuando sea relevante), **Decisión** (qué se hizo, con
  nombres de archivo/clase reales), **Consecuencias** (qué queda pendiente, qué riesgo residual
  hay, qué debe revisar quien continúe). No reescribas ADRs anteriores salvo que el cliente pida
  deshacer esa decisión explícitamente — en ese caso, añade un ADR nuevo que referencie y
  reemplace al anterior, no edites el viejo en silencio.
- **`docs/API.md`** debe reflejar cada endpoint nuevo o cambiado en el mismo cambio que lo
  introduce — no lo dejes para "después", porque "después" es exactamente cuando se vuelve
  obsoleto (esto ya pasó una vez en este proyecto: el módulo académico creció varios endpoints
  antes de que alguien actualizara esta tabla).
- **Este archivo (`AGENTS.md`)** se actualiza cuando cambie algo de las secciones 0-3 (estructura,
  reglas de oro, mapa de carpetas) — no para cada ADR nuevo, que vive en `docs/DECISIONS.md`.
- **`.github/copilot-instructions.md`** es deliberadamente corto y no debe duplicar contenido de
  este archivo — si necesitas añadir contexto para Copilot específicamente, hazlo aquí y deja que
  ese archivo siga apuntando aquí.

## 6. Antes de escribir código: checklist rápido

1. ¿Ya hay un ADR sobre esto o algo relacionado? Lee `docs/DECISIONS.md` (usa buscar por palabra
   clave del dominio: "promoción", "Teacher", "Mongo", "transacción", etc.).
2. ¿El cambio toca un agregado del Domain? Recuerda R3 — ni un `using` a un paquete NuGet ahí, y
   cambios de estado solo vía métodos con nombre, nunca setters públicos nuevos.
3. ¿El cambio toca persistencia? Actualiza el `ToBson`/`ToDomain` del repositorio a mano — no hay
   serialización automática.
4. ¿El cambio es un caso de uso nuevo? Sigue el patrón `record` + `Handler` + `[RequireRole]` +
   `ITransactionalCommand` si aplica, y añade su fila a `docs/API.md` si expone un endpoint.
5. ¿Pudiste compilar/probar de verdad? Si no, dilo explícitamente al terminar (sección 1) y haz el
   grep manual de call sites afectados (sección 4).
6. ¿La decisión que tomaste no es obvia / alguien podría preguntarse "por qué así"? Escribe el ADR
   antes de cerrar la tarea, no después.
