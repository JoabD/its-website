import { Component, computed, inject, signal } from '@angular/core';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { combineLatest, of } from 'rxjs';
import { catchError, map, switchMap, tap } from 'rxjs/operators';
import { ApiClient } from '../../../core/http/api-client';
import {
  CurrentPeriodResponseDto,
  EnrollmentRowDto,
  GradeEntryDto,
  GradeImportTemplateResponseDto,
  ImportGradesResponseDto,
  ImportStudentGradesResponseDto,
  OfferingListItemDto,
  PagedResultDto,
  RecordGradesBatchResponseDto,
  RecordStudentGradesBatchResponseDto,
  RegionListItemDto,
  StudentGradingSummaryRowDto,
  StudentOfferingRowDto,
  StudentSubjectGradeEntryDto,
  StudyPlanDto,
  UserListItemDto,
} from '../../../api/schema';
import { STUDY_PLAN_LABELS } from '../../../domain/models';
import { CardComponent } from '../../../shared/ui/card/card.component';
import { ButtonComponent } from '../../../shared/ui/button/button.component';
import { BadgeComponent } from '../../../shared/ui/badge/badge.component';
import { ToastService } from '../../../shared/ui/toast/toast.service';

const PASSING_GRADE = 7;

/**
 * RN-16: captura de calificaciones (0-10). Dos vías, tal como se pidió: manual por grupo (guardado
 * en lote, "Guardar todos los cambios") e importación por Excel (una plantilla cubre TODAS las
 * materias de un cuatrimestre/semestre+región a la vez — plan de control escolar / Kardex, fase 2).
 * El campo de captura manual sigue aceptando decimales (step 0.1) a propósito: se decidió no
 * tocar ese comportamiento existente aunque el valor real que se guarda sea entero 0-10.
 */
