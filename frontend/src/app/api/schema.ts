/**
 * Tipos generados desde el OpenAPI del backend (ESPECIFICACION-TECNICA.md §6.2).
 *
 * En un entorno con la API corriendo, este archivo se regenera con:
 *   npm run api:types   →  openapi-typescript http://localhost:5080/openapi/v1.json -o src/app/api/schema.ts
 *
 * Este archivo NO se edita a mano en un proyecto en marcha. Como este entregable se construyó sin
 * un backend en ejecución contra el cual generar el contrato, se transcribió a mano el contrato
 * documentado en PROMPT-MAESTRO.md §7 — la primera tarea real al integrar backend+frontend es
 * regenerarlo de verdad y dejar que cualquier divergencia rompa la compilación (ese es el objetivo).
 */

export type UserRole = 'Student' | 'Teacher' | 'Administrator' | 'RegionalCoordinator' | 'RegionalSecretary';
export type ModalityDto = 'Onsite' | 'Online' | 'Diploma';
export type ApplicationStatusDto = 'Pending' | 'Approved' | 'Rejected';
export type SchoolingLevelDto = 'Primary' | 'Secondary' | 'HighSchool' | 'Other';
export type StudyPlanDto = 'Quarterly' | 'Semester';
export type ProgramTypeDto = 'Quarterly' | 'Diploma';

export interface PagedResultDto<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalItems: number;
  totalPages: number;
  months?: string[];
}

export interface ProblemDetailsDto {
  type?: string;
  title: string;
  status: number;
  detail: string;
  traceId?: string;
  errors?: Record<string, string[]>;
}

export interface LoginResponseDto {
  accessToken: string;
  accessTokenExpiresAtUtc: string;
  refreshToken: string;
  userId: string;
  role: UserRole;
  mustChangePassword: boolean;
}

export interface RefreshTokenResponseDto {
  accessToken: string;
  accessTokenExpiresAtUtc: string;
  refreshToken: string;
}

export interface CurrentUserDto {
  id: string;
  enrollmentNumber: number;
  role: UserRole;
  /** Roles adicionales al principal (2026-10, "un usuario puede tener múltiples roles") — hoy solo
   * se usa para Teacher (ver User.ExtraRoles en el backend). */
  extraRoles: UserRole[];
  fullName: string;
  email: string;
  regionName: string | null;
  modality: ModalityDto | null;
  currentTerm: number | null;
  mustChangePassword: boolean;
}

export interface RegionListItemDto {
  id: string;
  code: number;
  name: string;
  modalityScope: ModalityDto[];
}

export interface CurriculumSubjectDto {
  id: string;
  code: string;
  name: string;
  programType: 'Quarterly' | 'Diploma';
  termNumber: number | null;
  displayOrder: number;
  hasSyllabus: boolean;
  syllabusPdfUrl: string | null;
  /** Cuatrimestral (Virtual) o Semestral (Presencial) — a qué currículo pertenece esta materia
   * (plan de control escolar, fase "flujo de asignación", 2026-10). Determina en qué grupos de
   * Académico puede ofertarse. */
  plan: StudyPlanDto;
}

/** Configuración → Catálogo de materias (2026-10, pedido explícito del cliente: "el admin debe
 * también poder agregar una materia nueva al plan que sea y del número de cuatrimestre o semestre
 * que sea"). Distinto de CreateOfferingRequestDto: esto da de alta la materia en el catálogo (qué
 * existe), no asigna profesor/región (eso sigue siendo la pantalla Materias). `plan` es obligatorio
 * salvo para Diplomado, que siempre es Cuatrimestral. */
export interface CreateSubjectRequestDto {
  code: string;
  name: string;
  programType: ProgramTypeDto;
  termNumber: number | null;
  displayOrder: number;
  plan: StudyPlanDto;
}

export interface SubmitApplicationRequestDto {
  fullName: string;
  email: string;
  phone: string;
  churchName?: string | null;
  area?: string | null;
  community?: string | null;
  municipality?: string | null;
  state?: string | null;
  studyPurpose?: string | null;
  modality: ModalityDto;
  requestedRegionId: string | null;
  onlineReason: string | null;
  recaptchaToken: string;
}

