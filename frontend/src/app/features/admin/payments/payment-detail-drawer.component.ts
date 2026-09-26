import { Component, ElementRef, effect, inject, input, output, signal, viewChild } from '@angular/core';
import { of } from 'rxjs';
import { catchError, tap } from 'rxjs/operators';
import { ApiClient } from '../../../core/http/api-client';
import { SendPaymentReceiptResponseDto, StudentPaymentRowDto } from '../../../api/schema';
import { ButtonComponent } from '../../../shared/ui/button/button.component';
import { ToastService } from '../../../shared/ui/toast/toast.service';

const MONTH_NAMES = [
  'enero', 'febrero', 'marzo', 'abril', 'mayo', 'junio', 'julio', 'agosto', 'septiembre', 'octubre', 'noviembre', 'diciembre',
];

/** Cuota fija mensual (RN de negocio: $500 MXN, ver RegisterManualPaymentCommandHandler.FixedMonthlyQuota
 * en el backend). Se usa aquí solo para armar el texto del mensaje de WhatsApp en el cliente, sin
 * pedirle nada al backend — el botón de WhatsApp así abre al instante, sin esperar una respuesta. */
const FIXED_MONTHLY_QUOTA = 500;

function formatMonthLong(monthCode: string): string {
  if (monthCode.length !== 6) return monthCode;
  const year = monthCode.slice(0, 4);
  const month = Number(monthCode.slice(4, 6));
  return `${MONTH_NAMES[month - 1] ?? monthCode} ${year}`;
}

type MonthAction = 'mark' | 'undo' | 'receipt';

/**
 * Panel de verificación de pagos (docs/Plan-Panel-Pagos.md): drawer de detalle por alumno, con una
 * cuadrícula mes por mes — reemplaza el clic directo sobre la celda "Pendiente"/"Pagado" de la
 * matriz (poco intuitivo, según feedback real) por una pantalla dedicada donde cada acción tiene
 * su propio botón con texto e ícono, nunca un badge que "resulta" ser clickeable.
 *
 * Regla de diseño deliberada: marcar un mes como pagado NO envía el recibo automáticamente. Son
 * dos pasos a propósito — si alguien marca el mes equivocado, lo revierte sin que ya se le haya
 * mandado un correo/WhatsApp de confirmación al alumno. Para compensar la fricción, en cuanto se
 * marca un mes aparece de inmediato el botón "Enviar recibo" en ese mismo mes, sin tener que
 * buscarlo.
 */
