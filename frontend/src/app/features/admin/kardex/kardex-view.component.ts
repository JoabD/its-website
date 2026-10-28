import { Component, computed, inject, input, signal } from '@angular/core';
import { DecimalPipe } from '@angular/common';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { combineLatest, of } from 'rxjs';
import { catchError, switchMap, tap } from 'rxjs/operators';
import { ApiClient } from '../../../core/http/api-client';
import { KardexResponseDto, KardexSubjectRowDto } from '../../../api/schema';
import { AuthStore } from '../../../core/auth/auth.store';
import { CardComponent } from '../../../shared/ui/card/card.component';
import { ButtonComponent } from '../../../shared/ui/button/button.component';
import { BadgeComponent } from '../../../shared/ui/badge/badge.component';
import { ToastService } from '../../../shared/ui/toast/toast.service';

const PASSING_GRADE = 7;

/**
 * Plan de control escolar, fase 8: vista del Kardex, reutilizada tanto por "Mi Kardex" (alumno,
 * studentId="me") como por el Kardex admin (Administrator/RegionalCoordinator/RegionalSecretary,
 * studentId=id real de un alumno de su alcance — el backend revalida el alcance en el handler,
 * RN-09). Descarga en PDF y envío por correo desde el día uno, confirmado por el usuario.
 *
 * Rediseño del flujo de asignación (2026-10): "en el detalle de cada alumno debería poderse
 * asignar sus calificaciones por semestre o cuatrimestre" — antes, corregir la calificación de una
 * materia de un alumno concreto obligaba a ir a Calificaciones y buscar el grupo correspondiente.
 * El input `editable` (prendido solo por KardexAdminComponent, nunca por "Mi Kardex" del alumno)
 * agrega captura en línea por materia, directamente aquí en el detalle del alumno — reutiliza
 * RecordGradeCommand (mismo endpoint que ya usa Calificaciones), así que las reglas de negocio
 * (mínimo 7, solo Teacher dueño o Administrator) se validan igual en el backend. Solo se habilita
 * de verdad para Administrator (RecordGradeCommand no acepta Coordinador/Secretario regional).
 */
