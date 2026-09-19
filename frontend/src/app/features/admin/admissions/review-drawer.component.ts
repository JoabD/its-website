import { Component, ElementRef, effect, inject, input, output, signal, viewChild } from '@angular/core';
import { DatePipe } from '@angular/common';
import { of } from 'rxjs';
import { catchError, finalize } from 'rxjs/operators';
import { ApiClient } from '../../../core/http/api-client';
import { ApplicationDetailDto, ApproveApplicationResponseDto, ChecklistItemStateDto } from '../../../api/schema';
import { ButtonComponent } from '../../../shared/ui/button/button.component';
import { BadgeComponent, BadgeTone } from '../../../shared/ui/badge/badge.component';
import { ToastService } from '../../../shared/ui/toast/toast.service';
import { APPLICATION_STATUS_LABELS, ApplicationStatus, MODALITY_LABELS, Modality } from '../../../domain/models';

/**
 * Panel de revisión (estilo drawer, spec §2.2): ficha completa + checklist dinámico (catálogo vivo
 * de Configuración → Documentos de inscripción) antes de poder inscribir. Vive como hijo embebido
 * de AdmissionsComponent — no es una ruta propia — para que abrir/cerrar sea instantáneo y no
 * pierda el estado de la bandeja (página, filtro, búsqueda) detrás.
 *
 * Detalles de UX definidos en la spec y confirmados por el usuario:
 *  - Encabezado y pie fijos, cuerpo con scroll.
 *  - El checklist se autoguarda en cada click (sin botón "Guardar"), con un check discreto que se
 *    desvanece en vez de un toast por cada marca — los toasts se reservan para la decisión final.
 *  - Contador en vivo "X de Y documentos verificados".
 *  - "Rechazar" revela el campo de motivo solo cuando se va a usar, para mantener el panel limpio.
 *  - Skeleton mientras carga (no un spinner genérico), Esc cierra, foco regresa a quien abrió el drawer.
 *  - Ancho completo en pantallas angostas (w-full + max-w-xl: en móvil el ancho de la ventana manda).
 */
