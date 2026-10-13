import { Component, computed, inject, signal } from '@angular/core';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { combineLatest, of } from 'rxjs';
import { catchError, map, switchMap, tap } from 'rxjs/operators';
import { ApiClient } from '../../../core/http/api-client';
import {
  CreateOfferingRequestDto,
  CurrentPeriodResponseDto,
  CurriculumSubjectDto,
  OfferingListItemDto,
  PagedResultDto,
  PromoteStudentsRequestDto,
  PromoteStudentsResponseDto,
  RegionListItemDto,
  StudyPlanDto,
  UserListItemDto,
} from '../../../api/schema';
import { AuthStore } from '../../../core/auth/auth.store';
import { CardComponent } from '../../../shared/ui/card/card.component';
import { BadgeComponent } from '../../../shared/ui/badge/badge.component';
import { ButtonComponent } from '../../../shared/ui/button/button.component';
import { ToastService } from '../../../shared/ui/toast/toast.service';

/**
 * Segundo rediseño de esta pantalla (2026-10: "esto es demasiado confuso para un usuario").
 *
 * El primer rediseño agregó la posibilidad de crear una materia+grupo, pero seguía exigiendo que
 * el usuario entendiera y escogiera "Plan" (Cuatrimestral/Semestral) Y "Cuatrimestre/semestre" por
 * separado, antes de poder siquiera elegir la materia — dos decisiones de más, con vocabulario
 * técnico, para algo que la materia ya sabe de sí misma. Simplificación real, no solo cosmética:
 *
 * - Agregar una materia ahora es UN formulario de 3 campos: Materia, Región, Profesor. El plan y el
 *   cuatrimestre/semestre salen solos de la materia elegida (ya vienen en CurriculumSubjectDto) —
 *   el usuario nunca tiene que saber qué es un "plan de estudios" para usar esta pantalla.
 * - Ya no hay que elegir primero "Cuatrimestral" o "Semestral" para poder ver o agregar algo: la
 *   lista de abajo muestra TODAS las materias asignadas juntas, cada una con su cuatrimestre o
 *   semestre como una etiqueta, y se puede filtrar escribiendo el nombre de la materia si la lista
 *   crece mucho.
 * - El texto ya no explica "pasos" ni usa palabras como "oferta" — dice lo que hace cada botón.
 *
 * Quien da la materia (el profesor) se asigna aquí mismo, al momento de agregarla — por eso esta
 * pantalla sirve tanto para "asignar materias a cuatrimestre/semestre" como para "asignar materias
 * a los profesores": son la misma acción. Reutiliza AssignTeacherCommand / RemoveOfferingCommand
 * (backend, ya existían) — el plan/término se derivan de subject.Plan / subject.TermNumber, el
 * backend no cambió.
 */
