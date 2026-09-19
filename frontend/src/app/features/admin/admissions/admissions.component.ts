import { Component, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { combineLatest, of } from 'rxjs';
import { catchError, debounceTime, distinctUntilChanged, finalize, switchMap } from 'rxjs/operators';
import { ApiClient } from '../../../core/http/api-client';
import { ApplicationListItemDto, PagedResultDto } from '../../../api/schema';
import { CardComponent } from '../../../shared/ui/card/card.component';
import { BadgeComponent, BadgeTone } from '../../../shared/ui/badge/badge.component';
import { ToastService } from '../../../shared/ui/toast/toast.service';
import { ApplicationReviewDrawerComponent } from './review-drawer.component';
import { APPLICATION_STATUS_LABELS, ApplicationStatus, MODALITY_LABELS, Modality } from '../../../domain/models';

type StatusTab = ApplicationStatus | 'All';

const STATUS_TABS: { value: StatusTab; label: string }[] = [
  { value: 'Pending', label: 'Pendientes' },
  { value: 'Approved', label: 'Aprobadas' },
  { value: 'Rejected', label: 'Rechazadas' },
  { value: 'All', label: 'Todas' },
];

/**
 * Bandeja de admisiones (estilo Gmail): fila con acciones que aparecen al pasar el mouse en vez de
 * botones siempre visibles — "Revisar" abre el drawer con la ficha completa + checklist; "Aprobar"
 * es la vía rápida que se infiere de tener ya toda la documentación (RN de producto: NO valida el
 * checklist); "Eliminar" solo existe mientras la solicitud sigue pendiente (una vez decidida es un
 * registro histórico, no se puede borrar — ver AdmissionApplication.IsDeletable en el dominio).
 *
 * Las acciones también quedan visibles a opacidad completa en pantallas táctiles (sin hover), y
 * con foco de teclado, para no esconder funcionalidad a quien no usa mouse.
 */
@Component({
  selector: 'shk-admissions',
  imports: [CardComponent, BadgeComponent, ApplicationReviewDrawerComponent, DatePipe],
  template: `
    <div class="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
      <h1 class="text-2xl font-bold text-slate-900">Bandeja de inscripciones</h1>
      <input
        type="search"
        placeholder="Buscar por folio o nombre…"
        class="shk-field w-full sm:w-72"
        [value]="searchText()"
        (input)="onSearch($any($event.target).value)"
      />
    </div>

    <div class="mt-4 flex flex-wrap gap-2">
      @for (tab of statusTabs; track tab.value) {
        <button
          type="button"
          class="rounded-full px-4 py-1.5 text-sm font-medium transition"
          [class]="tab.value === statusFilter() ? 'bg-[var(--shk-color-primary)] text-white' : 'bg-white text-slate-600 border border-slate-200 hover:border-slate-300'"
          (click)="onStatusChange(tab.value)"
        >
          {{ tab.label }}
        </button>
      }
    </div>

    <shk-card class="mt-5 overflow-x-auto p-0">
      <table class="w-full text-left text-sm">
        <thead class="text-slate-500">
          <tr class="border-b border-slate-100">
            <th class="px-6 py-3 font-medium">Folio</th>
            <th class="px-6 py-3 font-medium">Solicitante</th>
            <th class="hidden px-6 py-3 font-medium md:table-cell">Modalidad</th>
            <th class="hidden px-6 py-3 font-medium md:table-cell">Región</th>
            <th class="px-6 py-3 font-medium">Estatus</th>
            <th class="hidden px-6 py-3 font-medium sm:table-cell">Recibida</th>
            <th class="px-6 py-3"></th>
          </tr>
        </thead>
        <tbody>
          @for (application of result()?.items ?? []; track application.id) {
            <tr class="group border-b border-slate-50 last:border-0 hover:bg-slate-50/80">
              <td class="px-6 py-3 font-medium text-slate-800">{{ application.folio }}</td>
              <td class="px-6 py-3 text-slate-700">{{ application.applicantName }}</td>
              <td class="hidden px-6 py-3 text-slate-600 md:table-cell">{{ modalityLabel(application.modality) }}</td>
              <td class="hidden px-6 py-3 text-slate-600 md:table-cell">{{ application.regionName }}</td>
              <td class="px-6 py-3">
                <shk-badge [tone]="statusTone(application.status)">{{ statusLabel(application.status) }}</shk-badge>
                @if (daysUntilPurge(application.purgeScheduledAtUtc); as days) {
                  <div class="mt-1 text-[11px] text-slate-400">Se elimina en {{ days }} {{ days === 1 ? 'día' : 'días' }}</div>
                }
              </td>
              <td class="hidden px-6 py-3 text-slate-500 sm:table-cell">{{ application.submittedAtUtc | date: 'dd/MM/yyyy' }}</td>
              <td class="px-6 py-3">
                <div class="flex items-center justify-end gap-1 opacity-0 transition group-hover:opacity-100 group-focus-within:opacity-100 max-sm:opacity-100">
                  <button
                    type="button"
                    class="rounded-lg p-2 text-slate-500 transition hover:bg-white hover:text-[var(--shk-color-primary)] hover:shadow-sm"
                    title="Revisar"
                    aria-label="Revisar solicitud"
                    (click)="reviewingId.set(application.id)"
                  >
                    <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" class="h-4.5 w-4.5">
                      <path stroke-linecap="round" stroke-linejoin="round" d="M2.25 12s3.75-7.5 9.75-7.5 9.75 7.5 9.75 7.5-3.75 7.5-9.75 7.5S2.25 12 2.25 12Z" />
                      <path stroke-linecap="round" stroke-linejoin="round" d="M12 15a3 3 0 1 0 0-6 3 3 0 0 0 0 6Z" />
                    </svg>
                  </button>
                  @if (application.status === 'Pending') {
                    <button
                      type="button"
                      class="rounded-lg p-2 text-slate-500 transition hover:bg-white hover:text-emerald-600 hover:shadow-sm disabled:opacity-40"
                      title="Aprobar (sin revisar checklist)"
                      aria-label="Aprobar solicitud"
                      [disabled]="busyId() === application.id"
                      (click)="quickApprove(application)"
                    >
                      <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" class="h-4.5 w-4.5">
                        <path stroke-linecap="round" stroke-linejoin="round" d="m4.5 12.75 6 6 9-13.5" />
                      </svg>
                    </button>
                    <button
                      type="button"
                      class="rounded-lg p-2 text-slate-500 transition hover:bg-white hover:text-red-600 hover:shadow-sm disabled:opacity-40"
                      title="Eliminar"
                      aria-label="Eliminar solicitud"
                      [disabled]="busyId() === application.id"
                      (click)="remove(application)"
                    >
                      <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" class="h-4.5 w-4.5">
                        <path stroke-linecap="round" stroke-linejoin="round" d="m14.74 9-.346 9m-4.788 0L9.26 9m9.968-3.21c.342.052.682.107 1.022.166m-1.022-.165L18.16 19.673a2.25 2.25 0 0 1-2.244 2.077H8.084a2.25 2.25 0 0 1-2.244-2.077L4.772 5.79m14.456 0a48.108 48.108 0 0 0-3.478-.397M4.772 5.79c.34-.059.68-.114 1.022-.165m0 0a48.11 48.11 0 0 1 3.478-.397m7.5 0v-.916c0-1.18-.91-2.164-2.09-2.201a51.964 51.964 0 0 0-3.32 0c-1.18.037-2.09 1.022-2.09 2.201v.916m7.5 0a48.667 48.667 0 0 0-7.5 0" />
                      </svg>
                    </button>
                  }
                </div>
              </td>
            </tr>
          } @empty {
            <tr><td colspan="7" class="px-6 py-10 text-center text-slate-400">Sin solicitudes que coincidan con el filtro.</td></tr>
          }
        </tbody>
      </table>

      @if ((result()?.totalPages ?? 0) > 1) {
        <div class="flex flex-col gap-2 border-t border-slate-100 px-6 py-3 text-sm text-slate-500 sm:flex-row sm:items-center sm:justify-between">
          <span>Página {{ page() }} de {{ result()?.totalPages }} · {{ result()?.totalItems }} solicitudes</span>
          <div class="flex gap-2">
            <button type="button" class="shk-page-btn" [disabled]="page() <= 1" (click)="page.set(page() - 1)">Anterior</button>
            <button type="button" class="shk-page-btn" [disabled]="page() >= (result()?.totalPages ?? 1)" (click)="page.set(page() + 1)">Siguiente</button>
          </div>
        </div>
      }
    </shk-card>

    <shk-application-review-drawer
      [applicationId]="reviewingId()"
      (close)="reviewingId.set(null)"
      (decided)="refreshTick.update(n => n + 1)"
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
    .shk-page-btn {
      border-radius: 9999px;
      border: 1px solid #e2e8f0;
      padding: 0.25rem 0.9rem;
      transition: all 0.2s ease;
    }
    .shk-page-btn:hover:not(:disabled) { border-color: var(--shk-color-accent); color: var(--shk-color-primary); }
    .shk-page-btn:disabled { opacity: 0.4; cursor: not-allowed; }
    `,
  ],
})
export class AdmissionsComponent {
  private readonly api = inject(ApiClient);
  private readonly toast = inject(ToastService);