export interface SubmitApplicationResponseDto {
  applicationId: string;
  folio: string;
}

export interface ApplicationListItemDto {
  id: string;
  folio: string;
  applicantName: string;
  modality: ModalityDto;
  regionName: string;
  status: ApplicationStatusDto;
  submittedAtUtc: string;
  /** Fecha en la que la purga automática (30 días tras decidir) la elimina; null mientras Pending. */
  purgeScheduledAtUtc: string | null;
}

/** Catálogo vivo (Configuración → Documentos de inscripción): un ítem por documento activo. */
export interface ChecklistItemStateDto {
  id: string;
  label: string;
  checked: boolean;
}

export interface ApplicationChecklistDto {
  items: ChecklistItemStateDto[];
  isComplete: boolean;
}

export interface ApplicationDetailDto {
  id: string;
  folio: string;
  fullName: string;
  email: string;
  phone: string;
  area: string;
  community: string;
  municipality: string;
  state: string | null;
  churchName: string;
  studyPurpose: string;
  modality: ModalityDto;
  regionName: string;
  onlineReason: string | null;
  status: ApplicationStatusDto;
  submittedAtUtc: string;
  decisionReason: string | null;
  decidedAtUtc: string | null;
  isDeletable: boolean;
  approvedViaQuickAction: boolean;
  purgeScheduledAtUtc: string | null;
  checklist: ApplicationChecklistDto;
}

export interface UpdateChecklistRequestDto {
  checkedItemIds: string[];
}

export interface ApproveApplicationRequestDto {
  viaQuickAction: boolean;
}

export interface ApproveApplicationResponseDto {
  userId: string;
  enrollmentNumber: number;
  matricula: string;
  temporaryPassword: string;
}

/** Configuración → Documentos de inscripción (catálogo editable del checklist). */
export interface ChecklistItemListItemDto {
  id: string;
  label: string;
  displayOrder: number;
  isActive: boolean;
}

export interface CreateChecklistItemRequestDto {
  label: string;
}

export interface UpdateChecklistItemRequestDto {
  label: string;
  displayOrder: number;
}

export interface SetChecklistItemActiveRequestDto {
  isActive: boolean;
}

/** Configuración → Regiones (vista/edición administrativa completa). */
export interface RegionAdminListItemDto {
  id: string;
  code: number;
  name: string;
  abbreviation: string;
  modalityScope: ModalityDto[];
  isActive: boolean;
}

export interface UpdateRegionRequestDto {
  modalityScope: ModalityDto[];
  abbreviation: string;
}

export interface UserListItemDto {
  id: string;
  enrollmentNumber: number;
  matricula: string | null;
  fullName: string;
  email: string | null;
  phone: string | null;
  role: UserRole;
  /** Ver CurrentUserDto.extraRoles — permite mostrar en "Docentes" a alguien cuyo rol principal
   * no es Teacher (p. ej. un Administrador que también da clases). */
  extraRoles: UserRole[];
  status: string;
  regionName: string | null;
  modality: ModalityDto | null;
  currentTerm: number | null;
  plan: StudyPlanDto | null;
  /** Regiones donde esta persona da clases (2026-10, "un profesor puede ser asignado a varias
   * regiones") — solo relevante cuando role es Teacher o extraRoles incluye Teacher. */
  teachingRegionNames: string[];
}

/** Panel de Usuarios → "Agregar usuario": alta de cualquier rol de staff (no alumnos — esos van
 * por Alumnos → "Agregar alumno", que usa su propio flujo con matrícula/plan/cuatrimestre). Correo
 * y teléfono opcionales (mismo ajuste que CreateStudentRequestDto): un Profesor recién agregado
 * puede no tener correo todavía — queda de alta pero sin acceso al sistema hasta que se capture.
 * birthDate ya NO es obligatoria (2026-10, pedido explícito del cliente) — se reemplazó por
 * regionIds, obligatorio solo cuando role es Teacher ("un profesor puede ser asignado a varias
 * regiones"). */