@Component({
  selector: 'shk-payment-detail-drawer',
  imports: [ButtonComponent],
  template: `
    @if (open()) {
      <div class="fixed inset-0 z-40 flex justify-end" (keydown.escape)="close.emit()">
        <div class="absolute inset-0 bg-slate-900/40 backdrop-blur-[2px]" (click)="close.emit()"></div>

        <aside
          #panel
          class="relative flex h-full w-full max-w-2xl flex-col bg-[var(--shk-color-surface)] shadow-2xl focus:outline-none"
          role="dialog"
          aria-modal="true"
          tabindex="-1"
        >
          @if (student(); as s) {
            <header class="flex items-start justify-between gap-4 border-b border-slate-100 px-6 py-5">
              <div>
                <h2 class="text-xl font-bold text-slate-900">{{ s.studentName }}</h2>
                <p class="mt-1 text-sm text-slate-500">Matrícula {{ s.enrollmentNumber }} · {{ s.regionName }}</p>
                <p class="mt-1.5 flex flex-wrap items-center gap-x-3 gap-y-1 text-xs text-slate-500">
                  <span class="inline-flex items-center gap-1">
                    <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" class="h-3.5 w-3.5">
                      <path stroke-linecap="round" stroke-linejoin="round" d="M21.75 6.75v10.5a2.25 2.25 0 0 1-2.25 2.25h-15a2.25 2.25 0 0 1-2.25-2.25V6.75m19.5 0a2.25 2.25 0 0 0-2.25-2.25h-15a2.25 2.25 0 0 0-2.25 2.25m19.5 0v.243a2.25 2.25 0 0 1-1.07 1.916l-7.5 4.615a2.25 2.25 0 0 1-2.36 0L3.32 8.91a2.25 2.25 0 0 1-1.07-1.916V6.75" />
                    </svg>
                    {{ s.email || 'Sin correo registrado' }}
                  </span>
                  <span class="inline-flex items-center gap-1">
                    <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" class="h-3.5 w-3.5">
                      <path stroke-linecap="round" stroke-linejoin="round" d="M2.25 6.75c0 8.284 6.716 15 15 15h1.5a2.25 2.25 0 0 0 2.25-2.25v-1.372c0-.516-.351-.966-.852-1.091l-4.423-1.106c-.44-.11-.902.055-1.173.417l-.97 1.293c-.282.376-.769.542-1.21.38a12.035 12.035 0 0 1-7.143-7.143c-.162-.441.004-.928.38-1.21l1.293-.97c.363-.271.527-.734.417-1.173L6.963 3.102a1.125 1.125 0 0 0-1.091-.852H4.5A2.25 2.25 0 0 0 2.25 4.5v2.25Z" />
                    </svg>
                    {{ s.phone || 'Sin teléfono registrado' }}
                  </span>
                </p>
              </div>
              <button
                type="button"
                class="rounded-full p-2 text-slate-400 transition hover:bg-slate-100 hover:text-slate-600"
                aria-label="Cerrar"
                (click)="close.emit()"
              >
                <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" class="h-5 w-5">
                  <path stroke-linecap="round" stroke-linejoin="round" d="M6 18 18 6M6 6l12 12" />
                </svg>
              </button>
            </header>

            <div class="flex-1 overflow-y-auto px-6 py-5">
              <p class="rounded-lg bg-slate-50 px-3 py-2.5 text-xs leading-relaxed text-slate-500">
                Haz clic en un mes <strong class="text-slate-700">pendiente</strong> para marcarlo como pagado. En un mes ya
                pagado puedes <strong class="text-slate-700">enviarlo por correo</strong> (se descarga el PDF a la vez),
                <strong class="text-slate-700">enviarlo por WhatsApp</strong>, o <strong class="text-slate-700">revertir</strong>
                el pago si fue un error.
              </p>

              <div class="mt-4 grid grid-cols-2 gap-3 sm:grid-cols-3">
                @for (month of months(); track month) {
                  <div
                    class="rounded-xl border p-3 transition"
                    [class.border-emerald-200]="isPaid(month)"
                    [class.bg-emerald-50]="isPaid(month)"
                    [class.border-red-200]="!isPaid(month) && isOverdue(month)"
                    [class.bg-red-50]="!isPaid(month) && isOverdue(month)"
                    [class.border-slate-200]="!isPaid(month) && !isOverdue(month)"
                    [class.bg-white]="!isPaid(month) && !isOverdue(month)"
                  >
                    <p class="text-sm font-semibold capitalize text-slate-800">{{ formatMonth(month) }}</p>

                    @if (isPaid(month)) {
                      <div class="mt-2 flex items-center gap-1.5 text-xs font-medium text-emerald-700">
                        <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" class="h-4 w-4">
                          <path stroke-linecap="round" stroke-linejoin="round" d="m4.5 12.75 6 6 9-13.5" />
                        </svg>
                        Pagado
                      </div>
                      <div class="mt-3 flex flex-col gap-1.5">
                        <button
                          type="button"
                          class="inline-flex items-center justify-center gap-1.5 rounded-lg bg-[var(--shk-color-accent)] px-2.5 py-1.5 text-xs font-semibold text-[var(--shk-color-primary-dark)] transition hover:bg-[var(--shk-color-accent-dark)] disabled:cursor-wait disabled:opacity-60"
                          [disabled]="isBusy(month)"
                          (click)="sendReceiptByEmail(month)"
                        >
                          <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" class="h-3.5 w-3.5">
                            <path stroke-linecap="round" stroke-linejoin="round" d="M21.75 6.75v10.5a2.25 2.25 0 0 1-2.25 2.25h-15a2.25 2.25 0 0 1-2.25-2.25V6.75m19.5 0a2.25 2.25 0 0 0-2.25-2.25h-15a2.25 2.25 0 0 0-2.25 2.25m19.5 0v.243a2.25 2.25 0 0 1-1.07 1.916l-7.5 4.615a2.25 2.25 0 0 1-2.36 0L3.32 8.91a2.25 2.25 0 0 1-1.07-1.916V6.75" />
                          </svg>
                          @if (activeAction(month) === 'receipt') {
                            Enviando…
                          } @else if (s.email) {
                            Enviar por correo y descargar
                          } @else {
                            Descargar recibo
                          }
                        </button>
                        @if (s.phone) {
                          <button
                            type="button"
                            class="inline-flex items-center justify-center gap-1.5 rounded-lg bg-emerald-50 px-2.5 py-1.5 text-xs font-semibold text-emerald-700 transition hover:bg-emerald-100 disabled:cursor-wait disabled:opacity-60"
                            [disabled]="isBusy(month)"
                            (click)="sendReceiptByWhatsApp(month)"
                          >
                            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="currentColor" class="h-3.5 w-3.5">
                              <path d="M12.04 2.003c-5.523 0-10 4.477-10 10 0 1.766.463 3.42 1.27 4.856L2 22l5.25-1.276a9.94 9.94 0 0 0 4.79 1.222h.004c5.523 0 10-4.477 10-10s-4.477-9.943-10.004-9.943Zm5.824 14.152c-.245.69-1.213 1.263-1.99 1.428-.53.11-1.222.199-3.553-.763-2.983-1.234-4.901-4.253-5.05-4.451-.148-.198-1.208-1.61-1.208-3.07 0-1.46.767-2.178 1.04-2.475.245-.267.578-.386.923-.387.111 0 .234.006.335.011.294.013.442.03.635.492.245.586.837 2.024.91 2.172.074.148.124.32.025.518-.099.198-.148.32-.297.494-.148.173-.31.386-.443.518-.148.148-.302.31-.13.607.173.297.767 1.267 1.647 2.05 1.132 1.01 2.087 1.322 2.383 1.47.297.148.47.124.643-.074.173-.198.742-.866.94-1.163.197-.297.395-.247.667-.148.272.098 1.708.806 2.001.953.293.148.487.222.56.346.074.124.074.717-.171 1.407Z" />
                            </svg>
                            Enviar por WhatsApp
                          </button>
                        }
                        <button
                          type="button"
                          class="inline-flex items-center justify-center gap-1.5 rounded-lg border border-slate-200 px-2.5 py-1.5 text-xs font-medium text-slate-600 transition hover:bg-slate-100 disabled:cursor-wait disabled:opacity-60"
                          [disabled]="isBusy(month)"
                          (click)="undo(month)"
                        >
                          <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" class="h-3.5 w-3.5">
                            <path stroke-linecap="round" stroke-linejoin="round" d="M9 15 3 9m0 0 6-6M3 9h12a6 6 0 0 1 0 12h-3" />
                          </svg>
                          {{ activeAction(month) === 'undo' ? 'Revirtiendo…' : 'Revertir pago' }}
                        </button>
                      </div>
                    } @else {
                      <button
                        type="button"
                        class="mt-2 inline-flex w-full items-center justify-center gap-1.5 rounded-lg px-2.5 py-2 text-xs font-semibold transition disabled:cursor-wait disabled:opacity-60"
                        [class.bg-red-600]="isOverdue(month)"
                        [class.text-white]="isOverdue(month)"
                        [class.hover:bg-red-700]="isOverdue(month)"
                        [class.bg-slate-100]="!isOverdue(month)"
                        [class.text-slate-600]="!isOverdue(month)"
                        [class.hover:bg-slate-200]="!isOverdue(month)"
                        [disabled]="isBusy(month)"
                        (click)="markPaid(month)"
                      >
                        <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" class="h-3.5 w-3.5">
                          <path stroke-linecap="round" stroke-linejoin="round" d="M12 4.5v15m7.5-7.5h-15" />
                        </svg>
                        {{ activeAction(month) === 'mark' ? 'Marcando…' : isOverdue(month) ? 'Vencido — marcar pagado' : 'Marcar como pagado' }}
                      </button>
                    }
                  </div>
                }
              </div>
            </div>

            <footer class="flex items-center justify-end border-t border-slate-100 px-6 py-4">
              <shk-button variant="ghost" (click)="close.emit()">Cerrar</shk-button>
            </footer>
          }
        </aside>
      </div>
    }
  `,
})
export class PaymentDetailDrawerComponent {
  private readonly api = inject(ApiClient);
  private readonly toast = inject(ToastService);
  private readonly panelRef = viewChild<ElementRef<HTMLElement>>('panel');

