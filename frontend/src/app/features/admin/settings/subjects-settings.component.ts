import { Component, computed, inject, signal } from '@angular/core';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { of } from 'rxjs';
import { catchError, switchMap, tap } from 'rxjs/operators';
import { ApiClient } from '../../../core/http/api-client';
import { CreateSubjectRequestDto, CurriculumSubjectDto, ProgramTypeDto, StudyPlanDto } from '../../../api/schema';
import { CardComponent } from '../../../shared/ui/card/card.component';
import { BadgeComponent } from '../../../shared/ui/badge/badge.component';
import { ButtonComponent } from '../../../shared/ui/button/button.component';
import { ToastService } from '../../../shared/ui/toast/toast.service';
import { PROGRAM_TYPE_LABELS, STUDY_PLAN_LABELS } from '../../../domain/models';

/**
 * "Catálogo de materias" (2026-10, pedido explícito del cliente: "necesito que hagamos un
 * mecanismo... el admin debe también de tener la posibilidad de poder agregar una materia nueva al
 * plan que sea y del número de cuatrimestre o semestre que sea"). Da de alta QUÉ materias existen
 * — a propósito es una pantalla distinta de **Materias** (`academic.component.ts`), que decide
 * quién la da y en qué región (una "oferta"), no qué materias hay. Se repite ese deslinde en el
 * texto de ambas pantallas para no repetir la confusión de nombres que ya se corrigió una vez ahí
 * ("esto es demasiado confuso para un usuario").
 *
 * Corrige también un hueco real del backend: `CreateSubjectCommand` ya existía, pero nunca recibía
 * el plan de estudios — así que SIEMPRE creaba la materia como Cuatrimestral, sin ninguna forma de
 * dar de alta una materia Semestral. Esta pantalla es la primera en mandar ese campo.
 */