export interface CreateUserRequestDto {
  role: UserRole;
  fullName: string;
  email?: string | null;
  phone?: string | null;
  birthDate?: string | null;
  regionId?: string | null;
  modality?: ModalityDto | null;
  regionIds?: string[] | null;
}

export interface CreateUserResponseDto {
  userId: string;
  enrollmentNumber: number;
  temporaryPassword: string;
}

/** Cambia rol/región/estatus de un usuario YA existente (UpdateUserCommand) — a diferencia de
 * AdminUpdateUserRequestDto (que edita nombre/correo/teléfono/contraseña), este endpoint es el que
 * usa "Docentes" → "Asignar como profesor" para convertir en Profesor a alguien que ya tiene cuenta
 * en el sistema, sin crear un usuario duplicado. El modelo de dominio tiene un solo rol por usuario
 * (no listas de roles): asignar "Teacher" aquí REEMPLAZA el rol anterior, no lo añade. */
export interface UpdateUserRequestDto {
  role: UserRole;
  regionId?: string | null;
  modality?: ModalityDto | null;
  status?: string | null;
}

/** "Docentes" → "Ya tiene cuenta" (2026-10, pedido explícito del cliente: "no necesitamos revocar
 * ni un rol... un usuario puede tener múltiples roles"). A diferencia de UpdateUserRequestDto (que
 * SÍ reemplaza el rol principal), este agrega Teacher como rol ADICIONAL sin tocar nada más del
 * usuario — ver POST /api/v1/users/{id}/teacher-role. */
export interface AssignTeacherRoleRequestDto {
  regionIds: string[];
}

/** Panel de Usuarios → editar (side panel): nombre/correo/teléfono + contraseña nueva opcional
 * (elegida por el admin, no temporal — dejar vacía conserva la contraseña vigente). */
export interface AdminUpdateUserRequestDto {
  fullName: string;
  email: string;
  phone: string;
  newPassword?: string | null;
}

/** Panel de Usuarios → restablecer contraseña (admin, no el propio usuario — ver ResetPasswordCommandHandler). */
export interface ResetPasswordResponseDto {
  temporaryPassword: string;
}

/** Alumnos → "Agregar alumno" → formulario manual. Correo y teléfono opcionales (ajuste de flujo
 * real: no siempre se tienen al capturar al alumno) — sin correo, el alumno queda registrado pero
 * sin acceso al sistema por ahora; sin teléfono, se queda sin la opción de "enviar por WhatsApp". */
export interface CreateStudentRequestDto {
  fullName: string;
  email?: string | null;
  phone?: string | null;
  birthDate: string;
  regionId: string;
  modality: ModalityDto;
  plan: StudyPlanDto;
  currentTerm: number;
}

export interface CreateStudentResponseDto {
  userId: string;
  enrollmentNumber: number;
  matricula: string;
  temporaryPassword: string;
}

/** Alumnos → "Agregar alumno" → importar Excel. */
export interface StudentImportRowErrorDto {
  rowNumber: number;
  code: string;
  message: string;
  rawValues: string[];
}

export interface StudentImportBatchDto {
  id: string;
  fileName: string;
  status: 'Processing' | 'Completed' | 'Failed';
  totalRows: number;
  importedRows: number;
  errors: StudentImportRowErrorDto[];
}

export interface ImportStudentsResponseDto {
  batchId: string;
  totalRows: number;
  importedRows: number;
  errors: StudentImportRowErrorDto[];
}

/** Alumnos → "Agregar alumno" → pestaña Excel → "Descargar plantilla". Mismo patrón que
 * SendPaymentReceiptResponseDto: el .xlsx viaja en base64 dentro del JSON (no un <a href> plano)
 * porque el endpoint requiere el header Authorization, así que la descarga se dispara desde JS. */
export interface StudentImportTemplateResponseDto {
  fileName: string;
  contentBase64: string;
}

export interface PeriodListItemDto {
  id: string;
  code: string;
  name: string;
  startsOnUtc: string;
  endsOnUtc: string;
  status: 'Active' | 'Closed';
}

