# API.md — Contrato de la API

Base URL: `/api/v1`. Todas las respuestas de error siguen RFC 9457 (`ProblemDetailsDto` en
`frontend/src/app/api/schema.ts`). Los endpoints marcados 🔒 requieren `Authorization: Bearer
<accessToken>`; los marcados 🔒👑 además requieren un rol específico (validado siempre en servidor
vía `AuthorizationBehavior`, nunca solo en el frontend — RN-08).

Documentación interactiva real: una vez la API está corriendo, Scalar sirve el OpenAPI generado en
`/scalar/v1` (paquete `Scalar.AspNetCore`, ver `Program.cs`). Este documento es un mapa de alto
nivel, no reemplaza esa fuente generada desde el código.

## Auth (`/auth`)

| Método | Ruta | Descripción |
|---|---|---|
| POST | `/auth/login` | `{ enrollmentNumber, password }` → `LoginResponseDto` (access + refresh token). RN-07. |
| POST | `/auth/refresh` | Rota el refresh token. |
| POST 🔒 | `/auth/logout` | Invalida la sesión actual. |
| POST 🔒 | `/auth/change-password` | `{ currentPassword, newPassword }`. RN-24: obligatorio si `mustChangePassword=true`. |
| GET 🔒 | `/auth/me` | `CurrentUserDto` del usuario autenticado. |

## Admisiones (`/admissions`)

| Método | Ruta | Descripción |
|---|---|---|
| POST | `/admissions/applications` | Público. Envía una solicitud de un paso: `{ fullName, email, phone, modality, requestedRegionId, churchName?, area?, community?, municipality?, state?, studyPurpose?, recaptchaToken }`. Nombre, correo, teléfono, modalidad y reCAPTCHA son obligatorios; `requestedRegionId` es obligatorio para modalidad presencial. Devuelve `{ applicationId, folio }`. |
| GET 🔒👑 Admin | `/admissions/applications?status&search&page&pageSize` | Bandeja paginada. |
| GET 🔒👑 Admin | `/admissions/applications/{id}` | Detalle completo. |
| POST 🔒👑 Admin | `/admissions/applications/{id}/approve` | RN-01: una solicitud decidida es inmutable. |
| POST 🔒👑 Admin | `/admissions/applications/{id}/reject` | `{ reason }`. |

## Catálogo (`/catalog`)

| Método | Ruta | Descripción |
|---|---|---|
| GET | `/catalog/regions?modality` | Público. Filtra por modalidad si se envía. |
| GET | `/catalog/curriculum` | Público. Materias por cuatrimestre/diplomado, con `hasSyllabus`. RN-20. |
| GET 🔒👑 Admin | `/catalog/subjects` | Listado administrable. |
| POST 🔒👑 Admin | `/catalog/subjects` | Alta de materia. |
| PUT 🔒👑 Admin | `/catalog/subjects/{id}/syllabus` | Publica/actualiza el PDF del temario. |
| DELETE 🔒👑 Admin | `/catalog/subjects/{id}` | Baja de materia. |

## Académico (`/academic`) 🔒