  readonly open = input<boolean>(false);
  readonly student = input<StudentPaymentRowDto | null>(null);
  readonly months = input<string[]>([]);
  readonly close = output<void>();
  readonly changed = output<void>();

  /** "<monthCode>" del mes en vuelo + qué acción, para deshabilitar solo ese mes mientras responde el backend. */
  private readonly busyMonth = signal<string | null>(null);
  private readonly busyAction = signal<MonthAction | null>(null);

  private lastFocusedElement: HTMLElement | null = null;

  constructor() {
    effect(() => {
      if (!this.open()) {
        if (this.lastFocusedElement) {
          this.lastFocusedElement.focus();
          this.lastFocusedElement = null;
        }
        return;
      }

      this.lastFocusedElement = (document.activeElement as HTMLElement) ?? null;
      queueMicrotask(() => this.panelRef()?.nativeElement.focus());
    });
  }

  protected formatMonth(monthCode: string): string {
    return formatMonthLong(monthCode);
  }

  protected isPaid(monthCode: string): boolean {
    return this.student()?.paidByMonth[monthCode] === true;
  }

  protected isOverdue(monthCode: string): boolean {
    const now = new Date();
    const currentCode = `${now.getFullYear()}${String(now.getMonth() + 1).padStart(2, '0')}`;
    return monthCode < currentCode;
  }