@Component({
  selector: 'shk-application-review-drawer',
  imports: [ButtonComponent, BadgeComponent, DatePipe],
  template: `
    @if (applicationId()) {
      <div class="fixed inset-0 z-40 flex justify-end" (keydown.escape)="close.emit()">
        <div class="absolute inset-0 bg-slate-900/40 backdrop-blur-[2px]" (click)="close.emit()"></div>

        <aside
          #panel
          class="relative flex h-full w-full max-w-xl flex-col bg-[var(--shk-color-surface)] shadow-2xl focus:outline-none"
          role="dialog"
          aria-modal="true"
          tabindex="-1"
        >
          @if (loading()) {
            <div class="animate-pulse px-6 py-5">
              <div class="h-3 w-24 rounded bg-slate-200"></div>
              <div class="mt-3 h-6 w-56 rounded bg-slate-200"></div>
              <div class="mt-3 h-5 w-28 rounded-full bg-slate-200"></div>
              <div class="mt-8 space-y-3">
                <div class="h-3 w-32 rounded bg-slate-200"></div>
                <div class="h-4 w-full rounded bg-slate-100"></div>
                <div class="h-4 w-4/5 rounded bg-slate-100"></div>
              </div>
              <div class="mt-8 space-y-3">
                <div class="h-3 w-32 rounded bg-slate-200"></div>
                <div class="h-4 w-full rounded bg-slate-100"></div>
                <div class="h-4 w-3/5 rounded bg-slate-100"></div>
              </div>
              <div class="mt-8 h-28 w-full rounded-xl bg-slate-100"></div>
            </div>
          } @else if (loadError()) {
            <div class="flex flex-1 flex-col items-center justify-center gap-3 px-6 text-center">
              <p class="text-sm text-slate-500">No se pudo cargar la solicitud.</p>
              <shk-button variant="secondary" (click)="load()">Reintentar</shk-button>
            </div>
          } @else if (detail(); as d) {
            <header class="flex items-start justify-between gap-4 border-b border-slate-100 px-6 py-5">
              <div>
                <p class="text-xs font-semibold uppercase tracking-wide text-slate-400">Folio {{ d.folio }}</p>
                <h2 class="mt-1 text-xl font-bold text-slate-900">{{ d.fullName }}</h2>
                <div class="mt-2 flex flex-wrap items-center gap-2">
                  <shk-badge [tone]="statusTone(d.status)">{{ statusLabel(d.status) }}</shk-badge>
                  @if (d.approvedViaQuickAction) {
                    <shk-badge tone="neutral">Aprobación rápida</shk-badge>
                  }
                  @if (d.purgeScheduledAtUtc) {
                    <span class="text-xs text-slate-400">Se elimina el {{ d.purgeScheduledAtUtc | date: 'dd/MM/yyyy' }}</span>
                  }
                </div>
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
              <section>
                <h3 class="text-sm font-semibold text-[var(--shk-color-primary)]">Datos personales</h3>
                <dl class="mt-3 grid grid-cols-1 gap-x-4 gap-y-3 text-sm sm:grid-cols-2">
                  <div><dt class="text-slate-400">Fecha de nacimiento</dt><dd class="text-slate-700">{{ d.birthDate | date: 'dd/MM/yyyy' }}</dd></div>
                  <div><dt class="text-slate-400">Estado civil</dt><dd class="text-slate-700">{{ d.maritalStatus }}</dd></div>
                  <div><dt class="text-slate-400">Correo</dt><dd class="text-slate-700 break-all">{{ d.email }}</dd></div>
                  <div><dt class="text-slate-400">Teléfono</dt><dd class="text-slate-700">{{ d.phone }}</dd></div>
                </dl>
              </section>

              <section class="mt-6">
                <h3 class="text-sm font-semibold text-[var(--shk-color-primary)]">Domicilio</h3>
                <p class="mt-3 text-sm leading-relaxed text-slate-700">
                  {{ d.street }}, {{ d.neighborhood }}, {{ d.locality }}, {{ d.municipality }}@if (d.state) {, {{ d.state }}}
                </p>
              </section>

              <section class="mt-6">
                <h3 class="text-sm font-semibold text-[var(--shk-color-primary)]">Iglesia y referencia pastoral</h3>
                <dl class="mt-3 grid grid-cols-1 gap-x-4 gap-y-3 text-sm sm:grid-cols-2">
                  <div class="sm:col-span-2"><dt class="text-slate-400">Iglesia</dt><dd class="text-slate-700">{{ d.churchName }}</dd></div>
                  <div class="sm:col-span-2"><dt class="text-slate-400">Domicilio de la iglesia</dt><dd class="text-slate-700">{{ d.churchStreet }}, {{ d.churchNeighborhood }}, {{ d.churchLocality }}, {{ d.churchMunicipality }}</dd></div>
                  <div><dt class="text-slate-400">Pastor</dt><dd class="text-slate-700">{{ d.pastorName }}</dd></div>
                  <div><dt class="text-slate-400">Tiempo asistiendo</dt><dd class="text-slate-700">{{ d.timeAttending }}</dd></div>
                  <div class="sm:col-span-2">
                    <dt class="text-slate-400">Rol ministerial</dt>
                    <dd class="text-slate-700">{{ d.hasMinistryRole ? (d.ministryRoleName || 'Sí') : 'Ninguno' }}</dd>
                  </div>
                </dl>
              </section>

              <section class="mt-6">
                <h3 class="text-sm font-semibold text-[var(--shk-color-primary)]">Formación y motivación</h3>
                <dl class="mt-3 grid grid-cols-1 gap-x-4 gap-y-3 text-sm sm:grid-cols-2">
                  <div class="sm:col-span-2">
                    <dt class="text-slate-400">Escolaridad</dt>
                    <dd class="text-slate-700">{{ d.educationLevel }}{{ d.otherEducationDescription ? ' — ' + d.otherEducationDescription : '' }}</dd>
                  </div>
                  <div class="sm:col-span-2"><dt class="text-slate-400">Formación teológica previa</dt><dd class="text-slate-700">{{ d.theologicalBackground }}</dd></div>
                  <div class="sm:col-span-2"><dt class="text-slate-400">Propósito de estudio</dt><dd class="text-slate-700">{{ d.studyPurpose }}</dd></div>
                </dl>
              </section>

              <section class="mt-6">
                <h3 class="text-sm font-semibold text-[var(--shk-color-primary)]">Modalidad</h3>
                <dl class="mt-3 grid grid-cols-1 gap-x-4 gap-y-3 text-sm sm:grid-cols-2">
                  <div><dt class="text-slate-400">Modalidad</dt><dd class="text-slate-700">{{ modalityLabel(d.modality) }}</dd></div>
                  <div><dt class="text-slate-400">Región</dt><dd class="text-slate-700">{{ d.regionName }}</dd></div>
                  @if (d.onlineReason) {
                    <div class="sm:col-span-2"><dt class="text-slate-400">Motivo de modalidad virtual</dt><dd class="text-slate-700">{{ d.onlineReason }}</dd></div>
                  }
                </dl>
              </section>

              @if (d.status !== 'Pending') {
                <section class="mt-6 rounded-xl bg-slate-50 p-4">
                  <h3 class="text-sm font-semibold text-[var(--shk-color-primary)]">Decisión</h3>
                  <p class="mt-2 text-sm text-slate-700">
                    {{ statusLabel(d.status) }} el {{ d.decidedAtUtc | date: 'dd/MM/yyyy HH:mm' }}
                    @if (d.decisionReason) { — {{ d.decisionReason }} }
                  </p>
                </section>
              }

              @if (d.status === 'Pending') {
                <section class="mt-6 rounded-xl border border-[var(--shk-color-accent)]/25 bg-[var(--shk-color-accent)]/[0.06] p-4">
                  <div class="flex items-center justify-between">
                    <h3 class="text-sm font-semibold text-[var(--shk-color-primary)]">Checklist de documentos</h3>
                    <span class="text-xs font-semibold text-slate-500">{{ checkedCount() }} de {{ checklistItems().length }} verificados</span>
                  </div>
                  @if (checklistItems().length === 0) {
                    <p class="mt-2 text-xs text-slate-500">No hay documentos configurados en el catálogo — se puede inscribir sin checklist.</p>
                  } @else {
                    <ul class="mt-3 space-y-1">
                      @for (item of checklistItems(); track item.id) {
                        <li>
                          <label class="flex cursor-pointer items-center gap-3 rounded-lg px-2 py-1.5 transition hover:bg-white">
                            <input
                              type="checkbox"
                              class="h-4 w-4 rounded border-slate-300 text-[var(--shk-color-accent)] focus:ring-[var(--shk-color-accent)]"
                              [checked]="item.checked"
                              [disabled]="savingChecklist()"
                              (change)="toggleChecklist(item.id, $any($event.target).checked)"
                            />
                            <span class="flex-1 text-sm text-slate-700">{{ item.label }}</span>
                            @if (justSavedId() === item.id) {
                              <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2" class="h-4 w-4 text-emerald-600">
                                <path stroke-linecap="round" stroke-linejoin="round" d="m4.5 12.75 6 6 9-13.5" />
                              </svg>
                            }
                          </label>
                        </li>
                      }
                    </ul>
                  }
                </section>

                @if (rejecting()) {
                  <section class="mt-4 rounded-xl border border-red-100 bg-red-50/60 p-4">
                    <label class="text-sm font-semibold text-red-700" for="reject-reason">Motivo de rechazo</label>
                    <textarea
                      id="reject-reason"
                      rows="2"
                      class="mt-2 w-full rounded-lg border border-red-200 bg-white px-3 py-2 text-sm focus:border-red-400 focus:outline-none"
                      placeholder="Explica brevemente por qué se rechaza…"
                      [value]="rejectReason()"
                      (input)="rejectReason.set($any($event.target).value)"
                    ></textarea>
                    <div class="mt-3 flex justify-end gap-2">
                      <shk-button variant="ghost" (click)="cancelReject()">Cancelar</shk-button>
                      <shk-button variant="danger" [loading]="deciding()" (click)="reject()">Confirmar rechazo</shk-button>
                    </div>
                  </section>
                }
              }
            </div>

            @if (d.status === 'Pending' && !rejecting()) {
              <footer class="flex flex-col-reverse gap-3 border-t border-slate-100 px-6 py-4 sm:flex-row sm:items-center sm:justify-end">
                <shk-button variant="danger" [loading]="deciding()" (click)="rejecting.set(true)">Rechazar</shk-button>
                <shk-button variant="primary" [disabled]="!checklistComplete()" [loading]="deciding()" (click)="approve()">
                  Inscribir
                </shk-button>
              </footer>
            }
          } @else {
            <div class="flex flex-1 items-center justify-center px-6 text-center text-sm text-slate-400">
              No se pudo cargar la solicitud.
            </div>
          }
        </aside>
      </div>
    }
  `,
})
export class ApplicationReviewDrawerComponent {
  private readonly api = inject(ApiClient);
  private readonly toast = inject(ToastService);
  private readonly panelRef = viewChild<ElementRef<HTMLElement>>('panel');