@Component({
  selector: 'shk-grades',
  imports: [FormsModule, CardComponent, ButtonComponent, BadgeComponent],
  template: `
    <h1 class="text-2xl font-bold text-slate-900">Calificaciones</h1>

    <shk-card class="mt-6">
      <label class="flex flex-col gap-1 text-sm md:w-96">
        <span class="font-medium text-slate-700">Grupo (captura manual)</span>
        <select class="shk-field" [ngModel]="selectedOfferingId()" (ngModelChange)="selectOffering($event)">
          <option value="" disabled>Selecciona un grupo</option>
          @for (offering of offerings(); track offering.id) {
            <option [value]="offering.id">{{ offering.subjectName }} — {{ offering.regionName }} ({{ offering.plan === 'Semester' ? 'Semestral' : 'Cuatrimestral' }} {{ offering.termNumber }})</option>
          }
        </select>
      </label>
    </shk-card>

    @if (selectedOfferingId()) {
      <shk-card class="mt-6 pb-20 sm:pb-6">
        <div class="flex flex-wrap items-center justify-between gap-3">
          <div class="flex gap-4 text-sm">
            <div><span class="text-slate-400">Promedio del grupo:</span> <b>{{ groupAverage() ?? '—' }}</b></div>
            <div><span class="text-slate-400">Aprobados:</span> <b class="text-emerald-600">{{ approvedCount() }} / {{ enrollments().length }}</b></div>
            <div><span class="text-slate-400">Pendientes:</span> <b class="text-amber-600">{{ pendingCount() }}</b></div>
          </div>
          <!-- Botón normal en desktop/tablet; en celular vive fijo abajo (ver barra sticky más abajo) -->
          <shk-button class="hidden sm:inline-flex" variant="primary" [loading]="savingBatch()" [disabled]="dirtyIds().size === 0" (click)="saveAll()">
            Guardar todos los cambios ({{ dirtyIds().size }})
          </shk-button>
        </div>

        <!-- Tabla: desktop/tablet (sm+) -->
        <table class="mt-4 hidden w-full text-left text-sm sm:table">
          <thead class="text-slate-500"><tr><th class="py-2">Matrícula</th><th class="py-2">Alumno</th><th class="py-2">Calificación</th><th class="py-2">Estatus</th></tr></thead>
          <tbody>
            @for (row of enrollments(); track row.studentId) {
              <tr class="border-t border-slate-100">
                <td class="py-2 font-medium">{{ row.enrollmentNumber }}</td>
                <td class="py-2">{{ row.studentName }}</td>
                <td class="py-2">
                  <input
                    type="number" min="0" max="10" step="0.1" class="shk-field w-24"
                    [ngModel]="draftGrades()[row.studentId]"
                    (ngModelChange)="onGradeChange(row.studentId, $event)"
                  />
                </td>
                <td class="py-2">
                  @if (statusFor(draftGrades()[row.studentId]); as status) {
                    <shk-badge [tone]="status.tone">{{ status.label }}</shk-badge>
                  }
                </td>
              </tr>
            } @empty {
              <tr><td colspan="4" class="py-6 text-center text-slate-400">Sin alumnos inscritos.</td></tr>
            }
          </tbody>
        </table>

        <!-- Tarjetas: solo celular (sm:hidden) — una por alumno, con el campo de calificación a la
             derecha, igual que el mockup (columna A alumno / captura al lado, sin scroll horizontal). -->
        <div class="mt-4 space-y-2 sm:hidden">
          @for (row of enrollments(); track row.studentId) {
            <div class="flex items-center justify-between gap-3 rounded-xl border border-slate-100 px-3 py-2.5">
              <div class="min-w-0">
                <div class="truncate text-sm font-medium text-slate-800">{{ row.studentName }}</div>
                <div class="text-xs text-slate-400">Matrícula {{ row.enrollmentNumber }}</div>
                @if (statusFor(draftGrades()[row.studentId]); as status) {
                  <shk-badge class="mt-1 inline-block" [tone]="status.tone">{{ status.label }}</shk-badge>
                }
              </div>
              <input
                type="number" min="0" max="10" step="0.1" class="shk-field w-16 shrink-0 text-center"
                [ngModel]="draftGrades()[row.studentId]"
                (ngModelChange)="onGradeChange(row.studentId, $event)"
              />
            </div>
          } @empty {
            <p class="py-6 text-center text-sm text-slate-400">Sin alumnos inscritos.</p>
          }
        </div>
      </shk-card>

      <!-- Barra de guardado fija al fondo — solo celular, igual patrón que el resto del panel admin
           (ver admin-shell: pb-20 en <main> ya deja espacio para esto + la barra de pestañas). -->
      <div class="fixed inset-x-0 bottom-14 z-20 border-t border-slate-200 bg-white/95 px-4 py-3 backdrop-blur sm:hidden">
        <shk-button class="w-full justify-center" variant="primary" [loading]="savingBatch()" [disabled]="dirtyIds().size === 0" (click)="saveAll()">
          Guardar todos los cambios ({{ dirtyIds().size }})
        </shk-button>
      </div>
    }

    <shk-card class="mt-6">
      <h2 class="font-semibold text-[var(--shk-color-primary)]">Calificar por alumno</h2>
      <p class="mt-1 text-sm text-slate-500">
        Busca por región, plan y cuatrimestre/semestre, y abre la ficha de un alumno para calificar — a mano o importando un Excel solo de él.
      </p>

      <div class="mt-4 grid gap-3 sm:grid-cols-4">
        <label class="flex flex-col gap-1 text-sm">
          <span class="font-medium text-slate-700">Región</span>
          <select class="shk-field" [ngModel]="rosterRegion()" (ngModelChange)="rosterRegion.set($event)">
            <option value="Todas">Todas las regiones</option>
            @for (name of regionOptions(); track name) {
              <option [value]="name">{{ name }}</option>
            }
          </select>
        </label>
        <label class="flex flex-col gap-1 text-sm">
          <span class="font-medium text-slate-700">Plan</span>
          <select class="shk-field" [ngModel]="rosterPlan()" (ngModelChange)="rosterPlan.set($event)">
            <option value="Todos">Todos los planes</option>
            <option value="Quarterly">Cuatrimestral</option>
            <option value="Semester">Semestral</option>
          </select>
        </label>
        <label class="flex flex-col gap-1 text-sm">
          <span class="font-medium text-slate-700">Cuatrimestre/semestre</span>
          <select class="shk-field" [ngModel]="rosterTerm()" (ngModelChange)="rosterTerm.set($event)">
            <option value="Todos">Todos</option>
            @for (n of [1, 2, 3, 4, 5, 6]; track n) {
              <option [value]="n">{{ n }}</option>
            }
          </select>
        </label>
        <label class="flex flex-col gap-1 text-sm">
          <span class="font-medium text-slate-700">Buscar</span>
          <input
            type="search"
            placeholder="Nombre o matrícula…"
            class="shk-field"
            [ngModel]="studentSearchText()"
            (ngModelChange)="studentSearchText.set($event)"
          />
        </label>
      </div>

      <div class="mt-3 text-sm text-slate-500">{{ rosterRows().length }} alumno(s) encontrado(s).</div>

      <table class="mt-3 hidden w-full text-left text-sm sm:table">
        <thead class="text-slate-500">
          <tr>
            <th class="py-2">Alumno</th>
            <th class="py-2">Matrícula</th>
            <th class="py-2">Región</th>
            <th class="py-2">Plan</th>
            <th class="py-2">Cuatrimestre/semestre</th>
            <th class="py-2">Materias calificadas</th>
            <th class="py-2"></th>
          </tr>
        </thead>
        <tbody>
          @for (row of rosterRows(); track row.id) {
            <tr class="border-t border-slate-100">
              <td class="py-2 font-medium">{{ row.fullName }}</td>
              <td class="py-2 text-slate-500">{{ row.enrollmentNumber }}</td>
              <td class="py-2">{{ row.regionName }}</td>
              <td class="py-2">{{ row.planLabel }}</td>
              <td class="py-2">{{ row.termLabel }}</td>
              <td class="py-2">
                @if (row.hasSubjects) {
                  <shk-badge tone="success">{{ row.graded }}/{{ row.total }} calificadas</shk-badge>
                } @else {
                  <shk-badge tone="danger">Sin materias</shk-badge>
                }
              </td>
              <td class="py-2 text-right">
                <shk-button variant="primary" (click)="openStudentDrawer(row.id)">Calificar</shk-button>
              </td>
            </tr>
          } @empty {
            <tr><td colspan="7" class="py-6 text-center text-slate-400">Sin alumnos que coincidan con el filtro.</td></tr>
          }
        </tbody>
      </table>

      <div class="mt-3 space-y-2 sm:hidden">
        @for (row of rosterRows(); track row.id) {
          <div class="flex items-center justify-between gap-3 rounded-xl border border-slate-100 px-3 py-2.5">
            <div class="min-w-0">
              <div class="truncate text-sm font-medium text-slate-800">{{ row.fullName }}</div>
              <div class="text-xs text-slate-400">Matrícula {{ row.enrollmentNumber }} · {{ row.regionName }} · {{ row.planLabel }} {{ row.termLabel }}</div>
              @if (row.hasSubjects) {
                <shk-badge class="mt-1 inline-block" tone="success">{{ row.graded }}/{{ row.total }} calificadas</shk-badge>
              } @else {
                <shk-badge class="mt-1 inline-block" tone="danger">Sin materias</shk-badge>
              }
            </div>
            <shk-button variant="primary" (click)="openStudentDrawer(row.id)">Calificar</shk-button>
          </div>
        } @empty {
          <p class="py-6 text-center text-sm text-slate-400">Sin alumnos que coincidan con el filtro.</p>
        }
      </div>
    </shk-card>

    <!-- Panel lateral: ficha del alumno -->
    @if (drawerOpen()) {
      <div class="fixed inset-0 z-40 bg-slate-900/40" (click)="closeDrawer()"></div>
      <aside class="fixed inset-y-0 right-0 z-50 flex w-full max-w-md flex-col bg-white shadow-xl">
        <div class="flex items-start justify-between gap-3 border-b border-slate-100 px-5 py-4">
          <div>
            @if (selectedStudentInfo(); as info) {
              <div class="text-base font-bold text-[var(--shk-color-primary)]">{{ info.fullName }}</div>
              <div class="mt-0.5 text-xs text-slate-500">Matrícula {{ info.enrollmentNumber }} · {{ info.regionName }} · {{ info.planLabel }} {{ info.termLabel }}</div>
            }
          </div>
          <button type="button" class="h-7 w-7 shrink-0 rounded-full bg-slate-100 text-slate-500 hover:bg-slate-200" (click)="closeDrawer()" aria-label="Cerrar">✕</button>
        </div>

        <div class="flex-1 overflow-y-auto px-5 py-4">
          @if (studentOfferings().length > 0) {
            <div class="space-y-2">
              @for (row of studentOfferings(); track row.offeringId) {
                <div class="flex items-center justify-between gap-3 rounded-xl border border-slate-100 px-3 py-2.5">
                  <div class="min-w-0">
                    <div class="truncate text-sm font-medium text-slate-800">{{ row.subjectName }}</div>
                    <div class="text-xs text-slate-400">{{ row.regionName }} · {{ row.termNumber ?? '—' }}</div>
                    @if (statusFor(studentDraftGrades()[row.offeringId]); as status) {
                      <shk-badge class="mt-1 inline-block" [tone]="status.tone">{{ status.label }}</shk-badge>
                    }
                  </div>
                  <input
                    type="number" min="0" max="10" step="0.1" class="shk-field w-16 shrink-0 text-center"
                    [ngModel]="studentDraftGrades()[row.offeringId]"
                    (ngModelChange)="onStudentGradeChange(row.offeringId, $event)"
                  />
                </div>
              }
            </div>
            <shk-button
              class="mt-4 w-full justify-center"
              variant="primary"
              [loading]="savingStudentBatch()"
              [disabled]="studentDirtyIds().size === 0"
              (click)="saveStudentGrades()"
            >
              Guardar calificaciones de este alumno ({{ studentDirtyIds().size }})
            </shk-button>
          } @else {
            <div class="rounded-lg border border-amber-200 bg-amber-50 px-4 py-3 text-sm leading-relaxed text-amber-700">
              Este alumno no tiene materias asignadas en el periodo activo. Puede ser que aún no haya profesor asignado a sus materias de este cuatrimestre/semestre en su región, o que todavía no se haya corrido la inscripción automática para su cohorte — revisa "Inscribir alumnos en sus materias" o "Promover alumnos" en Académico.
            </div>
          }

          <div class="mt-5 border-t border-dashed border-slate-200 pt-4">
            <div class="text-xs font-bold uppercase tracking-wide text-slate-500">O súbelas desde Excel</div>
            <p class="mt-1 text-xs text-slate-400">Una plantilla con solo las materias de este alumno, para capturar su calificación de un jalón.</p>
            <div class="mt-2 flex flex-wrap gap-2">
              <shk-button variant="secondary" [loading]="downloadingStudentTemplate()" [disabled]="studentOfferings().length === 0" (click)="downloadStudentTemplate()">
                ⬇ Descargar plantilla
              </shk-button>
              <label class="inline-flex cursor-pointer items-center gap-2 rounded-full border border-slate-300 px-4 py-2 text-xs font-semibold text-slate-700 hover:bg-slate-50">
                ↑ Seleccionar archivo
                <input type="file" accept=".xlsx,.csv" class="hidden" (change)="onStudentFileSelected($any($event.target).files)" />
              </label>
              @if (studentImportFileName()) {
                <span class="self-center text-xs text-slate-500">{{ studentImportFileName() }}</span>
                <shk-button variant="primary" [loading]="studentImporting()" (click)="submitStudentImport()">Importar</shk-button>
              }
            </div>
            @if (studentImportResult(); as result) {
              <div class="mt-3 rounded-lg border border-emerald-200 bg-emerald-50 px-3 py-2 text-xs font-semibold text-emerald-700">
                {{ result.importedCells }} calificación(es) importada(s).
              </div>
              @if (result.errors.length > 0) {
                <div class="mt-2 rounded-lg border border-red-200 bg-red-50 px-3 py-2 text-xs text-red-700">
                  @for (err of result.errors; track err.rowNumber + err.subjectName + err.code) {
                    <div>{{ err.subjectName }}: {{ err.message }}</div>
                  }
                </div>
              }
            }
            @if (studentImportError()) {
              <div class="mt-3 rounded-lg border border-red-200 bg-red-50 px-3 py-2 text-xs text-red-700">{{ studentImportError() }}</div>
            }
          </div>
        </div>
      </aside>
    }

    <shk-card class="mt-6">
      <h2 class="font-semibold text-[var(--shk-color-primary)]">Importar calificaciones por Excel</h2>
      <p class="mt-1 text-sm text-slate-500">Una plantilla cubre todas las materias de un cuatrimestre/semestre completo, para un plan y una región.</p>

      <div class="mt-4 grid gap-3 sm:grid-cols-3">
        <label class="flex flex-col gap-1 text-sm">
          <span class="font-medium text-slate-700">Plan</span>
          <select class="shk-field" [ngModel]="importPlan()" (ngModelChange)="importPlan.set($event)">
            <option value="Quarterly">Cuatrimestral</option>
            <option value="Semester">Semestral</option>
          </select>
        </label>
        <label class="flex flex-col gap-1 text-sm">
          <span class="font-medium text-slate-700">{{ importPlan() === 'Semester' ? 'Semestre' : 'Cuatrimestre' }}</span>
          <select class="shk-field" [ngModel]="importTerm()" (ngModelChange)="importTerm.set(+$event)">
            @for (n of [1, 2, 3, 4, 5, 6]; track n) {
              <option [value]="n">{{ n }}</option>
            }
          </select>
        </label>
        <label class="flex flex-col gap-1 text-sm">
          <span class="font-medium text-slate-700">Región</span>
          <select class="shk-field" [ngModel]="importRegionId()" (ngModelChange)="importRegionId.set($event)">
            <option value="" disabled>Selecciona una región</option>
            @for (region of regions(); track region.id) {
              <option [value]="region.id">{{ region.name }}</option>
            }
          </select>
        </label>
      </div>

      <div class="mt-4 flex flex-wrap gap-2">
        <shk-button variant="secondary" [loading]="downloadingTemplate()" [disabled]="!importRegionId()" (click)="downloadTemplate()">
          ⬇ Descargar plantilla
        </shk-button>
        <label class="inline-flex cursor-pointer items-center gap-2 rounded-full border border-slate-300 px-5 py-2.5 text-sm font-semibold text-slate-700 hover:bg-slate-50">
          ↑ Seleccionar archivo
          <input type="file" accept=".xlsx,.csv" class="hidden" (change)="onFileSelected($any($event.target).files)" />
        </label>
        @if (selectedFileName()) {
          <span class="self-center text-sm text-slate-500">{{ selectedFileName() }}</span>
          <shk-button variant="primary" [loading]="importing()" [disabled]="!importRegionId()" (click)="submitImport()">Importar</shk-button>
        }
      </div>

      @if (importResult(); as result) {
        <div class="mt-4 rounded-lg border border-emerald-200 bg-emerald-50 px-4 py-3 text-sm font-semibold text-emerald-700">
          {{ result.importedCells }} de {{ result.totalCells }} calificaciones importadas correctamente ({{ result.totalRows }} fila(s) leídas).
        </div>
        @if (result.errors.length > 0) {
          <div class="mt-3 rounded-lg border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700">
            <p class="font-semibold">{{ result.errors.length }} celda(s) con error — no se importaron, el resto sí:</p>
            <ul class="mt-2 space-y-1">
              @for (err of result.errors; track err.rowNumber + err.subjectName + err.code) {
                <li>Fila {{ err.rowNumber }} — {{ err.subjectName }}: {{ err.message }}</li>
              }
            </ul>
          </div>
        }
      }
      @if (importError()) {
        <div class="mt-4 rounded-lg border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700">{{ importError() }}</div>
      }
    </shk-card>
  `,
  styles: [
    `
    .shk-field {
      border-radius: var(--shk-radius);
      border: 1px solid #cbd5e1;
      padding: 0.5rem 0.75rem;
      font-size: 0.875rem;
      line-height: 1.25rem;
    }
    `,
  ],
})
export class GradesComponent {
  private readonly api = inject(ApiClient);
  private readonly toast = inject(ToastService);
  private readonly route = inject(ActivatedRoute);

