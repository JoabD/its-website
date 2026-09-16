import { Component, inject, signal } from '@angular/core';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { of } from 'rxjs';
import { catchError, switchMap, tap } from 'rxjs/operators';
import { ApiClient } from '../../../core/http/api-client';
import { ApplicationListItemDto } from '../../../api/schema';
import { CardComponent } from '../../../shared/ui/card/card.component';
import { ButtonComponent } from '../../../shared/ui/button/button.component';
import { BadgeComponent, BadgeTone } from '../../../shared/ui/badge/badge.component';
import { ToastService } from '../../../shared/ui/toast/toast.service';
import { APPLICATION_STATUS_LABELS, ApplicationStatus, MODALITY_LABELS, Modality } from '../../../domain/models';

/**
 * RN-01: bandeja de admisiones. Aprobar/rechazar es un comando que el dominio valida siempre en
 * servidor (transición de estado inmutable una vez decidida) — esta pantalla solo dispara el
 * comando y refresca la lista, nunca decide localmente si la transición es válida.
 */
@Component({
  selector: 'shk-admissions',
  imports: [CardComponent, ButtonComponent, BadgeComponent],
  template: `
    <h1 class="text-2xl font-bold text-slate-900">Bandeja de inscripciones</h1>

    <shk-card class="mt-6">
      <table class="w-full text-left text-sm">
        <thead class="text-slate-500">
          <tr>
            <th class="py-2">Folio</th>
            <th class="py-2">Solicitante</th>
            <th class="py-2">Modalidad</th>
            <th class="py-2">Región</th>
            <th class="py-2">Estatus</th>
            <th class="py-2"></th>
          </tr>
        </thead>
        <tbody>
          @for (application of applications(); track application.id) {
            <tr class="border-t border-slate-100">
              <td class="py-2 font-medium">{{ application.folio }}</td>
              <td class="py-2">{{ application.applicantName }}</td>
              <td class="py-2">{{ modalityLabel(application.modality) }}</td>
              <td class="py-2">{{ application.regionName }}</td>
              <td class="py-2"><shk-badge [tone]="statusTone(application.status)">{{ statusLabel(application.status) }}</shk-badge></td>
              <td class="py-2">
                @if (application.status === 'Pending') {
                  <div class="flex gap-2">
                    <shk-button variant="secondary" [loading]="busyId() === application.id" (click)="decide(application.id, true)">Aprobar</shk-button>
                    <shk-button variant="danger" [loading]="busyId() === application.id" (click)="decide(application.id, false)">Rechazar</shk-button>
                  </div>
                }
              </td>
            </tr>
          } @empty {
            <tr><td colspan="6" class="py-6 text-center text-slate-400">Sin solicitudes registradas.</td></tr>
          }
        </tbody>
      </table>
    </shk-card>
  `,
})
export class AdmissionsComponent {
  private readonly api = inject(ApiClient);
  private readonly toast = inject(ToastService);

  private readonly refreshTick = signal(0);
  protected readonly busyId = signal<string | null>(null);

  protected readonly applications = toSignal(
    toObservable(this.refreshTick).pipe(
      switchMap(() =>
        this.api.get<ApplicationListItemDto[]>('/admissions/applications').pipe(catchError(() => of<ApplicationListItemDto[]>([]))),
      ),
    ),
    { initialValue: [] as ApplicationListItemDto[] },
  );

  protected modalityLabel(modality: Modality): string {
    return MODALITY_LABELS[modality];
  }

  protected statusLabel(status: ApplicationStatus): string {
    return APPLICATION_STATUS_LABELS[status];
  }

  protected statusTone(status: ApplicationStatus): BadgeTone {
    return status === 'Approved' ? 'success' : status === 'Rejected' ? 'danger' : 'warning';
  }

  protected decide(applicationId: string, approve: boolean): void {
    this.busyId.set(applicationId);
    const path = `/admissions/applications/${applicationId}/${approve ? 'approve' : 'reject'}`;

    this.api.post(path, {}).pipe(
      tap({
        next: () => {
          this.toast.success(approve ? 'Solicitud aprobada.' : 'Solicitud rechazada.');
          this.refreshTick.update((n) => n + 1);
        },
        error: () => this.toast.error('No se pudo procesar la decisión.'),
      }),
      catchError(() => of(null)),
    ).subscribe(() => this.busyId.set(null));
  }
}
