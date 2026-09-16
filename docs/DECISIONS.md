# DECISIONS.md — Architecture Decision Records

Formato: contexto → decisión → consecuencias. Las decisiones marcadas **(spec)** ya venían cerradas
en `PROMPT-MAESTRO.md` §11; el resto surgió al construir este entregable.

---

## ADR-001 — Versiones reales instaladas

**Contexto**: el spec pide .NET 10 / C# 14 y Angular 21, pero ambos ecosistemas publican parches
constantemente.

**Decisión**: se fijaron las versiones realmente instaladas y verificadas en el entorno de
construcción:

| Componente | Versión verificada |
|---|---|
| .NET SDK | 10.0.112 |
| Angular CLI / core | 21.2.24 / 21.2.0 |
| Node.js | 22.22.2 |
| MongoDB.Driver | 3.1.0 |
| MongoDB (servidor, desarrollo y Atlas) | 8.0 |

**Consecuencias**: cualquier `dotnet restore` futuro puede traer parches menores; si algo no
compila tras un `restore` limpio, lo primero a revisar es un cambio de comportamiento entre
parches, no un error de este código.

---

## ADR-002 — Esquema legado (spec)

**Contexto**: la base MySQL legada mezcla convenciones (nombres en español, claves foráneas
implícitas por convención de nombre, sin normalización de teléfono/email).

**Decisión**: el `Migrator` lee el esquema legado tal cual (`Legacy/LegacyModels.cs` refleja los
nombres de columna originales) y transforma explícitamente hacia los Value Objects del dominio
(`Email`, `PhoneNumber`, `PersonName`) en la capa de mapeo (`Mapping/AdmissionApplicationMapper.cs`),
nunca dentro del dominio mismo.

**Consecuencias**: el dominio permanece ignorante del esquema legado; el costo de esa ignorancia lo
paga el `Migrator`, que es desechable una vez completada la migración real.

---

## ADR-003 — RN-15 y `AutoEnrollmentPolicy` (spec)

**Contexto**: el sistema legado auto-inscribe alumnos a los grupos de su cuatrimestre/región al
abrir un periodo, pero la regla exacta de elegibilidad (cuatrimestre actual, modalidad, región)
solo existía como comportamiento observado, no como especificación escrita.

**Decisión**: se reconstruyó la regla como una política de dominio pura y con nombre propio,
`Academics/Policies/AutoEnrollmentPolicy.cs`, separada del caso de uso `AutoEnroll` que la invoca.
Esto permite que la regla tenga sus propias pruebas unitarias sin infraestructura
(`RN_15_Elegibilidad_por_cuatrimestre_y_region`, `RN_15_Enroll_es_idempotente`).

**Consecuencias**: si la regla real de negocio difiere de lo reconstruido, el cambio se hace en un
solo archivo con pruebas que documentan el comportamiento esperado.

---

## ADR-004 — Datos de cobranza (spec)

**Contexto**: los pagos legados se importan por lote (Excel/CSV) y no hay pasarela de pago en
línea; la "matriz de pagos" es un tablero de solo lectura por alumno × mes.

**Decisión**: `PaymentImportBatch` registra cada fila importada junto con sus errores
(`PaymentImportRowError`), nunca falla el lote completo por una fila inválida. `DelinquencyPolicy`
calcula meses adeudados de forma pura (sin efectos secundarios) a partir de `AcademicPeriodMonthsCalculator`
(RN-17) y del historial de pagos.

**Consecuencias**: una importación parcialmente inválida es visible y auditable, no un
todo-o-nada silencioso.

---

## ADR-005 — Regiones (spec)

**Contexto**: la modalidad "Presencial" depende de una región geográfica que el solicitante elige;
"Virtual" y "Diplomado" no tienen región real pero el legado guardaba un valor arbitrario.

**Decisión**: `RegionAssignmentPolicy` (RN-03) fuerza una región nula/lógica para Online/Diploma y
exige que la región elegida en Onsite pertenezca al `modalityScope` de esa región. La validación
vive en el dominio, no en el formulario Angular (que solo filtra las opciones visibles para UX).

---

## ADR-006 — Mapeo manual BsonDocument ↔ dominio (nueva)

**Contexto**: el spec técnico §3.3-D exige "cero setters públicos" en los agregados del dominio, y
el dominio no debe tener ninguna referencia a MongoDB (ni siquiera un atributo `[BsonId]`). El
driver oficial de MongoDB normalmente serializa objetos vía `BsonClassMap` + reflexión sobre
constructores/setters públicos.

**Decisión**: en vez de usar `BsonClassMap` (que hubiera forzado constructores públicos sin
argumentos o setters, rompiendo la regla de oro), cada repositorio en `Infrastructure/Persistence/`
escribe mapeo manual explícito `ToBson(...)` / `ToDomain(...)`, y el dominio expone factories
públicas con nombre (`Rehydrate`, `CreateStudentFromApplication`, etc.) para reconstruirse desde
datos ya validados.