  protected readonly selectedOfferingId = signal('');
  private readonly refreshTick = signal(0);
  protected readonly draftGrades = signal<Record<string, number | null>>({});
  protected readonly dirtyIds = signal<Set<string>>(new Set());
  protected readonly savingBatch = signal(false);

  // --- "Calificar por alumno" (2026-10, pedido explícito del cliente; rediseño 2026-10 de tabla +
  // filtros + panel lateral, tras feedback real de que el dropdown no era intuitivo y no explicaba
  // por qué un alumno salía sin materias) ---
  protected readonly studentSearchText = signal('');
  protected readonly rosterRegion = signal('Todas');
  protected readonly rosterPlan = signal('Todos');
  protected readonly rosterTerm = signal('Todos');
  protected readonly selectedStudentId = signal('');
  protected readonly drawerOpen = signal(false);
  protected readonly studentDraftGrades = signal<Record<string, number | null>>({});
  protected readonly studentDirtyIds = signal<Set<string>>(new Set());
  protected readonly savingStudentBatch = signal(false);
  private readonly studentRefreshTick = signal(0);
  private readonly rosterRefreshTick = signal(0);

  // Bulk Assignment por alumno vía Excel (2026-10, pedido explícito del cliente, además del Bulk
  // Assignment por grupo que ya existía).
  protected readonly downloadingStudentTemplate = signal(false);
  protected readonly studentImportFile = signal<File | null>(null);
  protected readonly studentImportFileName = signal<string | null>(null);
  protected readonly studentImporting = signal(false);
  protected readonly studentImportResult = signal<ImportStudentGradesResponseDto | null>(null);
  protected readonly studentImportError = signal<string | null>(null);