@Component({
  selector: 'shk-academic',
  imports: [CardComponent, BadgeComponent, ButtonComponent, RouterLink, FormsModule],
  template: `
    <div class="flex flex-wrap items-center justify-between gap-3">
      <div>
        <h1 class="text-2xl font-bold text-slate-900">Materias</h1>
        <p class="mt-1 text-sm text-slate-500">
          {{ isAdmin()
            ? 'Aquí decides qué materia da cada profesor, y en qué región. Da clic en una materia de la lista para ver y calificar a sus alumnos.'
            : 'Estas son las materias que tienes asignadas. Da clic en una para ver y calificar a tus alumnos.' }}
        </p>
      </div>
      @if (isAdmin()) {
        <shk-button variant="primary" (click)="openCreate()">+ Agregar materia</shk-button>
      }
    </div>

    @if (isAdmin() && createOpen()) {
      <shk-card class="mt-4 border-2 border-[var(--shk-color-primary)]">
        <div class="flex items-center justify-between gap-2">
          <h2 class="font-semibold text-[var(--shk-color-primary)]">Agregar materia</h2>
          <button type="button" class="text-slate-400 hover:text-slate-600" (click)="createOpen.set(false)">✕</button>
        </div>
        <p class="mt-1 text-sm text-slate-500">Elige la materia, en qué región se va a dar, y qué profesor la va a impartir.</p>

        <div class="mt-4 grid gap-3 sm:grid-cols-3">
          <label class="flex flex-col gap-1 text-sm">
            <span class="font-medium text-slate-700">Materia</span>
            <select class="shk-field" [ngModel]="createSubjectId()" (ngModelChange)="createSubjectId.set($event)">
              <option value="" disabled>Selecciona una materia</option>
              <optgroup label="Plan Cuatrimestral (Virtual)">
                @for (subject of quarterlySubjects(); track subject.id) {
                  <option [value]="subject.id">{{ subject.name }} — Cuatrimestre {{ subject.termNumber }}</option>
                }
              </optgroup>
              <optgroup label="Plan Semestral (Presencial)">
                @for (subject of semesterSubjects(); track subject.id) {
                  <option [value]="subject.id">{{ subject.name }} — Semestre {{ subject.termNumber }}</option>
                }
              </optgroup>
            </select>
          </label>
          <label class="flex flex-col gap-1 text-sm">
            <span class="font-medium text-slate-700">Región</span>
            <select class="shk-field" [ngModel]="createRegionId()" (ngModelChange)="createRegionId.set($event)">
              <option value="" disabled>Selecciona una región</option>
              @for (region of regions(); track region.id) {
                <option [value]="region.id">{{ region.name }}</option>
              }
            </select>
          </label>
          <label class="flex flex-col gap-1 text-sm">
            <span class="font-medium text-slate-700">Profesor</span>
            <select class="shk-field" [ngModel]="createTeacherId()" (ngModelChange)="createTeacherId.set($event)">
              <option value="" disabled>Selecciona un profesor</option>
              @for (teacher of teachers(); track teacher.id) {
                <option [value]="teacher.id">{{ teacher.fullName }}</option>
              }
            </select>
            @if (teachers().length === 0) {
              <span class="text-xs text-amber-600">No hay profesores registrados — agrega uno primero en "Docentes".</span>
            }
          </label>
        </div>

        <div class="mt-4 flex justify-end gap-2">
          <shk-button variant="secondary" (click)="createOpen.set(false)">Cancelar</shk-button>
          <shk-button
            variant="primary"
            [loading]="creatingOffering()"
            [disabled]="!createSubjectId() || !createRegionId() || !createTeacherId()"
            (click)="submitCreate()"
          >
            Agregar
          </shk-button>
        </div>
      </shk-card>
    }

    @if (isAdmin() && currentPeriod(); as period) {
      <shk-card class="mt-4">
        <div class="flex flex-wrap items-center justify-between gap-3">
          <div>
            <div class="text-sm font-semibold text-slate-700">Inscribir alumnos en sus materias</div>
            <p class="text-sm text-slate-500">
              Agrega automáticamente a cada alumno activo en las materias que le toquen según su cuatrimestre/semestre. Hazlo cada vez
              que agregues una materia nueva.
            </p>
          </div>
          <shk-button variant="secondary" [loading]="autoEnrolling()" (click)="triggerAutoEnroll(period.id)">
            Inscribir ahora
          </shk-button>
        </div>
      </shk-card>
    }

    @if (isAdmin()) {
      <shk-card class="mt-4">
        <div class="text-sm font-semibold text-slate-700">Promover alumnos</div>
        <p class="mt-1 text-sm text-slate-500">
          Sube de cuatrimestre o semestre a todos los alumnos activos del plan y término que elijas, y los inscribe automáticamente
          en las materias que ya tengan profesor asignado en el periodo activo.
        </p>

        <div class="mt-3 grid gap-3 sm:grid-cols-3">
          <label class="flex flex-col gap-1 text-sm">
            <span class="font-medium text-slate-700">Plan</span>
            <select class="shk-field" [ngModel]="promotePlan()" (ngModelChange)="promotePlan.set($event)">
              <option value="Quarterly">Cuatrimestral</option>
              <option value="Semester">Semestral</option>
            </select>
          </label>
          <label class="flex flex-col gap-1 text-sm">
            <span class="font-medium text-slate-700">{{ promotePlan() === 'Semester' ? 'Semestre actual' : 'Cuatrimestre actual' }}</span>
            <select class="shk-field" [ngModel]="promoteFromTerm()" (ngModelChange)="promoteFromTerm.set(+$event)">
              @for (n of [1, 2, 3, 4, 5, 6]; track n) {
                <option [value]="n">{{ n }}</option>
              }
            </select>
          </label>
          <div class="flex items-end">
            <shk-button variant="secondary" class="w-full justify-center" [loading]="promoting()" (click)="triggerPromote()">
              Promover alumnos
            </shk-button>
          </div>
        </div>

        <p class="mt-2 text-xs text-slate-400">
          Esto promoverá a TODOS los alumnos activos de ese plan y término, en todas las regiones. No se puede deshacer desde aquí.
        </p>

        @if (promoteResult(); as result) {
          <div class="mt-3 rounded-lg border border-emerald-200 bg-emerald-50 px-4 py-3 text-sm text-emerald-700">
            {{ result.studentsPromoted }} alumno(s) promovido(s)
            @if (result.studentsGraduated > 0) { · {{ result.studentsGraduated }} egresado(s) }
            · {{ result.enrollmentsCreated }} inscripción(es) nueva(s) creada(s).
          </div>
        }
      </shk-card>
    }

    <shk-card class="mt-4">
      <div class="flex flex-wrap items-center justify-between gap-3">
        <h2 class="font-semibold text-[var(--shk-color-primary)]">Materias asignadas</h2>
        <input
          type="search"
          placeholder="Buscar materia…"
          class="shk-field w-56"
          [ngModel]="searchText()"
          (ngModelChange)="searchText.set($event)"
        />
      </div>

      <!-- Tabla: desktop/tablet (sm+) -->
      <table class="mt-3 hidden w-full text-left text-sm sm:table">
        <thead class="text-slate-500">
          <tr>
            <th class="py-2">Materia</th><th class="py-2">Región</th>
            <th class="py-2">Profesor</th><th class="py-2">Alumnos</th><th class="py-2"></th>
          </tr>
        </thead>
        <tbody>
          @for (offering of filteredOfferings(); track offering.id) {
            <tr class="border-t border-slate-100">
              <td class="py-2 font-medium">
                {{ offering.subjectName }}
                <shk-badge tone="neutral" class="ml-1">{{ termLabel(offering) }}</shk-badge>
              </td>
              <td class="py-2">{{ offering.regionName }}</td>
              <td class="py-2">{{ offering.teacherName }}</td>
              <td class="py-2">{{ offering.enrollmentCount }}</td>
              <td class="py-2">
                <div class="flex items-center justify-end gap-3">
                  <a class="font-semibold text-[var(--shk-color-accent)] hover:underline" [routerLink]="['/admin/calificaciones']" [queryParams]="{ offeringId: offering.id }">
                    Ver y calificar alumnos →
                  </a>
                  @if (isAdmin()) {
                    <button
                      type="button"
                      class="text-xs font-semibold text-red-500 hover:text-red-700 disabled:opacity-40"
                      [disabled]="removingId() === offering.id"
                      (click)="removeOffering(offering)"
                    >
                      Quitar
                    </button>
                  }
                </div>
              </td>
            </tr>
          } @empty {
            <tr><td colspan="5" class="py-6 text-center text-slate-400">{{ emptyMessage() }}</td></tr>
          }
        </tbody>
      </table>

      <!-- Tarjetas: solo celular -->
      <div class="mt-3 space-y-2 sm:hidden">
        @for (offering of filteredOfferings(); track offering.id) {
          <div class="rounded-xl border border-slate-100 px-4 py-3">
            <a class="block active:bg-slate-50" [routerLink]="['/admin/calificaciones']" [queryParams]="{ offeringId: offering.id }">
              <div class="flex items-start justify-between gap-2">
                <div class="font-semibold text-slate-800">{{ offering.subjectName }}</div>
                <shk-badge tone="neutral">{{ termLabel(offering) }}</shk-badge>
              </div>
              <div class="mt-1 text-sm text-slate-500">{{ offering.regionName }} · {{ offering.teacherName }}</div>
              <div class="mt-2 flex items-center justify-between text-sm">
                <span class="text-slate-400">{{ offering.enrollmentCount }} alumno(s)</span>
                <span class="font-semibold text-[var(--shk-color-accent)]">Ver y calificar →</span>
              </div>
            </a>
            @if (isAdmin()) {
              <button
                type="button"
                class="mt-2 text-xs font-semibold text-red-500 disabled:opacity-40"
                [disabled]="removingId() === offering.id"
                (click)="removeOffering(offering)"
              >
                Quitar materia
              </button>
            }
          </div>
        } @empty {
          <p class="py-6 text-center text-sm text-slate-400">{{ emptyMessage() }}</p>
        }
      </div>
    </shk-card>
  `,
  styles: [
    `
    .shk-field {
      border-radius: 0.5rem;
      border: 1px solid #e2e8f0;
      background: #fff;
      padding: 0.5rem 0.75rem;
      font-size: 0.875rem;
    }
    .shk-field:focus { outline: none; border-color: var(--shk-color-accent); }
    `,
  ],
})
export class AcademicComponent {
  private readonly api = inject(ApiClient);
  private readonly toast = inject(ToastService);
  private readonly auth = inject(AuthStore);

