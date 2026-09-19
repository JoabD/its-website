/**
 * Modelos de vista (ESPECIFICACION-TECNICA.md §6.2): derivados de los tipos generados, con tipos
 * afinados e identificadores "branded" para no confundir un StudentId con un OfferingId.
 * Regla dura: ningún componente de features/ importa directamente desde api/schema — siempre pasa
 * por un mapper explícito (core/*.mappers.ts) hacia estos tipos.
 */

export type StudentId = string & { readonly __brand: 'StudentId' };
export type OfferingId = string & { readonly __brand: 'OfferingId' };
export type RegionId = string & { readonly __brand: 'RegionId' };
export type PeriodId = string & { readonly __brand: 'PeriodId' };

export function asStudentId(value: string): StudentId {
  return value as StudentId;
}

export function asOfferingId(value: string): OfferingId {
  return value as OfferingId;
}

export type UserRole = 'Student' | 'Teacher' | 'Administrator' | 'RegionalCoordinator' | 'RegionalSecretary';
export type Modality = 'Onsite' | 'Online' | 'Diploma';
export type ApplicationStatus = 'Pending' | 'Approved' | 'Rejected';
export type StudyPlan = 'Quarterly' | 'Semester';

export const MODALITY_LABELS: Record<Modality, string> = {
  Onsite: 'Presencial',
  Online: 'Virtual',
  Diploma: 'Diplomado',
};

/**
 * Plan de estudios del alumno (distinto de la clasificación de materias del currículo):
 * Cuatrimestral (RN de producto: toda solicitud pública aprobada entra así, cuatrimestre 1) o
 * Semestral (solo asignable al dar de alta manualmente o por Excel). Por ahora es solo
 * clasificación/reporte — no determina qué materias se asignan.
 */
export const STUDY_PLAN_LABELS: Record<StudyPlan, string> = {
  Quarterly: 'Cuatrimestral',
  Semester: 'Semestral',
};

export const ROLE_LABELS: Record<UserRole, string> = {
  Student: 'Alumno',
  Teacher: 'Profesor',
  Administrator: 'Administrador',
  RegionalCoordinator: 'Coordinador regional',
  RegionalSecretary: 'Secretario regional',
};

export const APPLICATION_STATUS_LABELS: Record<ApplicationStatus, string> = {
  Pending: 'Pendiente',
  Approved: 'Aprobada',
  Rejected: 'Rechazada',
};

export interface CurrentUser {
  readonly id: string;
  readonly enrollmentNumber: number;
  readonly role: UserRole;
  readonly fullName: string;
  readonly email: string;
  readonly regionName: string | null;
  readonly modality: Modality | null;
  readonly currentTerm: number | null;
  readonly mustChangePassword: boolean;
}

export interface StudentCourseRow {
  readonly offeringId: OfferingId;
  readonly subjectName: string;
  readonly teacherName: string;
  readonly regionName: string;
  readonly periodCode: string;
  /** null = sin calificar; NUNCA undefined ni 0 como "vacío" (spec técnico §6.2). */
  readonly grade: number | null;
}

export interface ApiError {
  readonly title: string;
  readonly detail: string;
  readonly status: number;
}