  protected readonly importPlan = signal<StudyPlanDto>('Quarterly');
  protected readonly importTerm = signal(1);
  protected readonly importRegionId = signal('');
  protected readonly downloadingTemplate = signal(false);
  protected readonly selectedFile = signal<File | null>(null);
  protected readonly selectedFileName = signal<string | null>(null);
  protected readonly importing = signal(false);
  protected readonly importResult = signal<ImportGradesResponseDto | null>(null);
  protected readonly importError = signal<string | null>(null);

  protected readonly regions = toSignal(
    this.api.get<RegionListItemDto[]>('/catalog/regions').pipe(catchError(() => of<RegionListItemDto[]>([]))),
    { initialValue: [] as RegionListItemDto[] },
  );

  private readonly currentPeriod = toSignal(
    this.api.get<CurrentPeriodResponseDto>('/academic/periods/current').pipe(catchError(() => of<CurrentPeriodResponseDto | null>(null))),
    { initialValue: null as CurrentPeriodResponseDto | null },
  );

  protected readonly offerings = toSignal(
    toObservable(this.currentPeriod).pipe(
      switchMap((period) => {
        if (!period) return of<OfferingListItemDto[]>([]);
        return this.api.get<OfferingListItemDto[]>('/academic/offerings', { periodId: period.id }).pipe(
          catchError(() => of<OfferingListItemDto[]>([])),
        );
      }),
    ),
    { initialValue: [] as OfferingListItemDto[] },
  );