  protected readonly isAdmin = computed(() => this.auth.role() === 'Administrator');

  protected readonly searchText = signal('');
  protected readonly autoEnrolling = signal(false);
  private readonly refreshTick = signal(0);

  protected readonly createOpen = signal(false);
  protected readonly createSubjectId = signal('');
  protected readonly createRegionId = signal('');
  protected readonly createTeacherId = signal('');
  protected readonly creatingOffering = signal(false);
  protected readonly removingId = signal<string | null>(null);

  // --- "Promover alumnos" (2026-10, pedido explícito del cliente) ---
  protected readonly promotePlan = signal<StudyPlanDto>('Quarterly');
  protected readonly promoteFromTerm = signal(1);
  protected readonly promoting = signal(false);
  protected readonly promoteResult = signal<PromoteStudentsResponseDto | null>(null);

  protected readonly currentPeriod = toSignal(
    toObservable(this.refreshTick).pipe(
      switchMap(() =>
        this.api.get<CurrentPeriodResponseDto>('/academic/periods/current').pipe(catchError(() => of<CurrentPeriodResponseDto | null>(null))),
      ),
    ),
    { initialValue: null as CurrentPeriodResponseDto | null },
  );

  protected readonly offerings = toSignal(
    combineLatest([toObservable(this.currentPeriod), toObservable(this.refreshTick)]).pipe(
      switchMap(([period]) => {
        if (!period) return of<OfferingListItemDto[]>([]);
        return this.api.get<OfferingListItemDto[]>('/academic/offerings', { periodId: period.id }).pipe(
          catchError(() => of<OfferingListItemDto[]>([])),
        );
      }),
    ),
    { initialValue: [] as OfferingListItemDto[] },
  );

