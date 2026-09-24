import { DecimalPipe } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { combineLatest, of } from 'rxjs';
import { catchError, switchMap, tap } from 'rxjs/operators';
import { ApiClient } from '../../../core/http/api-client';
import {
  BillingSummaryResponseDto,
  CurrentPeriodResponseDto,
  PagedResultDto,
  RegionListItemDto,
  StudentPaymentRowDto,
} from '../../../api/schema';
import { CardComponent } from '../../../shared/ui/card/card.component';
import { ButtonComponent } from '../../../shared/ui/button/button.component';
import { BadgeComponent } from '../../../shared/ui/badge/badge.component';
import { ToastService } from '../../../shared/ui/toast/toast.service';
import { PaymentDetailDrawerComponent } from './payment-detail-drawer.component';

const MONTH_NAMES = ['ene', 'feb', 'mar', 'abr', 'may', 'jun', 'jul', 'ago', 'sep', 'oct', 'nov', 'dic'];

function formatMonthCode(monthCode: string): string {
  if (monthCode.length !== 6) return monthCode;
  const year = monthCode.slice(0, 4);
  const month = Number(monthCode.slice(4, 6));
  return `${MONTH_NAMES[month - 1] ?? monthCode}. ${year}`;
}

/**
 * Panel de verificación de pagos (docs/Plan-Panel-Pagos.md), fase 1: además de la matriz por
 * mes-cuatrimestre que ya existía (marcar/revertir pagos, emitir avisos), se agregan:
 *  - Filtros por región y por mes específico (antes la matriz nunca mandaba `periodId`, así que la
 *    tabla siempre quedaba vacía — se corrige aquí cargando primero el periodo activo).
 *  - Búsqueda por nombre/matrícula sobre la matriz ya cargada (mismo patrón que StudentsComponent).
 *  - Recibo: dos botones explícitos en el drawer — "Enviar por correo y descargar" (correo automático
 *    + descarga del PDF en el mismo clic) y "Enviar por WhatsApp" (abre wa.me con el mensaje armado,
 *    100% en el cliente) — Opción C del plan (un clic humano en WhatsApp, cero en email).
 *  - Resumen financiero del mes seleccionado (cobrado vs. esperado, por región).
 *
 * RN-09: todos los filtros de región son de conveniencia en el cliente — el backend siempre acota
 * de verdad a la región del RegionalCoordinator/Secretary en el handler, nunca confía en esto.
 *
 * Rediseño de interacción (feedback real): antes cada celda de la matriz era clickeable para
 * marcar/revertir el pago — funcional, pero nada en el badge "Pendiente" sugería que se podía
 * hacer clic ahí. Ahora la matriz es de solo lectura (un vistazo rápido del cuatrimestre completo)
 * y clic en la FILA de un alumno abre <shk-payment-detail-drawer> con una cuadrícula mes por mes
 * donde cada acción (marcar, revertir, enviar recibo) es un botón explícito con texto.
 */