  protected readonly enrollments = toSignal(
    toObservable(this.refreshTick).pipe(
      switchMap(() => {
        const offeringId = this.selectedOfferingId();
        if (!offeringId) return of<EnrollmentRowDto[]>([]);
        return this.api.get<EnrollmentRowDto[]>(`/academic/offerings/${offeringId}/enrollments`).pipe(
          tap((rows) => {
            const drafts: Record<string, number | null> = {};
            for (const row of rows) drafts[row.studentId] = row.grade;
            this.draftGrades.set(drafts);
            this.dirtyIds.set(new Set());
          }),
          catchError(() => of<EnrollmentRowDto[]>([])),
        );
      }),
    ),
    { initialValue: [] as EnrollmentRowDto[] },
  );

  protected readonly allStudents = toSignal(
    this.api
      .get<PagedResultDto<UserListItemDto>>('/users', { role: 'Student', pageSize: 500 })
      .pipe(catchError(() => of<PagedResultDto<UserListItemDto>>({ items: [], page: 1, pageSize: 500, totalItems: 0, totalPages: 0 }))),
    { initialValue: null },
  );

  /** Avance de materias calificadas por alumno en el periodo activo — una sola llamada para toda la
   * tabla (evita N+1). Un alumno que no aparece aquí no tiene ninguna materia asignada todavía. */
  protected readonly gradingRosterMap = toSignal(
    combineLatest([toObservable(this.currentPeriod), toObservable(this.rosterRefreshTick)]).pipe(
      switchMap(([period]) => {
        if (!period) return of<Record<string, { total: number; graded: number }>>({});
        return this.api.get<StudentGradingSummaryRowDto[]>('/academic/grading-roster', { periodId: period.id }).pipe(
          map((rows) => {
            const byStudent: Record<string, { total: number; graded: number }> = {};
            for (const row of rows) byStudent[row.studentId] = { total: row.totalSubjects, graded: row.gradedSubjects };
            return byStudent;
          }),
          catchError(() => of<Record<string, { total: number; graded: number }>>({})),
        );
      }),
    ),
    { initialValue: {} as Record<string, { total: number; graded: number }> },
  );