  protected readonly curriculum = toSignal(
    this.api.get<CurriculumSubjectDto[]>('/catalog/curriculum').pipe(catchError(() => of<CurriculumSubjectDto[]>([]))),
    { initialValue: [] as CurriculumSubjectDto[] },
  );

  protected readonly regions = toSignal(
    this.api.get<RegionListItemDto[]>('/catalog/regions').pipe(catchError(() => of<RegionListItemDto[]>([]))),
    { initialValue: [] as RegionListItemDto[] },
  );

  protected readonly teachers = toSignal(
    this.api.get<PagedResultDto<UserListItemDto>>('/users', { role: 'Teacher', pageSize: 500 }).pipe(
      map((result) => result.items),
      catchError(() => of<UserListItemDto[]>([])),
    ),
    { initialValue: [] as UserListItemDto[] },
  );

  /** Solo materias con cuatrimestre/semestre definido — las de diplomado (TermNumber null) se
   * matriculan por región, no por término (ver AutoEnrollmentPolicy.IsEligible en el backend), y
   * este formulario simplificado no las cubre todavía. */
  protected readonly quarterlySubjects = computed(() =>
    this.curriculum()
      .filter((s) => s.plan === 'Quarterly' && s.termNumber !== null)
      .sort((a, b) => (a.termNumber ?? 0) - (b.termNumber ?? 0) || a.name.localeCompare(b.name)),
  );

  protected readonly semesterSubjects = computed(() =>
    this.curriculum()
      .filter((s) => s.plan === 'Semester' && s.termNumber !== null)
      .sort((a, b) => (a.termNumber ?? 0) - (b.termNumber ?? 0) || a.name.localeCompare(b.name)),
  );

  protected readonly filteredOfferings = computed(() => {
    const search = this.searchText().trim().toLowerCase();
    const all = [...this.offerings()].sort(
      (a, b) => (a.termNumber ?? 0) - (b.termNumber ?? 0) || a.subjectName.localeCompare(b.subjectName),
    );
    return search ? all.filter((o) => o.subjectName.toLowerCase().includes(search)) : all;
  });

