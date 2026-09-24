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
}

export interface SubmitApplicationRequestDto {
  fullName: string;
  birthDate: string;
  maritalStatus: string;
  email: string;
  phone: string;
  street: string;
  neighborhood: string;
  locality: string;
  municipality: string;
  state: string;
  churchName: string;
  churchStreet: string;
  churchNeighborhood: string;
  churchLocality: string;
  churchMunicipality: string;
  pastorName: string;
  timeAttending: string;
  hasMinistryRole: boolean;
  ministryRoleName: string | null;
  educationLevel: SchoolingLevelDto;
  otherEducationDescription: string | null;
  theologicalBackground: string;
  studyPurpose: string;
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
  birthDate: string;
  maritalStatus: string;
  email: string;
  phone: string;
  street: string;
  neighborhood: string;
  locality: string;
  municipality: string;
  state: string | null;
  churchName: string;
  churchStreet: string;
  churchNeighborhood: string;
  churchLocality: string;
  churchMunicipality: string;
  pastorName: string;
  timeAttending: string;
  hasMinistryRole: boolean;
  ministryRoleName: string | null;
  educationLevel: SchoolingLevelDto;
  otherEducationDescription: string | null;
  theologicalBackground: string;
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
  email: string;
  role: UserRole;
  status: string;
  regionName: string | null;
  modality: ModalityDto | null;
  currentTerm: number | null;
  plan: StudyPlanDto | null;
}

/** Alumnos → "Agregar alumno" → formulario manual. */
export interface CreateStudentRequestDto {
  fullName: string;
  email: string;
  phone: string;
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
  teacherName: string;
  enrollmentCount: number;
}

export interface EnrollmentRowDto {
  studentId: string;
  enrollmentNumber: number;
  studentName: string;
  grade: number | null;
  status: 'Active' | 'Dropped';
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
  sentTo: string;
  whatsAppPhone: string;
  whatsAppMessage: string;
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
}

export interface KardexResponseDto {
  studentId: string;
  enrollmentNumber: number;
  fullName: string;
  email: string;
  regionName: string | null;
  modality: ModalityDto | null;
  currentTerm: number | null;
  enrolledAtUtc: string;
  isGraduated: boolean;
  averageGrade: number | null;
  subjects: KardexSubjectRowDto[];
}