  protected readonly regionOptions = computed(() => {
    const items = this.allStudents()?.items ?? [];
    const names = new Set<string>();
    for (const student of items) {
      if (student.regionName) names.add(student.regionName);
    }
    return [...names].sort((a, b) => a.localeCompare(b));
  });

  protected readonly rosterRows = computed(() => {
    const items = this.allStudents()?.items ?? [];
    const progress = this.gradingRosterMap();
    const region = this.rosterRegion();
    const plan = this.rosterPlan();
    const term = this.rosterTerm();
    const search = this.studentSearchText().trim().toLowerCase();

    return items
      .filter((student) => {
        if (region !== 'Todas' && student.regionName !== region) return false;
        if (plan !== 'Todos' && student.plan !== plan) return false;
        if (term !== 'Todos' && String(student.currentTerm ?? '') !== term) return false;
        if (search && !student.fullName.toLowerCase().includes(search) && !String(student.enrollmentNumber).includes(search)) return false;
        return true;
      })
      .map((student) => {
        const summary = progress[student.id];
        const total = summary?.total ?? 0;
        const graded = summary?.graded ?? 0;
        return {
          id: student.id,
          fullName: student.fullName,
          enrollmentNumber: student.enrollmentNumber,
          regionName: student.regionName ?? '—',
          planLabel: student.plan ? STUDY_PLAN_LABELS[student.plan] : '—',
          termLabel: student.currentTerm ?? '—',
          total,
          graded,
          hasSubjects: total > 0,
        };
      })
      .sort((a, b) => a.fullName.localeCompare(b.fullName));
  });

  protected readonly selectedStudentInfo = computed(() => {
    const items = this.allStudents()?.items ?? [];
    const student = items.find((s) => s.id === this.selectedStudentId());
    if (!student) return null;
    return {
      fullName: student.fullName,
      enrollmentNumber: student.enrollmentNumber,
      regionName: student.regionName ?? '—',
      planLabel: student.plan ? STUDY_PLAN_LABELS[student.plan] : '—',
      termLabel: student.currentTerm ?? '—',
    };
  });

  protected readonly studentOfferings = toSignal(
    toObservable(this.studentRefreshTick).pipe(
      switchMap(() => {
        const studentId = this.selectedStudentId();
        const periodId = this.currentPeriod()?.id;
        if (!studentId || !periodId) return of<StudentOfferingRowDto[]>([]);
        return this.api.get<StudentOfferingRowDto[]>(`/academic/students/${studentId}/offerings`, { periodId }).pipe(
          tap((rows) => {
            const drafts: Record<string, number | null> = {};
            for (const row of rows) drafts[row.offeringId] = row.grade;
            this.studentDraftGrades.set(drafts);
            this.studentDirtyIds.set(new Set());
          }),
          catchError(() => of<StudentOfferingRowDto[]>([])),
        );
      }),
    ),
    { initialValue: [] as StudentOfferingRowDto[] },
  );

  protected readonly groupAverage = computed(() => {
    const values = Object.values(this.draftGrades()).filter((v): v is number => v !== null && v !== undefined && !Number.isNaN(v));
    if (values.length === 0) return null;
    return (values.reduce((a, b) => a + b, 0) / values.length).toFixed(1);
  });