  readonly applicationId = input<string | null>(null);
  readonly close = output<void>();
  readonly decided = output<void>();

  protected readonly loading = signal(false);
  protected readonly loadError = signal(false);
  protected readonly deciding = signal(false);
  protected readonly savingChecklist = signal(false);
  protected readonly detail = signal<ApplicationDetailDto | null>(null);
  protected readonly checklistItems = signal<ChecklistItemStateDto[]>([]);
  protected readonly justSavedId = signal<string | null>(null);
  protected readonly rejecting = signal(false);
  protected readonly rejectReason = signal('');

  protected readonly checkedCount = () => this.checklistItems().filter((i) => i.checked).length;
  protected readonly checklistComplete = () => this.checklistItems().every((i) => i.checked);

  private lastFocusedElement: HTMLElement | null = null;
  private justSavedTimeout?: ReturnType<typeof setTimeout>;

  constructor() {
    effect(() => {
      const id = this.applicationId();
      if (!id) {
        if (this.lastFocusedElement) {
          this.lastFocusedElement.focus();
          this.lastFocusedElement = null;
        }
        this.detail.set(null);
        this.rejecting.set(false);
        this.rejectReason.set('');
        return;
      }

      this.lastFocusedElement = (document.activeElement as HTMLElement) ?? null;
      this.rejecting.set(false);
      this.rejectReason.set('');
      this.load();
      queueMicrotask(() => this.panelRef()?.nativeElement.focus());
    });
  }