  protected isBusy(monthCode: string): boolean {
    return this.busyMonth() === monthCode;
  }

  protected activeAction(monthCode: string): MonthAction | null {
    return this.busyMonth() === monthCode ? this.busyAction() : null;
  }

  protected markPaid(monthCode: string): void {
    const s = this.student();
    if (!s || this.busyMonth() !== null) return;

    this.busyMonth.set(monthCode);
    this.busyAction.set('mark');

    this.api
      .post('/payments/manual', { studentId: s.studentId, monthCode })
      .pipe(
        tap({
          next: () => {
            this.toast.success(`${this.formatMonth(monthCode)} marcado como pagado.`);
            this.changed.emit();
          },
          error: () => this.toast.error('No se pudo registrar el pago.'),
        }),
        catchError(() => of(null)),
      )
      .subscribe(() => {
        this.busyMonth.set(null);
        this.busyAction.set(null);
      });
  }

  protected undo(monthCode: string): void {
    const s = this.student();
    if (!s || this.busyMonth() !== null) return;

    this.busyMonth.set(monthCode);
    this.busyAction.set('undo');

    this.api
      .delete('/payments/manual', { studentId: s.studentId, monthCode })
      .pipe(
        tap({
          next: () => {
            this.toast.success(`Se revirtió el pago de ${this.formatMonth(monthCode)}.`);
            this.changed.emit();
          },
          error: () => this.toast.error('No se pudo revertir el pago.'),
        }),
        catchError(() => of(null)),
      )
      .subscribe(() => {
        this.busyMonth.set(null);
        this.busyAction.set(null);
      });
  }

  /** Botón 1: genera el recibo, lo manda por correo (automático, vía outbox) y a la vez lo descarga
   * en el navegador del admin — así se puede confirmar/reenviar aunque el correo tarde en llegar. */
  protected sendReceiptByEmail(monthCode: string): void {
    const s = this.student();
    if (!s || this.busyMonth() !== null) return;

    this.busyMonth.set(monthCode);
    this.busyAction.set('receipt');

    this.api
      .post<SendPaymentReceiptResponseDto>('/payments/receipt', { studentId: s.studentId, monthCode })
      .pipe(
        tap({
          next: (response) => {
            if (response.sentTo) {
              this.toast.success(`Recibo enviado por correo a ${response.sentTo} y descargado.`);
            } else {
              this.toast.success('Recibo descargado (el alumno no tiene correo registrado, no se pudo enviar).');
            }
            this.downloadPdf(response.pdfBase64, response.fileName);
          },
          error: () => this.toast.error('No se pudo generar/enviar el recibo.'),
        }),
        catchError(() => of(null)),
      )
      .subscribe(() => {
        this.busyMonth.set(null);
        this.busyAction.set(null);
      });
  }

  /** Botón 2: abre WhatsApp (wa.me) con el mensaje ya armado — 100% en el cliente, con los datos que
   * ya trae la fila del alumno (teléfono, nombre) y la cuota fija, sin llamar al backend ni generar
   * el PDF de nuevo. Así abre al instante y no depende de que el correo se haya podido enviar. */
  protected sendReceiptByWhatsApp(monthCode: string): void {
    const s = this.student();
    if (!s) return;

    if (!s.phone) {
      this.toast.error('Este alumno no tiene un teléfono registrado.');
      return;
    }

    const message =
      `Hola ${s.studentName}, confirmamos tu pago de ${this.formatMonth(monthCode)} por $${FIXED_MONTHLY_QUOTA.toFixed(2)} MXN. ` +
      'Instituto Teológico Shekinah';
    const url = `https://wa.me/52${s.phone}?text=${encodeURIComponent(message)}`;
    window.open(url, '_blank', 'noopener');
  }

  private downloadPdf(base64: string, fileName: string): void {
    const bytes = atob(base64);
    const buffer = new Uint8Array(bytes.length);
    for (let i = 0; i < bytes.length; i++) {
      buffer[i] = bytes.charCodeAt(i);
    }

    const blob = new Blob([buffer], { type: 'application/pdf' });
    const url = URL.createObjectURL(blob);
    const anchor = document.createElement('a');
    anchor.href = url;
    anchor.download = fileName;
    anchor.click();
    URL.revokeObjectURL(url);
  }
}