  protected readonly approvedCount = computed(
    () => Object.values(this.draftGrades()).filter((v) => v !== null && v !== undefined && v >= PASSING_GRADE).length,
  );

  protected readonly pendingCount = computed(() => this.enrollments().length - this.approvedCount());

  constructor() {
    this.route.queryParamMap.subscribe((params) => {
      const offeringId = params.get('offeringId');
      if (offeringId) this.selectOffering(offeringId);
    });
  }

  protected selectOffering(offeringId: string): void {
    this.selectedOfferingId.set(offeringId);
    this.refreshTick.update((n) => n + 1);
  }

  protected onGradeChange(studentId: string, value: number | null): void {
    this.draftGrades.update((drafts) => ({ ...drafts, [studentId]: value }));
    this.dirtyIds.update((ids) => new Set(ids).add(studentId));
  }

  protected statusFor(value: number | null | undefined): { tone: 'success' | 'danger' | 'warning'; label: string } | null {
    if (value === null || value === undefined || Number.isNaN(value)) return { tone: 'warning', label: 'Sin calificar' };
    return value >= PASSING_GRADE ? { tone: 'success', label: 'Aprobada' } : { tone: 'danger', label: 'No aprobada' };
  }

  protected openStudentDrawer(studentId: string): void {
    this.selectedStudentId.set(studentId);
    this.drawerOpen.set(true);
    this.studentImportFile.set(null);
    this.studentImportFileName.set(null);
    this.studentImportResult.set(null);
    this.studentImportError.set(null);
    this.studentRefreshTick.update((n) => n + 1);
  }

  protected closeDrawer(): void {
    this.drawerOpen.set(false);
  }

  protected onStudentGradeChange(offeringId: string, value: number | null): void {
    this.studentDraftGrades.update((drafts) => ({ ...drafts, [offeringId]: value }));
    this.studentDirtyIds.update((ids) => new Set(ids).add(offeringId));
  }

  protected saveStudentGrades(): void {
    const studentId = this.selectedStudentId();
    const periodId = this.currentPeriod()?.id;
    const drafts = this.studentDraftGrades();
    const grades: StudentSubjectGradeEntryDto[] = [...this.studentDirtyIds()]
      .filter((id) => drafts[id] !== null && drafts[id] !== undefined)
      .map((id) => ({ offeringId: id, grade: Math.round(drafts[id] as number) }));

    if (!studentId || !periodId || grades.length === 0) return;

    this.savingStudentBatch.set(true);
    this.api
      .put<RecordStudentGradesBatchResponseDto>(`/academic/offerings/students/${studentId}/grades`, { periodId, grades })
      .pipe(
        tap({
          next: (result) => {
            this.toast.success(`${result.saved} calificación(es) guardada(s).`);
            if (result.errors.length > 0) {
              this.toast.error(`${result.errors.length} no se pudieron guardar.`);
            }
            this.studentDirtyIds.set(new Set());
            this.studentRefreshTick.update((n) => n + 1);
            this.rosterRefreshTick.update((n) => n + 1);
          },
          error: () => this.toast.error('No se pudieron guardar las calificaciones.'),
        }),
        catchError(() => of(null)),
      )
      .subscribe(() => this.savingStudentBatch.set(false));
  }

  protected downloadStudentTemplate(): void {
    const studentId = this.selectedStudentId();
    const periodId = this.currentPeriod()?.id;
    if (!studentId || !periodId) return;

    this.downloadingStudentTemplate.set(true);
    this.api
      .get<GradeImportTemplateResponseDto>(`/academic/students/${studentId}/grades/import/template`, { periodId })
      .pipe(
        map((response) => ({ ok: true as const, response })),
        catchError((error) => of({ ok: false as const, error })),
      )
      .subscribe((result) => {
        this.downloadingStudentTemplate.set(false);
        if (!result.ok) {
          this.toast.error(result.error?.error?.detail ?? 'No se pudo descargar la plantilla.');
          return;
        }
        this.downloadFile(
          result.response.contentBase64,
          result.response.fileName,
          'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
        );
      });
  }

  protected onStudentFileSelected(files: FileList | null): void {
    const file = files?.item(0) ?? null;
    this.studentImportFile.set(file);
    this.studentImportFileName.set(file?.name ?? null);
    this.studentImportResult.set(null);
    this.studentImportError.set(null);
  }