| Método | Ruta | Descripción |
|---|---|---|
| GET | `/academic/periods?page&pageSize` | |
| GET | `/academic/periods/current` | El periodo con estatus `Active` (RN-13: solo puede haber uno). |
| POST 🔒👑 Admin | `/academic/periods` | Abre un nuevo periodo. |
| GET | `/academic/offerings?periodId` | Grupos ofertados. Una oferta sin profesor todavía ("pendiente", ver ADR-015) aparece con `teacherName: "Sin profesor asignado"`. |
| POST 🔒👑 Admin | `/academic/offerings` | Asigna profesor a una materia/región. Si ya existe una oferta pendiente para esa materia/región/periodo, la completa con el profesor en vez de rechazarla como duplicada (RN-22). Dispara RN-15 auto-inscripción. |
| DELETE 🔒👑 Admin | `/academic/offerings/{id}` | |
| POST 🔒👑 Admin | `/academic/offerings/auto-enroll` | Re-ejecuta la auto-inscripción manualmente. |
| GET | `/academic/offerings/{id}/enrollments` | Alumnos inscritos + calificación actual. |
| PUT 🔒👑 Teacher/Admin | `/academic/offerings/{id}/enrollments/{studentId}/grade` | `{ grade }` 0-10. RN-16. Si la oferta no tiene profesor ("pendiente"), solo Admin puede. |
| PUT 🔒👑 Teacher/Admin | `/academic/offerings/{id}/enrollments/grades` | `{ grades: [{studentId, grade}] }` — captura en lote de un grupo completo ("Guardar todos los cambios"). |
| GET | `/academic/grades/import/template?periodId&plan&termNumber&regionId` | Plantilla Excel con TODAS las materias de un cuatrimestre/semestre+región. |
| POST 🔒👑 Admin | `/academic/grades/import?periodId&plan&termNumber&regionId` | `multipart/form-data`. Importación por lote, tolerante a errores por fila. |
| GET 🔒👑 Teacher/Admin | `/academic/grading-roster?periodId` | "Calificar por alumno": `{studentId, totalSubjects, gradedSubjects}` por alumno en TODO el periodo — `totalSubjects` es el currículo completo de su plan+término (ADR-015), no solo lo que ya tiene oferta. Evita N+1 en la tabla. |
| GET | `/academic/students/{studentId}/offerings?periodId` | "Calificar por alumno": materias del alumno en un periodo. Las que todavía no tienen oferta aparecen con `offeringId: null` y `status: "SinProfesor"` (fallback defensivo — normalmente ya no pasa, ver `ensure-offerings`). |
| POST 🔒👑 Admin | `/academic/students/{studentId}/ensure-offerings` | `{ periodId }`. ADR-015: crea (idempotente) una oferta "pendiente" (sin profesor) por cada materia del currículo del alumno que todavía no tenga oferta, y lo inscribe. Se llama al abrir la ficha del alumno, antes de pedir `/offerings`. |
| PUT 🔒 Teacher/Admin | `/academic/offerings/students/{studentId}/grades` | `{ periodId, grades: [{offeringId, grade}] }` — captura en lote de TODAS las materias de un alumno ("Guardar calificaciones de este alumno"). |
| GET 🔒 Teacher/Admin | `/academic/students/{studentId}/grades/import/template?periodId` | Plantilla Excel solo de las materias de ESE alumno. |
| POST 🔒 Teacher/Admin | `/academic/students/{studentId}/grades/import?periodId` | `multipart/form-data`. Importación por alumno. |
| POST 🔒👑 Admin | `/academic/students/{studentId}/promote` | ADR-015: promueve a UN alumno, solo si ya aprobó TODAS sus materias del término actual (`Grade.IsPassing`, ≥7) — si no, devuelve `{promoted:false, reason}` sin error. "Avanzar de cuatrimestre/semestre" en la ficha del alumno. |
| POST 🔒👑 Admin | `/academic/students/promote` | `{ plan, fromTerm }`. Promueve de un jalón a TODOS los alumnos activos de ese plan+término que cumplan la misma regla de elegibilidad que el endpoint anterior; al resto los cuenta en `studentsSkipped` sin tocarlos (ADR-015, antes promovía a ciegas). |

## Alumno (`/students/me`) 🔒👑 Student

| Método | Ruta | Descripción |
|---|---|---|
| GET | `/students/me/courses` | Materias del periodo activo con su calificación (`null` = sin calificar, nunca `0`). |
| GET | `/students/me/profile` | |
| PUT | `/students/me/profile` | RN-23: solo datos de contacto propios. |

## Usuarios (`/users`) 🔒👑 Admin

| Método | Ruta | Descripción |
|---|---|---|
| GET | `/users?role&regionId&search&page&pageSize` | |
| POST | `/users` | Alta (staff o alumno fuera del flujo de admisión). |
| PUT | `/users/{id}` | Actualiza rol/alcance/estatus. |
| POST | `/users/{id}/reset-password` | Genera contraseña temporal + `mustChangePassword=true`. |

## Pagos (`/payments`) 🔒

| Método | Ruta | Descripción |
|---|---|---|
| GET | `/payments/matrix?periodId&regionId&page&pageSize` | Matriz alumno × mes. 🔒👑 Admin/RegionalCoordinator. |
| POST 🔒👑 Admin | `/payments/import` | `multipart/form-data`, Excel/CSV. Nunca falla el lote completo por una fila inválida (ver `PaymentImportBatch`). |
| GET 🔒👑 Admin | `/payments/import/{batchId}` | Estado + errores del lote. |
| POST 🔒👑 Admin | `/payments/notices` | Emite avisos de adeudo (RN-19: bloquea al cuarto aviso). |
| GET 🔒👑 Admin | `/payments/notices?studentId&page&pageSize` | Historial de avisos. |

## Errores

Todo error de negocio (`Result.Failure`) se traduce a un `ProblemDetailsDto` con el `status` HTTP
correspondiente al `ErrorType` del dominio (`Validation`→400, `NotFound`→404, `Conflict`→409,
`Forbidden`→403, `Unauthorized`→401, `Failure`→500). Ver `Api/Extensions/ResultExtensions.cs` y el
middleware `GlobalExceptionHandler` para excepciones no controladas.