export interface OfferingListItemDto {
  id: string;
  subjectName: string;
  regionName: string;
  regionId: string;
  teacherName: string;
  enrollmentCount: number;
  termNumber: number | null;
  plan: StudyPlanDto;
}

/** Académico → "Nueva asignación": crea el grupo (profesor + materia + región + periodo) que
 * antes no existía ningún formulario para crear — ver AssignTeacherCommand (backend, ya existía
 * sin UI). Es el paso que faltaba antes de poder "asignar materias automáticamente": sin un grupo
 * creado para una materia/región, no hay nada en qué auto-inscribir alumnos (plan de control
 * escolar, fase "flujo de asignación", 2026-10). */
export interface CreateOfferingRequestDto {
  periodId: string;
  subjectId: string;
  regionId: string;
  teacherId: string;
}

export interface EnrollmentRowDto {
  studentId: string;
  enrollmentNumber: number;
  studentName: string;
  grade: number | null;
  status: 'Active' | 'Dropped';
}

/** "Promover alumnos" (2026-10, pedido explícito del cliente). Independiente de abrir/cerrar un
 * periodo académico — ver PromoteStudentsCommand (backend). */
export interface PromoteStudentsRequestDto {
  plan: StudyPlanDto;
  fromTerm: number;
}

export interface PromoteStudentsResponseDto {
  studentsPromoted: number;
  studentsGraduated: number;
  enrollmentsCreated: number;
  /** Ignorados (2026-10, pedido explícito del cliente): alumnos con alguna materia reprobada o sin
   * calificar — no se promueven, pero tampoco cuentan como error. */
  studentsSkipped: number;
}

/** "Calificar por alumno" → "Avanzar de cuatrimestre/semestre" (2026-10, pedido explícito del
 * cliente): promueve a UN alumno, solo si ya aprobó todas sus materias — ver PromoteStudentCommand
 * (backend). */
export interface PromoteStudentResponseDto {
  promoted: boolean;
  graduated: boolean;
  reason: string | null;
}

/** "Calificar por alumno" (2026-10, pedido explícito del cliente): todas las materias (ofertas) de
 * un alumno en un periodo, para capturar su calificación de cada una en un solo lugar. */
export interface StudentOfferingRowDto {
  offeringId: string | null;
  subjectName: string;
  regionName: string;
  termNumber: number | null;
  grade: number | null;
  status: 'Active' | 'Dropped' | 'SinProfesor';
}

export interface StudentSubjectGradeEntryDto {
  offeringId: string;
  grade: number;
}

export interface StudentSubjectGradeErrorDto {
  offeringId: string;
  subjectName: string;
  code: string;
  message: string;
}

export interface RecordStudentGradesBatchResponseDto {
  saved: number;
  errors: StudentSubjectGradeErrorDto[];
}

export interface StudentCourseRowDto {
  offeringId: string;
  subjectName: string;
  teacherName: string;
  regionName: string;
  periodCode: string;
  grade: number | null;
}

export interface StudentPaymentRowDto {
  studentId: string;
  enrollmentNumber: number;
  studentName: string;
  regionName: string;
  email: string;
  phone: string;
  paidByMonth: Record<string, boolean>;
  monthsDue: string[];
}

/** Panel de verificación de pagos: periodo activo (docs/Plan-Panel-Pagos.md). */
export interface CurrentPeriodResponseDto {
  id: string;
  code: string;
  name: string;
  startsOnUtc: string;
  endsOnUtc: string;
  monthCodes: string[];
  /** Todos los meses del cuatrimestre (sin tope de "hasta hoy") — usado por el panel de pagos para dejar marcar meses futuros. */
  allMonthCodes: string[];
}

/** Panel de verificación de pagos, fase 1: respuesta al enviar un recibo (email automático + datos para WhatsApp + PDF para descarga). */
export interface SendPaymentReceiptResponseDto {
  sentTo: string | null;
  whatsAppPhone: string | null;
  whatsAppMessage: string | null;
  pdfBase64: string;
  fileName: string;
}