**Consecuencias**: más código de mapeo escrito a mano, pero el dominio queda genuinamente aislado
de MongoDB — se podría cambiar de motor de persistencia sin tocar `Shekinah.Domain`. El costo se
paga una vez por agregado, no por cada campo nuevo.

---

## ADR-007 — CQRS propio en vez de MediatR (nueva, pero exigida por el spec)

**Contexto**: el spec prohíbe MediatR por licenciamiento (desde v11 requiere licencia comercial en
uso empresarial).

**Decisión**: `Application/Abstractions/Dispatcher.cs` implementa un dispatcher basado en
reflexión + `IServiceProvider`, con el mismo contrato conceptual (`ICommand`/`IQuery`/handlers) y
un pipeline de `IPipelineBehavior<TRequest,TResponse>` (logging, autorización, validación,
transacción) registrado vía Scrutor.

**Consecuencias**: el dispatcher es más simple que MediatR (sin `INotification`/pub-sub interno);
los eventos de dominio se despachan aparte, vía el Outbox, no a través de este mediator.

---

## ADR-008 — Reactive Forms tipados en vez de Signal Forms (spec técnico §6.5)

**Contexto**: Angular tiene un RFC de "Signal Forms" en desarrollo, pero no es estable en la
versión 21.2 usada aquí.

**Decisión**: se usa `NonNullableFormBuilder` + `FormGroup<T>` tipado explícitamente
(`admission-form.model.ts` es el caso más complejo: 5 grupos, ~30 campos, validaciones
condicionales).

**Consecuencias**: cuando Signal Forms se estabilice, la migración es localizada a los archivos
`*.model.ts` + plantillas; los stores y servicios no dependen de la forma de los formularios.

---

## ADR-009 — Cobertura real de pruebas de reglas de negocio (nueva, alcance honesto)

**Contexto**: el spec numera 24 reglas de negocio (RN-01 a RN-24), cada una exigiendo al menos una
prueba con nombre explícito.

**Estado real** (verificable en `backend/tests/Shekinah.Domain.UnitTests/`):

Cubiertas con prueba nombrada: RN-01, RN-02, RN-03, RN-05, RN-06, RN-07, RN-13, RN-14, RN-15,
RN-16, RN-17, RN-18, RN-19, RN-24.

No cubiertas todavía por una prueba unitaria del dominio (quedan como siguiente paso, no como
`NotImplementedException` oculto): RN-04, RN-08 a RN-12, RN-20 a RN-23. Varias de estas
(por ejemplo RN-08, "la autorización se evalúa siempre en servidor") sí están implementadas en
código — `AuthorizationBehavior` en Application, `[Authorize]` en los endpoints — pero su prueba
natural es de integración (`Shekinah.Api.FunctionalTests`), no del dominio puro, y esa suite no se
pudo ejecutar en este entorno sin acceso a NuGet.

**Consecuencias**: se prioriza declarar el estado real sobre inflar el conteo de "24/24 reglas
probadas" — la siguiente sesión de trabajo con acceso a NuGet debe correr
`dotnet test` en las 5 suites y cerrar los huecos listados arriba.

---

## ADR-010 — Interceptor de refresh de token simplificado (nueva, alcance honesto)

**Contexto**: el spec pide un access token de 15 minutos + refresh token rotativo de 7 días, con
manejo automático de la renovación ante un 401.

**Decisión temporal**: `refreshInterceptor` (frontend) cierra sesión de forma segura ante cualquier
401 fuera de `/auth/login`, en vez de reintentar con el refresh token y una cola de peticiones en
vuelo.

**Consecuencias**: el usuario tiene que volver a iniciar sesión cada vez que el access token expira
(cada 15 minutos de inactividad de red), en vez de una renovación transparente. El backend ya
expone `POST /auth/refresh`; conectar el interceptor a ese endpoint es la mejora inmediata más
valiosa para la siguiente iteración.

---

## ADR-011 — `Migrator` cubre 2 de las tablas legadas (nueva, alcance honesto)

**Contexto**: la base legada tiene aproximadamente 6 tablas relevantes (preregistros, usuarios,
pagos, materias, grupos, calificaciones).

**Decisión**: se implementaron los lectores de `Preregistros` y `Usuarios` con su mapeo completo a
`AdmissionApplication`/`User`; los lectores restantes están marcados `// TODO` explícitamente en
`Legacy/LegacyMySqlReader.cs`, y el `Program.cs` del Migrator corre en modo dry-run (reporta lo que
migraría sin escribir a Mongo todavía).

**Consecuencias**: el Migrator no está listo para una migración de producción real; es un
esqueleto funcional con el patrón correcto (lector → mapper → reporte de reconciliación) para
completar las tablas restantes siguiendo el mismo molde.

---

## ADR-012 — Sin Docker: despliegue a Vercel + Azure App Service/MonsterASP.NET + MongoDB Atlas (decisión del cliente)