  protected readonly statusTabs = STATUS_TABS;

  protected readonly page = signal(1);
  protected readonly statusFilter = signal<StatusTab>('Pending');
  protected readonly searchText = signal('');
  protected readonly refreshTick = signal(0);
  protected readonly busyId = signal<string | null>(null);
  protected readonly reviewingId = signal<string | null>(null);

  private readonly pageSize = 20;

  private readonly search$ = toObservable(this.searchText).pipe(debounceTime(350), distinctUntilChanged());

  protected readonly result = toSignal(
    combineLatest([toObservable(this.page), toObservable(this.statusFilter), this.search$, toObservable(this.refreshTick)]).pipe(
      switchMap(([page, status, search]) =>
        this.api
          .get<PagedResultDto<ApplicationListItemDto>>('/admissions/applications', {
            status: status === 'All' ? null : status,
            search: search || null,
            page,
            pageSize: this.pageSize,
          })
          .pipe(
            catchError(() =>
              of<PagedResultDto<ApplicationListItemDto>>({ items: [], page: 1, pageSize: this.pageSize, totalItems: 0, totalPages: 0 }),
            ),
          ),
      ),
    ),
    { initialValue: null },
  );

  protected onSearch(value: string): void {
    this.searchText.set(value);
    this.page.set(1);
  }