  protected load(): void {
    const id = this.applicationId();
    if (!id) return;

    this.loading.set(true);
    this.loadError.set(false);
    this.api
      .get<ApplicationDetailDto>(`/admissions/applications/${id}`)
      .pipe(
        catchError(() => of(null)),
        finalize(() => this.loading.set(false)),
      )
      .subscribe((result) => {
        this.detail.set(result);
        this.loadError.set(!result);
        if (result) this.checklistItems.set(result.checklist.items);
      });
  }

  protected modalityLabel(modality: Modality): string {
    return MODALITY_LABELS[modality];
  }

  protected statusLabel(status: ApplicationStatus): string {
    return APPLICATION_STATUS_LABELS[status];
  }

  protected statusTone(status: ApplicationStatus): BadgeTone {
    return status === 'Approved' ? 'success' : status === 'Rejected' ? 'danger' : 'warning';
  }

  protected toggleChecklist(itemId: string, checked: boolean): void {
    const id = this.applicationId();
    if (!id) return;

    const previous = this.checklistItems();
    const next = previous.map((i) => (i.id === itemId ? { ...i, checked } : i));
    this.checklistItems.set(next);
    this.savingChecklist.set(true);

    this.api
      .patch(`/admissions/applications/${id}/checklist`, { checkedItemIds: next.filter((i) => i.checked).map((i) => i.id) })
      .pipe(
        catchError(() => {
          this.checklistItems.set(previous);
          this.toast.error('No se pudo guardar el checklist.');
          return of(null);
        }),
        finalize(() => this.savingChecklist.set(false)),
      )
      .subscribe((result) => {
        if (result === null) return;
        clearTimeout(this.justSavedTimeout);
        this.justSavedId.set(itemId);
        this.justSavedTimeout = setTimeout(() => this.justSavedId.set(null), 1400);
      });
  }

  protected cancelReject(): void {
    this.rejecting.set(false);
    this.rejectReason.set('');
  }

  protected approve(): void {
    const id = this.applicationId();
    if (!id || !this.checklistComplete()) return;

    this.deciding.set(true);
    this.api
      .post<ApproveApplicationResponseDto>(`/admissions/applications/${id}/approve`, { viaQuickAction: false })
      .pipe(
        catchError(() => {
          this.toast.error('No se pudo inscribir al solicitante.');
          return of(null);
        }),
        finalize(() => this.deciding.set(false)),
      )
      .subscribe((response) => {
        if (!response) return;
        this.toast.success(`Solicitante inscrito. Matrícula ${response.matricula}.`);
        this.decided.emit();
        this.close.emit();
      });
  }

  protected reject(): void {
    const id = this.applicationId();
    if (!id) return;

    const reason = this.rejectReason().trim();
    if (!reason) {
      this.toast.error('Escribe un motivo para rechazar.');
      return;
    }

    this.deciding.set(true);
    this.api
      .post(`/admissions/applications/${id}/reject`, { reason })
      .pipe(
        catchError(() => {
          this.toast.error('No se pudo rechazar la solicitud.');
          return of(null);
        }),
        finalize(() => this.deciding.set(false)),
      )
      .subscribe((result) => {
        if (result === null) return;
        this.toast.success('Solicitud rechazada.');
        this.decided.emit();
        this.close.emit();
      });
  }
}
