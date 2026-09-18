import { Component, inject, signal } from '@angular/core';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { of } from 'rxjs';
import { catchError, switchMap, tap } from 'rxjs/operators';
import { ApiClient } from '../../../core/http/api-client';
import { PagedResultDto, StudentPaymentRowDto } from '../../../api/schema';
import { CardComponent } from '../../../shared/ui/card/card.component';
import { ButtonComponent } from '../../../shared/ui/button/button.component';
import { BadgeComponent } from '../../../shared/ui/badge/badge.component';
import { ToastService } from '../../../shared/ui/toast/toast.service';

/**
 * RN-17/RN-18/RN-19: matriz de pagos por mes-cuatrimestre. "Importar pagos" y "Emitir avisos"
 * siguen siendo comandos de backend (batch de importación + DelinquencyPolicy) que esta pantalla
 * solo dispara.
 *
 * Fase 3 del plan de control escolar: mientras no existe pasarela de pagos en línea, cada celda
 * de la matriz es clickeable — marca/revierte un mes como pagado (POST/DELETE /payments/manual).
 * Quien llega a esta pantalla ya pasó el roleGuard de la ruta (Administrator/RegionalCoordinator/
 * RegionalSecretary), así que no se repite el chequeo de rol aquí; el backend además revalida
 * scope de región en el handler (RN-09), nunca confía en lo que llega del cliente.
 */
@Component({
  selector: 'shk-payments',
  imports: [CardComponent, ButtonComponent, BadgeComponent],
  template: `
    <div class="flex items-center justify-between">
      <h1 class="text-2xl font-bold text-slate-900">Pagos</h1>
      <div class="flex gap-2">
        <shk-button variant="secondary" [loading]="issuing()" (click)="issueNotices()">Emitir avisos</shk-button>
      </div>
    </div>

    <p class="mt-2 text-sm text-slate-500">
      Haz clic en una celda para marcar o revertir el pago de ese mes (mientras no haya pagos en línea).
    </p>

    <shk-card class="mt-6 overflow-x-auto">
      <table class="w-full text-left text-sm">
        <thead class="text-slate-500">
          <tr>
            <th class="py-2">Matrícula</th>
            <th class="py-2">Alumno</th>
            <th class="py-2">Región</th>
            @for (month of matrix()?.months ?? []; track month) {
              <th class="py-2">{{ month }}</th>
            }
          </tr>
        </thead>
        <tbody>
          @for (row of matrix()?.items ?? []; track row.studentId) {
            <tr class="border-t border-slate-100">
              <td class="py-2 font-medium">{{ row.enrollmentNumber }}</td>
              <td class="py-2">{{ row.studentName }}</td>
              <td class="py-2">{{ row.regionName }}</td>
              @for (month of matrix()?.months ?? []; track month) {
                <td class="py-2">
                  <button
                    type="button"
                    class="rounded-full transition disabled:cursor-wait disabled:opacity-60"
                    [disabled]="isUpdating(row.studentId, month)"
                    [attr.aria-busy]="isUpdating(row.studentId, month)"
                    [title]="row.paidByMonth[month] ? 'Clic para revertir el pago de ' + month : 'Clic para marcar ' + month + ' como pagado'"
                    (click)="toggle(row, month)"
                  >
                    @if (row.paidByMonth[month]) {
                      <shk-badge tone="success">Pagado</shk-badge>
                    } @else {
                      <shk-badge tone="danger">Pendiente</shk-badge>
                    }
                  </button>
                </td>
              }
            </tr>
          } @empty {
            <tr><td colspan="3" class="py-6 text-center text-slate-400">Sin registros de pago.</td></tr>
          }
        </tbody>
      </table>
    </shk-card>
  `,
})
export class PaymentsComponent {
  private readonly api = inject(ApiClient);
  private readonly toast = inject(ToastService);

  protected readonly issuing = signal(false);
  private readonly refreshTick = signal(0);

  /** "<studentId>|<monthCode>" de la celda en vuelo, para bloquearla mientras responde el backend. */
  private readonly updatingCell = signal<string | null>(null);

  protected readonly matrix = toSignal(
    toObservable(this.refreshTick).pipe(
      switchMap(() =>
        this.api.get<PagedResultDto<StudentPaymentRowDto>>('/payments/matrix').pipe(
          catchError(() => of<PagedResultDto<StudentPaymentRowDto>>({ items: [], page: 1, pageSize: 50, totalItems: 0, totalPages: 0, months: [] })),
        ),
      ),
    ),
    { initialValue: null },
  );

  protected isUpdating(studentId: string, month: string): boolean {
    return this.updatingCell() === `${studentId}|${month}`;
  }

  protected toggle(row: StudentPaymentRowDto, month: string): void {
    const cellKey = `${row.studentId}|${month}`;
    if (this.updatingCell() !== null) return; // evita doble clic mientras hay una celda en vuelo

    const alreadyPaid = row.paidByMonth[month];
    this.updatingCell.set(cellKey);

    const request$ = alreadyPaid
      ? this.api.delete('/payments/manual', { studentId: row.studentId, monthCode: month })
      : this.api.post('/payments/manual', { studentId: row.studentId, monthCode: month });

    request$
      .pipe(
        tap({
          next: () => {
            this.toast.success(alreadyPaid ? `Se revirtió el pago de ${month}.` : `${month} marcado como pagado.`);
            this.refreshTick.update((n) => n + 1);
          },
          error: () => this.toast.error(alreadyPaid ? 'No se pudo revertir el pago.' : 'No se pudo registrar el pago.'),
        }),
        catchError(() => of(null)),
      )
      .subscribe(() => this.updatingCell.set(null));
  }

  protected issueNotices(): void {
    this.issuing.set(true);

    this.api.post('/payments/notices', {}).pipe(
      tap({
        next: () => {
          this.toast.success('Avisos de adeudo emitidos.');
          this.refreshTick.update((n) => n + 1);
        },
        error: () => this.toast.error('No se pudieron emitir los avisos.'),
      }),
      catchError(() => of(null)),
    ).subscribe(() => this.issuing.set(false));
  }
}