  protected onStatusChange(status: StatusTab): void {
    this.statusFilter.set(status);
    this.page.set(1);
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

  /** Purga automática a 30 días de decidida (spec confirmada, sin excepción manual) — solo
   * informativo, nunca una acción. Null mientras la solicitud sigue Pending. */
  protected daysUntilPurge(purgeScheduledAtUtc: string | null): number | null {
    if (!purgeScheduledAtUtc) return null;
    const diffMs = new Date(purgeScheduledAtUtc).getTime() - Date.now();
    return Math.max(0, Math.ceil(diffMs / (1000 * 60 * 60 * 24)));
  }

  protected quickApprove(application: ApplicationListItemDto): void {
    const confirmed = window.confirm(
      `¿Confirmas que ya tienes toda la documentación de ${application.applicantName}? Esta acción no revisa el checklist.`,
    );
    if (!confirmed) return;

    this.busyId.set(application.id);
    this.api
      .post(`/admissions/applications/${application.id}/approve`, { viaQuickAction: true })
      .pipe(
        catchError(() => {
          this.toast.error('No se pudo aprobar la solicitud.');
          return of(null);
        }),
        finalize(() => this.busyId.set(null)),
      )
      .subscribe((result) => {
        if (result === null) return;
        this.toast.success('Solicitud aprobada.');
        this.refreshTick.update((n) => n + 1);
      });
  }

  protected remove(application: ApplicationListItemDto): void {
    const confirmed = window.confirm(`¿Eliminar la solicitud de ${application.applicantName}? Esta acción no se puede deshacer.`);
    if (!confirmed) return;

    this.busyId.set(application.id);
    this.api
      .delete(`/admissions/applications/${application.id}`)
      .pipe(
        catchError(() => {
          this.toast.error('No se pudo eliminar la solicitud.');
          return of(null);
        }),
        finalize(() => this.busyId.set(null)),
      )
      .subscribe((result) => {
        if (result === null) return;
        this.toast.success('Solicitud eliminada.');
        this.refreshTick.update((n) => n + 1);
      });
  }
}