/** Panel de verificación de pagos, sección 8: resumen financiero de un mes, por región. */
export interface BillingSummaryRegionRowDto {
  regionId: string;
  regionName: string;
  activeStudents: number;
  paidCount: number;
  dueCount: number;
  expectedTotal: number;
  collectedTotal: number;
}

export interface BillingSummaryResponseDto {
  monthCode: string;
  activeStudents: number;
  paidCount: number;
  dueCount: number;
  expectedTotal: number;
  collectedTotal: number;
  collectionRatePercent: number;
  byRegion: BillingSummaryRegionRowDto[];
}

/** Fase 6 del plan de control escolar: avisos institucionales. */
export interface AnnouncementListItemDto {
  id: string;
  title: string;
  body: string;
  publishedAtUtc: string;
}

/** Fase 7 del plan de control escolar: eventos del calendario institucional, público. */
export interface CalendarEventDto {
  id: string;
  title: string;
  description: string | null;
  startAtUtc: string;
  endAtUtc: string | null;
  regionId: string | null;
  regionName: string | null;
}

/** Fase 8 del plan de control escolar: Kardex académico del alumno. */
export interface KardexSubjectRowDto {
  subjectName: string;
  termNumber: number | null;
  grade: number | null;
  status: string;
  periodCode: string;
  /** Oferta (grupo) a la que pertenece esta fila — permite capturar/corregir la calificación
   * directamente desde el detalle del alumno (plan de control escolar, fase "flujo de asignación",
   * 2026-10), llamando a PUT /academic/offerings/{offeringId}/enrollments/{studentId}/grade. */
  offeringId: string | null;
}

/** Plan de control escolar / Kardex, fase 2: resumen de un cuatrimestre/semestre cursado —
 * promedio y estatus de aprobación (mínimo 7, materias "Baja" excluidas). No bloquea la promoción
 * al siguiente periodo; es únicamente informativo (RN confirmada con el cliente). */
export interface KardexTermGroupDto {
  termNumber: number;
  averageGrade: number | null;
  isApproved: boolean;
  hasPendingSubject: boolean;
  subjects: KardexSubjectRowDto[];
}

export interface KardexResponseDto {
  studentId: string;
  enrollmentNumber: number;
  fullName: string;
  email: string | null;
  regionName: string | null;
  modality: ModalityDto | null;
  currentTerm: number | null;
  enrolledAtUtc: string;
  isGraduated: boolean;
  averageGrade: number | null;
  subjects: KardexSubjectRowDto[];
  terms: KardexTermGroupDto[];
  hasPendingSubjects: boolean;
}

/** Kardex → Calificaciones → "Guardar todos los cambios" (captura manual en lote). */
export interface GradeEntryDto {
  studentId: string;
  grade: number;
}

export interface GradeEntryErrorDto {
  studentId: string;
  code: string;
  message: string;
}

export interface RecordGradesBatchResponseDto {
  saved: number;
  errors: GradeEntryErrorDto[];
}

/** Kardex → Calificaciones → "Importar desde Excel": una plantilla cubre todas las materias de un
 * (periodo, plan, cuatrimestre/semestre, región). */
export interface GradeImportTemplateResponseDto {
  fileName: string;
  contentBase64: string;
}

export interface GradeImportCellErrorDto {
  rowNumber: number;
  subjectName: string;
  code: string;
  message: string;
}

export interface ImportGradesResponseDto {
  totalRows: number;
  totalCells: number;
  importedCells: number;
  errors: GradeImportCellErrorDto[];
}

/** Rediseño de "Calificar por alumno" (2026-10): avance de cada alumno en el periodo activo, para
 * pintar la tabla sin una llamada por alumno. Un alumno que no aparece aquí no tiene ninguna
 * materia asignada en este periodo (0/0 — "Sin materias"). */
export interface StudentGradingSummaryRowDto {
  studentId: string;
  totalSubjects: number;
  gradedSubjects: number;
}

export interface ImportStudentGradesResponseDto {
  importedCells: number;
  errors: GradeImportCellErrorDto[];
}
