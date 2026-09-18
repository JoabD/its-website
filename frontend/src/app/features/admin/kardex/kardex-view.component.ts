import { Component, inject, input, signal } from '@angular/core';
import { DecimalPipe } from '@angular/common';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { of } from 'rxjs';
import { catchError, switchMap, tap } from 'rxjs/operators';
import { ApiClient } from '../../../core/http/api-client';
import { KardexResponseDto } from '../../../api/schema';
import { AuthStore } from '../../../core/auth/auth.store';
import { CardComponent } from '../../../shared/ui/card/card.component';
import { ButtonComponent } from '../../../shared/ui/button/button.component';
import { BadgeComponent } from '../../../shared/ui/badge/badge.component';
import { ToastService } from '../../../shared/ui/toast/toast.service';

/**
 * Plan de control escolar, fase 8: vista del Kardex, reutilizada tanto por "Mi Kardex" (alumno,
 * studentId="me") como por el Kardex admin (Administrator/RegionalCoordinator/RegionalSecretary,
 * studentId=id real de un alumno de su alcance — el backend revalida el alcance en el handler,
 * RN-09). Descarga en PDF y envío por correo desde el día uno, confirmado por el usuario.
 */
@Component({
  selector: 'shk-kardex-view',
  imports: [CardComponent, ButtonComponent, BadgeComponent, DecimalPipe],
  template: `
    @if (kardex(); as data) {
      <shk-card>
        <div class="flex items-start justify-between gap-4">
          <div>
            <h2 class="text-lg font-semibold text-slate-900">{{ data.fullName }}</h2>
            <p class="text-sm text-slate-500">{{ data.email }} · Matrícula {{ data.enrollmentNumber }}</p>
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
            <div class="text-slate-400">Cuatrimestre actual</div>
            <div class="font-medium text-slate-700">{{ data.currentTerm ?? 'N/D' }}</div>
          </div>
          <div>
            <div class="text-slate-400">Promedio general</div>
            <div class="font-medium text-slate-700">{{ data.averageGrade ? (data.averageGrade | number: '1.1-1') : 'N/D' }}</div>
          </div>
        </div>

        <div class="mt-6 flex flex-wrap items-center gap-2">
          <shk-button variant="secondary" [loading]="downloading()" (click)="downloadPdf()">
            <i class="bi bi-file-earmark-pdf"></i> Descargar PDF
          </shk-button>

          @if (isStaff()) {
            <input
              type="email"
              class="shk-field w-64"
              placeholder="Correo destino (opcional, por defecto el del alumno)"
              [value]="overrideEmail()"
              (input)="overrideEmail.set($any($event.target).value)"
            />
          }

          <shk-button variant="secondary" [loading]="sending()" (click)="sendEmail()">
            <i class="bi bi-envelope"></i> Enviar por correo
          </shk-button>
        </div>
      </shk-card>

      <shk-card class="mt-4 overflow-x-auto">
        <table class="w-full text-left text-sm">
          <thead class="text-slate-500">
            <tr>
              <th class="py-2">Materia</th>
              <th class="py-2">Cuatrimestre</th>
              <th class="py-2">Calificación</th>
              <th class="py-2">Estatus</th>
              <th class="py-2">Periodo</th>
            </tr>
          </thead>
          <tbody>
            @for (row of data.subjects; track row.subjectName + row.periodCode) {
              <tr class="border-t border-slate-100">
                <td class="py-2 font-medium">{{ row.subjectName }}</td>
                <td class="py-2">{{ row.termNumber ?? '—' }}</td>
                <td class="py-2">{{ row.grade ?? '—' }}</td>
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
            } @empty {
              <tr><td colspan="5" class="py-6 text-center text-slate-400">Sin materias registradas todavía.</td></tr>
            }
          </tbody>
        </table>
      </shk-card>
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

  private readonly api = inject(ApiClient);
  private readonly toast = inject(ToastService);
  private readonly auth = inject(AuthStore);

  protected readonly downloading = signal(false);
  protected readonly sending = signal(false);
  protected readonly overrideEmail = signal('');

  protected readonly isStaff = () => this.auth.role() !== 'Student';

  protected readonly kardex = toSignal(
    toObservable(this.studentId).pipe(
      switchMap((id) =>
        this.api.get<KardexResponseDto>(`/kardex/${id}`).pipe(catchError(() => of<KardexResponseDto | null>(null))),
      ),
    ),
    { initialValue: null as KardexResponseDto | null },
  );

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
        error: () => this.toast.error('No se pudo enviar el Kardex por correo.'),
      }),
      catchError(() => of(null)),
    ).subscribe(() => this.sending.set(false));
  }
}