@Component({
  selector: 'shk-payments',
  imports: [CardComponent, ButtonComponent, BadgeComponent, DecimalPipe, PaymentDetailDrawerComponent],
  template: `
    <div class="flex items-center justify-between">
      <h1 class="text-2xl font-bold text-slate-900">Pagos</h1>
      <div class="flex gap-2">
        <shk-button variant="secondary" [loading]="issuing()" (click)="issueNotices()">Emitir avisos</shk-button>
      </div>
    </div>

    <p class="mt-2 text-sm text-slate-500">
      Haz clic en el nombre de un alumno para abrir su detalle y verificar sus pagos mes por mes.
    </p>

    <!-- Resumen financiero del mes seleccionado -->
    @if (summary(); as s) {
      <div class="mt-6 grid grid-cols-2 gap-3 sm:grid-cols-4">
        <shk-card>
          <p class="text-xs text-slate-500">Cobrado — {{ formatMonth(selectedMonth() ?? '') }}</p>
          <p class="mt-1 text-xl font-bold text-slate-900">\${{ s.collectedTotal | number: '1.0-0' }}</p>
        </shk-card>
        <shk-card>
          <p class="text-xs text-slate-500">Esperado (cuota fija $500)</p>
          <p class="mt-1 text-xl font-bold text-slate-900">\${{ s.expectedTotal | number: '1.0-0' }}</p>
        </shk-card>
        <shk-card>
          <p class="text-xs text-slate-500">% de cobranza</p>
          <p class="mt-1 text-xl font-bold" [class.text-emerald-600]="s.collectionRatePercent >= 80" [class.text-amber-600]="s.collectionRatePercent < 80">
            {{ s.collectionRatePercent }}%
          </p>
        </shk-card>
        <shk-card>
          <p class="text-xs text-slate-500">Alumnos al corriente / total</p>
          <p class="mt-1 text-xl font-bold text-slate-900">{{ s.paidCount }} / {{ s.activeStudents }}</p>
        </shk-card>
      </div>

      @if (s.byRegion.length > 1) {
        <shk-card class="mt-3 overflow-x-auto">
          <table class="w-full text-left text-sm">
            <thead class="text-slate-500">
              <tr><th class="py-1">Región</th><th class="py-1">Alumnos</th><th class="py-1">Pagados</th><th class="py-1">Esperado</th><th class="py-1">Cobrado</th></tr>
            </thead>
            <tbody>
              @for (row of s.byRegion; track row.regionId) {
                <tr class="border-t border-slate-100">
                  <td class="py-1">{{ row.regionName }}</td>
                  <td class="py-1">{{ row.activeStudents }}</td>
                  <td class="py-1">{{ row.paidCount }}</td>
                  <td class="py-1">\${{ row.expectedTotal | number: '1.0-0' }}</td>
                  <td class="py-1">\${{ row.collectedTotal | number: '1.0-0' }}</td>
                </tr>
              }
            </tbody>
          </table>
        </shk-card>
      }
    }

    <!-- Filtros -->
    <div class="mt-6 flex flex-wrap gap-3">
      <input
        type="search"
        placeholder="Buscar por nombre o matrícula…"
        class="shk-field w-64"
        (input)="searchText.set($any($event.target).value)"
      />
      <select class="shk-field" (change)="regionFilter.set($any($event.target).value || null)">
        <option value="">Todas las regiones</option>
        @for (region of regions(); track region.id) {
          <option [value]="region.id">{{ region.name }}</option>
        }
      </select>
      <select class="shk-field" [value]="selectedMonth() ?? ''" (change)="selectedMonth.set($any($event.target).value || null)">
        @for (month of period()?.allMonthCodes ?? []; track month) {
          <option [value]="month">{{ formatMonth(month) }}</option>
        }
      </select>
    </div>

    <shk-card class="mt-4 overflow-x-auto">
      <table class="w-full text-left text-sm">
        <thead class="text-slate-500">
          <tr>
            <th class="py-2">Matrícula</th>
            <th class="py-2">Alumno</th>
            <th class="py-2">Región</th>
            <th class="py-2">Meses pagados</th>
            <th class="py-2"></th>
          </tr>
        </thead>
        <tbody>
          @for (row of filteredRows(); track row.studentId) {
            <tr
              class="cursor-pointer border-t border-slate-100 transition hover:bg-slate-50"
              tabindex="0"
              [attr.aria-label]="'Ver detalle de pagos de ' + row.studentName"
              (click)="selectedStudentId.set(row.studentId)"
              (keydown.enter)="selectedStudentId.set(row.studentId)"
            >
              <td class="py-2 font-medium">{{ row.enrollmentNumber }}</td>
              <td class="py-2 font-medium text-[var(--shk-color-primary)] underline decoration-dotted underline-offset-2">{{ row.studentName }}</td>
              <td class="py-2">{{ row.regionName }}</td>
              <td class="py-2">
                @if (monthsDueCount(row) === 0) {
                  <shk-badge tone="success">Al corriente</shk-badge>
                } @else {
                  <shk-badge tone="danger">{{ monthsDueCount(row) }} pendiente(s)</shk-badge>
                }
              </td>
              <td class="py-2 text-right">
                <span class="inline-flex items-center gap-1 text-xs font-semibold text-[var(--shk-color-primary)]">
                  Ver detalle
                  <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" class="h-3.5 w-3.5">
                    <path stroke-linecap="round" stroke-linejoin="round" d="m8.25 4.5 7.5 7.5-7.5 7.5" />
                  </svg>
                </span>
              </td>
            </tr>
          } @empty {
            <tr><td colspan="5" class="py-6 text-center text-slate-400">Sin registros que coincidan con el filtro.</td></tr>
          }
        </tbody>
      </table>
    </shk-card>

    <shk-payment-detail-drawer
      [open]="selectedStudentId() !== null"
      [student]="selectedStudent()"
      [months]="period()?.allMonthCodes ?? []"
      (close)="selectedStudentId.set(null)"
      (changed)="refreshTick.update(n => n + 1)"
    />
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
export class PaymentsComponent {
  private readonly api = inject(ApiClient);
  private readonly toast = inject(ToastService);

  protected readonly issuing = signal(false);
  protected readonly refreshTick = signal(0);

  protected readonly searchText = signal('');
  protected readonly regionFilter = signal<string | null>(null);
  protected readonly selectedMonth = signal<string | null>(null);

  /** Alumno cuyo drawer de detalle está abierto (por id, no por referencia — así el drawer siempre
   * recibe la fila fresca de `matrix()` después de marcar/revertir/enviar un recibo, en vez de
   * quedarse con una copia vieja de `paidByMonth`). */
  protected readonly selectedStudentId = signal<string | null>(null);

  protected readonly regions = toSignal(
    this.api.get<RegionListItemDto[]>('/catalog/regions').pipe(catchError(() => of<RegionListItemDto[]>([]))),
    { initialValue: [] as RegionListItemDto[] },
  );

  protected readonly period = toSignal(
    this.api.get<CurrentPeriodResponseDto | null>('/academic/periods/current').pipe(
      tap((p) => {
        if (p && !this.selectedMonth() && p.monthCodes.length > 0) {
          const todayCode = this.currentMonthCode();
          const lastMonth = p.monthCodes[p.monthCodes.length - 1] ?? null;
          this.selectedMonth.set(p.monthCodes.includes(todayCode) ? todayCode : lastMonth);
        }
      }),
      // Nota: el mes SELECCIONADO por defecto (para el resumen financiero) usa `monthCodes` (meses ya
      // transcurridos) a propósito — no tiene sentido mostrar "cobranza esperada" de un mes futuro.
      // `allMonthCodes` solo se usa para las COLUMNAS de la matriz/drawer, donde sí queremos dejar
      // marcar por adelantado.
      catchError(() => of(null)),
    ),
    { initialValue: null },
  );

  protected readonly matrix = toSignal(
    combineLatest([toObservable(this.refreshTick), toObservable(this.period)]).pipe(
      switchMap(([, period]) => {
        if (!period) return of<PagedResultDto<StudentPaymentRowDto>>({ items: [], page: 1, pageSize: 500, totalItems: 0, totalPages: 0, months: [] });
        return this.api
          .get<PagedResultDto<StudentPaymentRowDto>>('/payments/matrix', { periodId: period.id, page: 1, pageSize: 500 })
          .pipe(catchError(() => of<PagedResultDto<StudentPaymentRowDto>>({ items: [], page: 1, pageSize: 500, totalItems: 0, totalPages: 0, months: [] })));
      }),
    ),
    { initialValue: null },
  );

  protected readonly summary = toSignal(
    combineLatest([toObservable(this.refreshTick), toObservable(this.selectedMonth), toObservable(this.regionFilter)]).pipe(
      switchMap(([, monthCode, regionId]) => {
        if (!monthCode) return of<BillingSummaryResponseDto | null>(null);
        return this.api
          .get<BillingSummaryResponseDto>('/payments/summary', { monthCode, regionId: regionId ?? undefined })
          .pipe(catchError(() => of<BillingSummaryResponseDto | null>(null)));
      }),
    ),
    { initialValue: null },
  );

  protected readonly filteredRows = computed(() => {
    const items = this.matrix()?.items ?? [];
    const search = this.searchText().trim().toLowerCase();
    const region = this.regionFilter();

    return items.filter((row) => {
      const matchesSearch = !search || row.studentName.toLowerCase().includes(search) || String(row.enrollmentNumber).includes(search);
      const matchesRegion = !region || this.regions().find((r) => r.id === region)?.name === row.regionName;
      return matchesSearch && matchesRegion;
    });
  });

  /** Fila fresca (de `matrix()`, no una copia) del alumno seleccionado — null si no hay ninguno abierto. */
  protected readonly selectedStudent = computed<StudentPaymentRowDto | null>(() => {
    const id = this.selectedStudentId();
    if (!id) return null;
    return this.matrix()?.items.find((row) => row.studentId === id) ?? null;
  });

  protected formatMonth(monthCode: string): string {
    return formatMonthCode(monthCode);
  }

  /** Reemplaza las columnas de mes en la tabla (feedback real: nadie las necesitaba de un vistazo,
   * y sobrecargaban la fila) por un conteo simple de meses pendientes del cuatrimestre completo. */
  protected monthsDueCount(row: StudentPaymentRowDto): number {
    return row.monthsDue.length;
  }

  private currentMonthCode(): string {
    const now = new Date();
    return `${now.getFullYear()}${String(now.getMonth() + 1).padStart(2, '0')}`;
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