  protected submitStudentImport(): void {
    const file = this.studentImportFile();
    const studentId = this.selectedStudentId();
    const periodId = this.currentPeriod()?.id;
    if (!file || !studentId || !periodId) return;

    this.studentImportError.set(null);
    this.studentImportResult.set(null);
    this.studentImporting.set(true);

    const formData = new FormData();
    formData.append('file', file);
    const params = new URLSearchParams({ periodId });

    this.api
      .postForm<ImportStudentGradesResponseDto>(`/academic/students/${studentId}/grades/import?${params.toString()}`, formData)
      .pipe(
        map((response) => ({ ok: true as const, response })),
        catchError((error) => of({ ok: false as const, error })),
      )
      .subscribe((result) => {
        this.studentImporting.set(false);
        if (!result.ok) {
          this.studentImportError.set(result.error?.error?.detail ?? 'No se pudo importar el archivo.');
          return;
        }
        this.studentImportResult.set(result.response);
        this.studentImportFile.set(null);
        this.studentImportFileName.set(null);
        if (result.response.importedCells > 0) {
          this.toast.success(`${result.response.importedCells} calificación(es) importada(s).`);
          this.studentRefreshTick.update((n) => n + 1);
          this.rosterRefreshTick.update((n) => n + 1);
        }
        if (result.response.errors.length > 0) {
          this.toast.error(`${result.response.errors.length} celda(s) con error — revisa el detalle.`);
        }
      });
  }

  protected saveAll(): void {
    const offeringId = this.selectedOfferingId();
    const drafts = this.draftGrades();
    const grades: GradeEntryDto[] = [...this.dirtyIds()]
      .filter((id) => drafts[id] !== null && drafts[id] !== undefined)
      .map((id) => ({ studentId: id, grade: Math.round(drafts[id] as number) }));

    if (grades.length === 0) return;

    this.savingBatch.set(true);
    this.api.put<RecordGradesBatchResponseDto>(`/academic/offerings/${offeringId}/enrollments/grades`, { grades }).pipe(
      tap({
        next: (result) => {
          this.toast.success(`${result.saved} calificación(es) guardada(s).`);
          if (result.errors.length > 0) {
            this.toast.error(`${result.errors.length} no se pudieron guardar.`);
          }
          this.dirtyIds.set(new Set());
          this.refreshTick.update((n) => n + 1);
        },
        error: () => this.toast.error('No se pudieron guardar los cambios.'),
      }),
      catchError(() => of(null)),
    ).subscribe(() => this.savingBatch.set(false));
  }

  protected downloadTemplate(): void {
    if (!this.importRegionId()) return;

    this.downloadingTemplate.set(true);
    this.api
      .get<GradeImportTemplateResponseDto>('/academic/grades/import/template', {
        periodId: this.currentPeriod()?.id ?? '',
        plan: this.importPlan(),
        termNumber: this.importTerm(),
        regionId: this.importRegionId(),
      })
      .pipe(
        map((response) => ({ ok: true as const, response })),
        catchError((error) => of({ ok: false as const, error })),
      )
      .subscribe((result) => {
        this.downloadingTemplate.set(false);
        if (!result.ok) {
          this.toast.error(result.error?.error?.detail ?? 'No se pudo descargar la plantilla.');
          return;
        }
        this.downloadFile(
          result.response.contentBase64,
          result.response.fileName,
          'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
        );
      });
  }

  protected onFileSelected(files: FileList | null): void {
    const file = files?.item(0) ?? null;
    this.selectedFile.set(file);
    this.selectedFileName.set(file?.name ?? null);
    this.importResult.set(null);
    this.importError.set(null);
  }

  protected submitImport(): void {
    const file = this.selectedFile();
    if (!file || !this.importRegionId()) return;

    this.importError.set(null);
    this.importResult.set(null);
    this.importing.set(true);

    const formData = new FormData();
    formData.append('file', file);

    const params = new URLSearchParams({
      periodId: this.currentPeriod()?.id ?? '',
      plan: this.importPlan(),
      termNumber: String(this.importTerm()),
      regionId: this.importRegionId(),
    });

    this.api
      .postForm<ImportGradesResponseDto>(`/academic/grades/import?${params.toString()}`, formData)
      .pipe(
        map((response) => ({ ok: true as const, response })),
        catchError((error) => of({ ok: false as const, error })),
      )
      .subscribe((result) => {
        this.importing.set(false);
        if (!result.ok) {
          this.importError.set(result.error?.error?.detail ?? 'No se pudo importar el archivo.');
          return;
        }
        this.importResult.set(result.response);
        if (result.response.importedCells > 0) {
          this.toast.success(`${result.response.importedCells} calificación(es) importada(s).`);
          this.refreshTick.update((n) => n + 1);
        }
        if (result.response.errors.length > 0) {
          this.toast.error(`${result.response.errors.length} celda(s) con error — revisa el detalle.`);
        }
      });
  }

  private downloadFile(base64: string, fileName: string, contentType: string): void {
    const bytes = atob(base64);
    const buffer = new Uint8Array(bytes.length);
    for (let i = 0; i < bytes.length; i++) {
      buffer[i] = bytes.charCodeAt(i);
    }
    const blob = new Blob([buffer], { type: contentType });
    const url = URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.download = fileName;
    link.click();
    URL.revokeObjectURL(url);
  }
}