@Component({
  selector: 'shk-kardex-view',
  imports: [CardComponent, ButtonComponent, BadgeComponent, DecimalPipe, FormsModule],
  template: `
    @if (kardex(); as data) {
      <shk-card>
        <div class="flex items-start justify-between gap-4">
          <div>
            <h2 class="text-lg font-semibold text-slate-900">{{ data.fullName }}</h2>
            <p class="text-sm text-slate-500">{{ data.email ?? 'Sin correo registrado' }} · Matrícula {{ data.enrollmentNumber }}</p>
          </div>
          <shk-badge [tone]="data.isGraduated ? 'success' : 'warning'">{{ data.isGraduated ? 'Egresado' : 'Activo' }}</shk-badge>
        </div>

        <div class="mt-4 grid grid-cols-2 gap-3 text-sm sm:grid-cols-4">
          <div>
            <div class="text-slate-400">Región</div>
            <div class="font-medium text-slate-700">{{ data.regionName ?? 'N/D' }}</div>
          </div>
          <div>
            <div class="text-slate-400">Modalidad</div>
            <div class="font-medium text-slate-700">{{ data.modality ?? 'N/D' }}</div>
          </div>
          <div>
            <div class="text-slate-400">Cuatrimestre/semestre actual</div>
            <div class="font-medium text-slate-700">{{ data.currentTerm ?? 'N/D' }}</div>
          </div>
          <div>
            <div class="text-slate-400">Promedio general</div>
            <div class="font-medium text-slate-700">{{ data.averageGrade ? (data.averageGrade | number: '1.1-1') : 'N/D' }}</div>
          </div>
        </div>

        @if (data.hasPendingSubjects) {
          <div class="mt-4 rounded-lg border border-amber-200 bg-amber-50 px-4 py-3 text-sm text-amber-800">
            ⚠ Hay materias pendientes de aprobar (calificación menor a 7 o sin calificar) en uno o más periodos. Esto no bloquea su avance, pero conviene darle seguimiento.
          </div>
        }

        @if (canEdit()) {
          <div class="mt-4 rounded-lg border border-[var(--shk-color-accent)]/30 bg-[var(--shk-color-accent)]/5 px-4 py-3 text-sm text-slate-600">
            Puedes capturar o corregir la calificación de cada materia directamente abajo, por cuatrimestre/semestre — el cambio se guarda
            al dar clic en "Guardar" junto al campo.
          </div>
        }

        <div class="mt-6 flex flex-wrap items-center gap-2">
          <shk-button variant="secondary" [loading]="downloading()" (click)="downloadPdf()">
            <i class="bi bi-file-earmark-pdf"></i> Descargar PDF
          </shk-button>

          @if (isStaff()) {
            <input
              type="email"
              class="shk-field w-64"
              [placeholder]="data.email ? 'Correo destino (opcional, por defecto el del alumno)' : 'El alumno no tiene correo — captura uno para enviarlo'"
              [value]="overrideEmail()"
              (input)="overrideEmail.set($any($event.target).value)"
            />
          }

          <shk-button variant="secondary" [loading]="sending()" (click)="sendEmail()">
            <i class="bi bi-envelope"></i> Enviar por correo
          </shk-button>
        </div>
      </shk-card>

      @for (term of data.terms; track term.termNumber) {
        <shk-card class="mt-4 overflow-x-auto">
          <div class="flex items-center justify-between gap-4">
            <h3 class="font-semibold text-slate-800">Cuatrimestre/semestre {{ term.termNumber }}</h3>
            <div class="flex items-center gap-3 text-sm">
              <span class="text-slate-500">Promedio: <b class="text-slate-800">{{ term.averageGrade ? (term.averageGrade | number: '1.1-1') : '—' }}</b></span>
              @if (term.isApproved) {
                <shk-badge tone="success">✓ Aprobado</shk-badge>
              } @else if (term.hasPendingSubject) {
                <shk-badge tone="warning">⚠ Pendiente</shk-badge>
              }
            </div>
          </div>

          <!-- Tabla: desktop/tablet (sm+). En celular una tabla de 4 columnas obliga a hacer scroll
               horizontal para leer la calificación — casi imposible de usar con el pulgar. -->
          <table class="mt-3 hidden w-full text-left text-sm sm:table">
            <thead class="text-slate-500">
              <tr><th class="py-2">Materia</th><th class="py-2">Calificación</th><th class="py-2">Estatus</th><th class="py-2">Periodo</th></tr>
            </thead>
            <tbody>
              @for (row of term.subjects; track row.subjectName + row.periodCode) {
                <tr class="border-t border-slate-100">
                  <td class="py-2 font-medium">{{ row.subjectName }}</td>
                  <td class="py-2">
                    @if (canEdit() && row.offeringId && row.status !== 'Baja') {
                      <div class="flex items-center gap-2">
                        <input
                          type="number" min="0" max="10" step="1" class="shk-field w-20"
                          [ngModel]="draftGrades()[row.offeringId]"
                          (ngModelChange)="onDraftChange(row.offeringId!, $event)"
                        />
                        @if (isDirty(row)) {
                          <button
                            type="button"
                            class="rounded-lg bg-[var(--shk-color-primary)] px-2.5 py-1 text-xs font-semibold text-white disabled:opacity-50"
                            [disabled]="savingOfferingId() === row.offeringId"
                            (click)="saveGrade(row)"
                          >
                            {{ savingOfferingId() === row.offeringId ? '…' : 'Guardar' }}
                          </button>
                        }
                      </div>
                    } @else {
                      {{ row.grade ?? '—' }}
                    }
                  </td>
                  <td class="py-2">
                    @switch (row.status) {
                      @case ('Aprobada') { <shk-badge tone="success">Aprobada</shk-badge> }
                      @case ('No aprobada') { <shk-badge tone="danger">No aprobada</shk-badge> }
                      @case ('Baja') { <shk-badge tone="neutral">Baja</shk-badge> }
                      @default { <shk-badge tone="warning">En curso</shk-badge> }
                    }
                  </td>
                  <td class="py-2">{{ row.periodCode }}</td>
                </tr>
              }
            </tbody>
          </table>

          <!-- Tarjetas: solo celular (sm:hidden) — una materia por renglón, calificación y estatus
               visibles sin desplazar nada de lado a lado. -->
          <div class="mt-3 space-y-2 sm:hidden">
            @for (row of term.subjects; track row.subjectName + row.periodCode) {
              <div class="rounded-lg border border-slate-100 px-3 py-2.5">
                <div class="flex items-center justify-between gap-3">
                  <div class="min-w-0">
                    <div class="truncate text-sm font-medium text-slate-800">{{ row.subjectName }}</div>
                    <div class="text-xs text-slate-400">{{ row.periodCode }}</div>
                  </div>
                  <div class="flex shrink-0 items-center gap-2">
                    @if (!(canEdit() && row.offeringId && row.status !== 'Baja')) {
                      <span class="text-sm font-semibold text-slate-700">{{ row.grade ?? '—' }}</span>
                    }
                    @switch (row.status) {
                      @case ('Aprobada') { <shk-badge tone="success">Aprobada</shk-badge> }
                      @case ('No aprobada') { <shk-badge tone="danger">No aprobada</shk-badge> }
                      @case ('Baja') { <shk-badge tone="neutral">Baja</shk-badge> }
                      @default { <shk-badge tone="warning">En curso</shk-badge> }
                    }
                  </div>
                </div>
                @if (canEdit() && row.offeringId && row.status !== 'Baja') {
                  <div class="mt-2 flex items-center gap-2">
                    <input
                      type="number" min="0" max="10" step="1" class="shk-field w-20"
                      [ngModel]="draftGrades()[row.offeringId]"
                      (ngModelChange)="onDraftChange(row.offeringId!, $event)"
                    />
                    @if (isDirty(row)) {
                      <button
                        type="button"
                        class="rounded-lg bg-[var(--shk-color-primary)] px-2.5 py-1 text-xs font-semibold text-white disabled:opacity-50"
                        [disabled]="savingOfferingId() === row.offeringId"
                        (click)="saveGrade(row)"
                      >
                        {{ savingOfferingId() === row.offeringId ? '…' : 'Guardar' }}
                      </button>
                    }
                  </div>
                }
              </div>
            }
          </div>
        </shk-card>
      } @empty {
        <shk-card class="mt-4">
          <p class="py-6 text-center text-slate-400">Sin materias registradas todavía.</p>
        </shk-card>
      }
    } @else {
      <p class="py-6 text-center text-slate-400">No se pudo cargar el Kardex.</p>
    }
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
export class KardexViewComponent {
  readonly studentId = input.required<string>();

  /** Solo KardexAdminComponent lo enciende (nunca "Mi Kardex" del alumno). Aun encendido, la
   * captura real solo se habilita si el rol es Administrator (ver canEdit) — RecordGradeCommand
   * no acepta RegionalCoordinator/RegionalSecretary. */
  readonly editable = input(false);

  private readonly api = inject(ApiClient);
  private readonly toast = inject(ToastService);
  private readonly auth = inject(AuthStore);

  protected readonly downloading = signal(false);
  protected readonly sending = signal(false);
  protected readonly overrideEmail = signal('');
  protected readonly savingOfferingId = signal<string | null>(null);
  protected readonly draftGrades = signal<Record<string, number | null>>({});
  private readonly refreshTick = signal(0);

  protected readonly isStaff = () => this.auth.role() !== 'Student';
  protected readonly canEdit = computed(() => this.editable() && this.auth.role() === 'Administrator');

  protected readonly kardex = toSignal(
    combineLatest([toObservable(this.studentId), toObservable(this.refreshTick)]).pipe(
      switchMap(([id]) =>
        this.api.get<KardexResponseDto>(`/kardex/${id}`).pipe(
          tap((data) => {
            const drafts: Record<string, number | null> = {};
            for (const row of data.subjects) {
              if (row.offeringId) drafts[row.offeringId] = row.grade;
            }
            this.draftGrades.set(drafts);
          }),
          catchError(() => of<KardexResponseDto | null>(null)),
        ),
      ),
    ),
    { initialValue: null as KardexResponseDto | null },
  );

  protected onDraftChange(offeringId: string, value: number | null): void {
    this.draftGrades.update((drafts) => ({ ...drafts, [offeringId]: value }));
  }

  protected isDirty(row: KardexSubjectRowDto): boolean {
    if (!row.offeringId) return false;
    const draft = this.draftGrades()[row.offeringId];
    return (draft ?? null) !== (row.grade ?? null);
  }

  protected saveGrade(row: KardexSubjectRowDto): void {
    const offeringId = row.offeringId;
    const draft = offeringId ? this.draftGrades()[offeringId] : null;
    if (!offeringId || draft === null || draft === undefined || Number.isNaN(draft)) return;

    this.savingOfferingId.set(offeringId);
    this.api.put<void>(`/academic/offerings/${offeringId}/enrollments/${this.studentId()}/grade`, { grade: Math.round(draft) }).pipe(
      tap({
        next: () => {
          this.toast.success(`Calificación de "${row.subjectName}" guardada.`);
          this.refreshTick.update((n) => n + 1);
        },
        error: (error) => this.toast.error(error?.error?.detail ?? `No se pudo guardar la calificación de "${row.subjectName}".`),
      }),
      catchError(() => of(null)),
    ).subscribe(() => this.savingOfferingId.set(null));
  }

  protected downloadPdf(): void {
    this.downloading.set(true);
    this.api.getBlob(`/kardex/${this.studentId()}/pdf`).pipe(
      tap({
        next: (blob) => {
          const url = URL.createObjectURL(blob);
          const link = document.createElement('a');
          link.href = url;
          link.download = `Kardex-${this.kardex()?.enrollmentNumber ?? this.studentId()}.pdf`;
          link.click();
          URL.revokeObjectURL(url);
        },
        error: () => this.toast.error('No se pudo generar el PDF del Kardex.'),
      }),
      catchError(() => of(null)),
    ).subscribe(() => this.downloading.set(false));
  }

  protected sendEmail(): void {
    this.sending.set(true);
    const email = this.overrideEmail().trim();

    this.api.post(`/kardex/${this.studentId()}/email`, { email: email || null }).pipe(
      tap({
        next: () => this.toast.success('Kardex enviado por correo.'),
        error: (error) => this.toast.error(error?.error?.detail ?? 'No se pudo enviar el Kardex por correo.'),
      }),
      catchError(() => of(null)),
    ).subscribe(() => this.sending.set(false));
  }
}
