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
| POST | `/admissions/applications` | Público. Envía una solicitud de admisión. RN-02/RN-03 validan modalidad/región en servidor. Devuelve `{ applicationId, folio }`. |
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
| GET | `/academic/offerings?periodId` | Grupos ofertados. |
| POST 🔒👑 Admin | `/academic/offerings` | Asigna profesor a una materia/región (dispara RN-15 auto-inscripción). |
| DELETE 🔒👑 Admin | `/academic/offerings/{id}` | |
| POST 🔒👑 Admin | `/academic/offerings/auto-enroll` | Re-ejecuta la auto-inscripción manualmente. |
| GET | `/academic/offerings/{id}/enrollments` | Alumnos inscritos + calificación actual. |
| PUT 🔒👑 Teacher/Admin | `/academic/offerings/{id}/enrollments/{studentId}/grade` | `{ grade }` 0-10. RN-16. |

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