@Component({
  selector: 'shk-subjects-settings',
  imports: [CardComponent, BadgeComponent, ButtonComponent, FormsModule],
  template: `
    <div class="flex flex-wrap items-center justify-between gap-3">
      <div>
        <h1 class="text-2xl font-bold text-slate-900">Configuración · Catálogo de materias</h1>
        <p class="mt-1 text-sm text-slate-500">
          Aquí decides qué materias existen, de qué plan (Cuatrimestral o Semestral) y en qué cuatrimestre o semestre.
          Para decidir quién la da y en qué región, ve a <strong>Materias</strong> — esta pantalla solo da de alta la materia en sí.
        </p>
      </div>
      <shk-button variant="primary" (click)="openCreate()">+ Agregar materia al catálogo</shk-button>
    </div>

    @if (createOpen()) {
      <shk-card class="mt-4 border-2 border-[var(--shk-color-primary)]">
        <div class="flex items-center justify-between gap-2">
          <h2 class="font-semibold text-[var(--shk-color-primary)]">Agregar materia al catálogo</h2>
          <button type="button" class="text-slate-400 hover:text-slate-600" (click)="createOpen.set(false)">✕</button>
        </div>

        <div class="mt-4 grid gap-3 sm:grid-cols-2">
          <label class="flex flex-col gap-1 text-sm">
            <span class="font-medium text-slate-700">Nombre de la materia</span>
            <input class="shk-field" [ngModel]="name()" (ngModelChange)="name.set($event)" placeholder="Ej. Teología Sistemática I" />
          </label>
          <label class="flex flex-col gap-1 text-sm">
            <span class="font-medium text-slate-700">Código</span>
            <input class="shk-field" [ngModel]="code()" (ngModelChange)="code.set($event)" placeholder="Ej. TEO-101" />
          </label>
          <label class="flex flex-col gap-1 text-sm">
            <span class="font-medium text-slate-700">Tipo</span>
            <select class="shk-field" [ngModel]="programType()" (ngModelChange)="programType.set($event)">
              <option value="Quarterly">Materia normal</option>
              <option value="Diploma">Diplomado</option>
            </select>
          </label>
          <label class="flex flex-col gap-1 text-sm">
            <span class="font-medium text-slate-700">Plan de estudios</span>
            <select class="shk-field" [ngModel]="plan()" (ngModelChange)="plan.set($event)" [disabled]="programType() === 'Diploma'">
              <option value="Quarterly">Cuatrimestral</option>
              <option value="Semester">Semestral</option>
            </select>
            @if (programType() === 'Diploma') {
              <span class="text-xs text-slate-400">Los diplomados siempre son Cuatrimestral.</span>
            }
          </label>
          <label class="flex flex-col gap-1 text-sm">
            <span class="font-medium text-slate-700">{{ plan() === 'Semester' ? 'Semestre' : 'Cuatrimestre' }}</span>
            <select class="shk-field" [ngModel]="termNumber()" (ngModelChange)="termNumber.set(+$event)" [disabled]="programType() === 'Diploma'">
              @for (n of [1, 2, 3, 4, 5, 6]; track n) {
                <option [value]="n">{{ n }}</option>
              }
            </select>
            @if (programType() === 'Diploma') {
              <span class="text-xs text-slate-400">Un diplomado no tiene cuatrimestre/semestre.</span>
            }
          </label>
          <label class="flex flex-col gap-1 text-sm">
            <span class="font-medium text-slate-700">Orden al mostrarla</span>
            <input type="number" min="1" class="shk-field" [ngModel]="displayOrder()" (ngModelChange)="displayOrder.set(+$event)" />
          </label>
        </div>

        @if (error()) {
          <p class="mt-3 text-sm text-red-600">{{ error() }}</p>
        }

        <div class="mt-4 flex justify-end gap-2">
          <shk-button variant="secondary" (click)="createOpen.set(false)">Cancelar</shk-button>
          <shk-button variant="primary" [loading]="creating()" (click)="submitCreate()">Agregar</shk-button>
        </div>
      </shk-card>
    }

    <shk-card class="mt-4">
      <div class="flex flex-wrap items-center justify-between gap-3">
        <h2 class="font-semibold text-[var(--shk-color-primary)]">Materias del catálogo</h2>
        <input
          type="search"
          placeholder="Buscar materia…"
          class="shk-field w-56"
          [ngModel]="searchText()"
          (ngModelChange)="searchText.set($event)"
        />
      </div>

      <table class="mt-3 hidden w-full text-left text-sm sm:table">
        <thead class="text-slate-500">
          <tr>
            <th class="py-2">Materia</th><th class="py-2">Plan</th><th class="py-2">Tipo</th><th class="py-2"></th>
          </tr>
        </thead>
        <tbody>
          @for (subject of filteredSubjects(); track subject.id) {
            <tr class="border-t border-slate-100">
              <td class="py-2 font-medium">
                {{ subject.name }}
                <span class="ml-1 text-xs text-slate-400">({{ subject.code }})</span>
              </td>
              <td class="py-2">
                <shk-badge tone="neutral">{{ planLabel(subject.plan) }} · {{ termLabel(subject) }}</shk-badge>
              </td>
              <td class="py-2 text-slate-500">{{ programTypeLabel(subject.programType) }}</td>
              <td class="py-2 text-right">
                <button
                  type="button"
                  class="text-xs font-semibold text-red-500 hover:text-red-700 disabled:opacity-40"
                  [disabled]="removingId() === subject.id"
                  (click)="removeSubject(subject)"
                >
                  Quitar
                </button>
              </td>
            </tr>
          } @empty {
            <tr><td colspan="4" class="py-6 text-center text-slate-400">{{ emptyMessage() }}</td></tr>
          }
        </tbody>
      </table>

      <div class="mt-3 space-y-2 sm:hidden">
        @for (subject of filteredSubjects(); track subject.id) {
          <div class="rounded-xl border border-slate-100 px-4 py-3">
            <div class="font-semibold text-slate-800">{{ subject.name }} <span class="text-xs text-slate-400">({{ subject.code }})</span></div>
            <div class="mt-1 flex items-center gap-2 text-sm text-slate-500">
              <shk-badge tone="neutral">{{ planLabel(subject.plan) }} · {{ termLabel(subject) }}</shk-badge>
              <span>{{ programTypeLabel(subject.programType) }}</span>
            </div>
            <button
              type="button"
              class="mt-2 text-xs font-semibold text-red-500 disabled:opacity-40"
              [disabled]="removingId() === subject.id"
              (click)="removeSubject(subject)"
            >
              Quitar materia
            </button>
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
export class SubjectsSettingsComponent {
  private readonly api = inject(ApiClient);
  private readonly toast = inject(ToastService);
  private readonly refreshTick = signal(0);

  protected readonly searchText = signal('');
  protected readonly createOpen = signal(false);
  protected readonly creating = signal(false);
  protected readonly removingId = signal<string | null>(null);
  protected readonly error = signal<string | null>(null);

  protected readonly name = signal('');
  protected readonly code = signal('');
  protected readonly programType = signal<ProgramTypeDto>('Quarterly');
  protected readonly plan = signal<StudyPlanDto>('Quarterly');
  protected readonly termNumber = signal(1);
  protected readonly displayOrder = signal(1);

  protected readonly subjects = toSignal(
    toObservable(this.refreshTick).pipe(
      switchMap(() => this.api.get<CurriculumSubjectDto[]>('/catalog/subjects').pipe(catchError(() => of<CurriculumSubjectDto[]>([])))),
    ),
    { initialValue: [] as CurriculumSubjectDto[] },
  );

  protected readonly filteredSubjects = computed(() => {
    const search = this.searchText().trim().toLowerCase();
    const all = [...this.subjects()].sort(
      (a, b) => a.plan.localeCompare(b.plan) || (a.termNumber ?? 0) - (b.termNumber ?? 0) || a.name.localeCompare(b.name),
    );
    return search ? all.filter((s) => s.name.toLowerCase().includes(search) || s.code.toLowerCase().includes(search)) : all;
  });

  protected readonly emptyMessage = computed(() =>
    this.searchText().trim() ? 'Ninguna materia coincide con tu búsqueda.' : 'Todavía no hay materias en el catálogo.',
  );

  protected planLabel(plan: StudyPlanDto): string {
    return STUDY_PLAN_LABELS[plan];
  }

  protected programTypeLabel(programType: CurriculumSubjectDto['programType']): string {
    return PROGRAM_TYPE_LABELS[programType as 'Quarterly' | 'Diploma'];
  }

  protected termLabel(subject: CurriculumSubjectDto): string {
    if (subject.termNumber === null) return 'Diplomado';
    return subject.plan === 'Semester' ? `Semestre ${subject.termNumber}` : `Cuatrimestre ${subject.termNumber}`;
  }

  protected openCreate(): void {
    this.name.set('');
    this.code.set('');
    this.programType.set('Quarterly');
    this.plan.set('Quarterly');
    this.termNumber.set(1);
    this.displayOrder.set(1);
    this.error.set(null);
    this.createOpen.set(true);
  }

  protected submitCreate(): void {
    this.error.set(null);

    if (!this.name().trim() || !this.code().trim()) {
      this.error.set('Completa el nombre y el código de la materia.');
      return;
    }

    const body: CreateSubjectRequestDto = {
      code: this.code().trim(),
      name: this.name().trim(),
      programType: this.programType(),
      termNumber: this.programType() === 'Diploma' ? null : this.termNumber(),
      displayOrder: this.displayOrder(),
      plan: this.programType() === 'Diploma' ? 'Quarterly' : this.plan(),
    };

    this.creating.set(true);
    this.api
      .post<string>('/catalog/subjects', body)
      .pipe(
        tap({
          next: () => {
            this.toast.success('Materia agregada al catálogo.');
            this.createOpen.set(false);
            this.refreshTick.update((n) => n + 1);
          },
          error: (error) => this.error.set(error?.error?.detail ?? 'No se pudo agregar la materia.'),
        }),
        catchError(() => of(null)),
      )
      .subscribe(() => this.creating.set(false));
  }

  protected removeSubject(subject: CurriculumSubjectDto): void {
    if (this.removingId() !== null) return;
    if (!confirm(`¿Quitar "${subject.name}" del catálogo? Esto no afecta a las materias ya asignadas a profesores, pero no podrás volver a asignarla.`)) return;

    this.removingId.set(subject.id);
    this.api
      .delete<void>(`/catalog/subjects/${subject.id}`)
      .pipe(
        tap({
          next: () => {
            this.toast.success('Materia quitada del catálogo.');
            this.refreshTick.update((n) => n + 1);
          },
          error: (error) => this.toast.error(error?.error?.detail ?? 'No se pudo quitar la materia.'),
        }),
        catchError(() => of(null)),
      )
      .subscribe(() => this.removingId.set(null));
  }
}