  protected readonly emptyMessage = computed(() => {
    if (this.searchText().trim()) return 'Ninguna materia coincide con tu búsqueda.';
    return this.isAdmin() ? 'Todavía no has agregado ninguna materia. Usa "+ Agregar materia" para empezar.' : 'Todavía no tienes materias asignadas.';
  });

  protected termLabel(offering: OfferingListItemDto): string {
    if (offering.termNumber === null) return 'Diplomado';
    return offering.plan === 'Semester' ? `Semestre ${offering.termNumber}` : `Cuatrimestre ${offering.termNumber}`;
  }

  protected openCreate(): void {
    this.createSubjectId.set('');
    this.createRegionId.set('');
    this.createTeacherId.set('');
    this.createOpen.set(true);
  }

  protected submitCreate(): void {
    const period = this.currentPeriod();
    const subjectId = this.createSubjectId();
    const regionId = this.createRegionId();
    const teacherId = this.createTeacherId();
    if (!period || !subjectId || !regionId || !teacherId) return;

    const body: CreateOfferingRequestDto = { periodId: period.id, subjectId, regionId, teacherId };

    this.creatingOffering.set(true);
    this.api.post<string>('/academic/offerings', body).pipe(
      tap({
        next: () => {
          this.toast.success('Materia agregada. Ya aparece en la lista de abajo.');
          this.createOpen.set(false);
          this.refreshTick.update((n) => n + 1);
        },
        error: (error) => this.toast.error(error?.error?.detail ?? 'No se pudo agregar la materia.'),
      }),
      catchError(() => of(null)),
    ).subscribe(() => this.creatingOffering.set(false));
  }

  protected removeOffering(offering: OfferingListItemDto): void {
    if (this.removingId() !== null) return;
    if (!confirm(`¿Quitar "${offering.subjectName}" (${offering.regionName}, ${offering.teacherName})? Esto no se puede deshacer y se perderían las calificaciones ya capturadas ahí.`)) return;

    this.removingId.set(offering.id);
    this.api.delete<void>(`/academic/offerings/${offering.id}`).pipe(
      tap({
        next: () => {
          this.toast.success('Materia quitada.');
          this.refreshTick.update((n) => n + 1);
        },
        error: (error) => this.toast.error(error?.error?.detail ?? 'No se pudo quitar la materia.'),
      }),
      catchError(() => of(null)),
    ).subscribe(() => this.removingId.set(null));
  }

  protected triggerAutoEnroll(periodId: string): void {
    this.autoEnrolling.set(true);
    this.api.post('/academic/offerings/auto-enroll', { periodId }).pipe(
      tap({
        next: () => {
          this.toast.success('Alumnos inscritos en sus materias.');
          this.refreshTick.update((n) => n + 1);
        },
        error: () => this.toast.error('No se pudo completar la inscripción automática.'),
      }),
      catchError(() => of(null)),
    ).subscribe(() => this.autoEnrolling.set(false));
  }

  protected triggerPromote(): void {
    const plan = this.promotePlan();
    const fromTerm = this.promoteFromTerm();
    const planLabel = plan === 'Semester' ? 'semestre' : 'cuatrimestre';
    if (
      !confirm(
        `¿Promover a TODOS los alumnos activos del plan ${plan === 'Semester' ? 'Semestral' : 'Cuatrimestral'} que estén en ${planLabel} ${fromTerm}? Esto los subirá de ${planLabel} y los inscribirá automáticamente en sus nuevas materias. No se puede deshacer desde aquí.`,
      )
    ) {
      return;
    }

    this.promoting.set(true);
    this.promoteResult.set(null);
    const body: PromoteStudentsRequestDto = { plan, fromTerm };
    this.api.post<PromoteStudentsResponseDto>('/academic/students/promote', body).pipe(
      tap({
        next: (result) => {
          this.promoteResult.set(result);
          this.toast.success('Alumnos promovidos correctamente.');
          this.refreshTick.update((n) => n + 1);
        },
        error: (error) => this.toast.error(error?.error?.detail ?? 'No se pudo completar la promoción de alumnos.'),
      }),
      catchError(() => of(null)),
    ).subscribe(() => this.promoting.set(false));
  }
}