**Contexto**: la primera versión de este proyecto incluía `docker-compose.yml` y Dockerfiles para
backend y frontend como mecanismo de arranque y despliegue. El cliente indicó explícitamente que no
quiere Docker y que el destino real de producción es: **Vercel** (frontend), **Azure App Service o
MonsterASP.NET** (API, ambos con capa gratuita) y **MongoDB Atlas** (base de datos, capa gratuita
M0).

**Decisión**: se eliminó todo el andamiaje de Docker (`docker-compose.yml`, `backend/Dockerfile`,
`frontend/Dockerfile`, `frontend/nginx.conf`, `infra/mongo-init/`). En su lugar: `frontend/vercel.json`
para el build estático en Vercel, `.github/workflows/deploy-api.yml` para publicar la API a Azure
App Service, y `frontend/src/environments/` (`environment.ts` / `environment.production.ts`) para
que la API sepa a qué URL apuntar según el entorno — antes la URL era una ruta relativa fija
(`/api/v1`) que solo funcionaba porque nginx/el proxy de desarrollo colocaban la API en el mismo
origen; en Vercel el frontend y la API viven en dominios distintos, así que esa ruta relativa ya no
alcanza.

**Consecuencias**: el CI (`ci.yml`) sigue usando Testcontainers en el job de integración (que
internamente arranca un contenedor Mongo efímero en el runner de GitHub Actions), pero eso es una
herramienta de testing, no el mecanismo de despliegue del proyecto — no hay contradicción con "sin
Docker" en el sentido de producción. Un desarrollador que de todas formas prefiera un entorno local
en contenedores puede crear su propio `docker-compose.yml` fuera de este repositorio (no versionado
aquí), pero eso no es parte del contrato ni de los criterios de aceptación de ninguna fase.

---

## ADR-013 — Cadena de conexión a MongoDB por entorno

**Contexto**: el cliente pidió explícitamente que la cadena de conexión de desarrollo se vea
exactamente como `mongodb://localhost:27017/`, sin decorar con `?replicaSet=rs0`, a pesar de que el
proyecto exige transacciones multi-documento (RN-05, RN-14) que normalmente requieren un replica
set.

**Decisión**: se usa esa cadena tal cual en `appsettings.json` para desarrollo. Esto es seguro
porque el driver oficial `MongoDB.Driver` 3.x detecta la topología real del servidor al conectar
(comando `hello`), así que si el `mongod` de destino ya es parte de un replica set, las
transacciones funcionan igual sin que la URL lo declare explícitamente. En producción, la cadena
real es la SRV que entrega MongoDB Atlas para el clúster M0 — que además ya es, por diseño, un
replica set de 3 nodos, sin configuración adicional de nuestra parte.

**Consecuencias**: un desarrollador que instale un `mongod` **standalone** local (sin `--replSet`)
verá fallar las transacciones con `MongoCommandException: Transaction numbers are only allowed on a
replica set member` — documentado en `docs/RUNBOOK.md` con las dos alternativas (replica set local
de un nodo, o apuntar directo a un clúster Atlas de desarrollo).

---

## ADR-014 — Gmail SMTP con credenciales explícitas del cliente

**Contexto**: el cliente entregó credenciales SMTP de Gmail reales para usar en el envío de correo
del sistema (RN-04, RN-05, RN-06, RN-19): host, puerto, cuenta remitente, contraseña de aplicación,
nombre para mostrar y una dirección de copia por defecto.

**Decisión**: se reemplazó la sección de configuración genérica `Smtp` (pensada originalmente para
MailHog en desarrollo) por `EmailSettings`, con exactamente los campos y valores que dio el cliente:
`SmtpHost`, `SmtpPort`, `SenderEmail`, `SenderAppPassword`, `SenderDisplayName`, `DefaultCcAddress`.
`OutboxProcessor` ahora autentica siempre contra Gmail (usuario + contraseña de aplicación) y usa
STARTTLS en el puerto 587. Se agregó un puerto nuevo, `INotificationRecipients`, para que
`Application` pueda pedir la dirección de copia administrativa sin depender de Infrastructure
directamente (Clean Architecture, R3); `SubmitApplicationCommandHandler` (RN-04) es el único punto
que la usa, porque es el único correo que es genuinamente "administrativo" — los correos de
credenciales (RN-05), rechazo (RN-06) y morosidad (RN-19) son personales del alumno y **no** llevan
copia, para no filtrar sus datos a otra bandeja.

**Consecuencias**: el valor real de `SenderAppPassword` nunca se escribe en un archivo versionado
(R8): vive en `appsettings.Development.json` (git-ignored) en local, y en la variable de entorno
`EmailSettings__SenderAppPassword` del panel de Azure/MonsterASP en producción. `appsettings.json`
(el que sí se versiona) trae el resto de los valores en claro porque no son secretos — el correo
remitente y el nombre para mostrar son información pública de todas formas — pero deja
`SenderAppPassword` vacío a propósito.
