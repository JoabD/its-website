import { Component, inject, signal } from '@angular/core';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { of } from 'rxjs';
import { catchError, finalize, switchMap } from 'rxjs/operators';
import { ApiClient } from '../../../core/http/api-client';
import { ModalityDto, RegionAdminListItemDto } from '../../../api/schema';
import { CardComponent } from '../../../shared/ui/card/card.component';
import { ButtonComponent } from '../../../shared/ui/button/button.component';
import { ToastService } from '../../../shared/ui/toast/toast.service';
import { MODALITY_LABELS } from '../../../domain/models';

interface RegionDraft {
  readonly id: string;
  readonly name: string;
  readonly code: number;
  abbreviation: string;
  modalityScope: Set<ModalityDto>;
}

const ALL_MODALITIES: ModalityDto[] = ['Onsite', 'Online', 'Diploma'];

/**
 * Configuración → Regiones (§4 de la spec): permite reasignar qué modalidades sirve cada región
 * (ej. una región que era solo Virtual pasa a ofrecer también Presencial) y ajustar su abreviatura
 * de matrícula. Confirmado sin candado adicional: el cambio aplica de inmediato a solicitudes
 * nuevas, sin bloquear por solicitudes Pending en curso.
 */
@Component({
  selector: 'shk-regions-settings',
  imports: [CardComponent, ButtonComponent],
  template: `
    <h1 class="text-2xl font-bold text-slate-900">Configuración · Regiones</h1>
    <p class="mt-1 text-sm text-slate-500">
      Ajusta qué modalidades ofrece cada región y su abreviatura para la matrícula (ITS/{{ '{' }}Abreviatura{{ '}' }}/consecutivo).
    </p>

    <div class="mt-6 grid gap-4">
      @for (region of drafts(); track region.id) {
        <shk-card>
          <div class="flex flex-col gap-4 sm:flex-row sm:items-start sm:justify-between">
            <div>
              <div class="font-[var(--shk-font-heading)] text-base font-semibold text-slate-900">{{ region.name }}</div>
              <div class="mt-1 text-xs text-slate-400">Código legado {{ region.code }}</div>
            </div>

            <div class="flex flex-col gap-1">
              <label class="text-xs font-medium text-slate-500" [for]="'abbr-' + region.id">Abreviatura</label>
              <input
                [id]="'abbr-' + region.id"
                type="text"
                maxlength="3"
                class="shk-field w-28 text-center uppercase tracking-widest"
                [value]="region.abbreviation"
                (input)="region.abbreviation = $any($event.target).value.toUpperCase()"
              />
            </div>
          </div>

          <div class="mt-4 flex flex-wrap gap-2">
            @for (modality of allModalities; track modality) {
              <button
                type="button"
                class="rounded-full border px-3.5 py-1.5 text-sm font-medium transition"
                [class]="region.modalityScope.has(modality)
                  ? 'border-transparent bg-[var(--shk-color-primary)] text-white'
                  : 'border-slate-200 bg-white text-slate-600 hover:border-slate-300'"
                (click)="toggleModality(region, modality)"
              >
                {{ modalityLabel(modality) }}
              </button>
            }
          </div>

          <div class="mt-4 flex justify-end">
            <shk-button variant="secondary" [loading]="savingId() === region.id" (click)="save(region)">Guardar cambios</shk-button>
          </div>
        </shk-card>
      } @empty {
        <p class="text-sm text-slate-400">No hay regiones configuradas todavía.</p>
      }
    </div>
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
export class RegionsSettingsComponent {
  private readonly api = inject(ApiClient);
  private readonly toast = inject(ToastService);
  private readonly refreshTick = signal(0);

  protected readonly allModalities = ALL_MODALITIES;
  protected readonly savingId = signal<string | null>(null);

  private readonly regions = toSignal(
    toObservable(this.refreshTick).pipe(
      switchMap(() =>
        this.api.get<RegionAdminListItemDto[]>('/catalog/regions/admin').pipe(catchError(() => of<RegionAdminListItemDto[]>([]))),
      ),
    ),
    { initialValue: [] as RegionAdminListItemDto[] },
  );

  protected readonly drafts = signal<RegionDraft[]>([]);

  constructor() {
    toObservable(this.regions).subscribe((regions) => {
      this.drafts.set(
        regions.map((r) => ({ id: r.id, name: r.name, code: r.code, abbreviation: r.abbreviation, modalityScope: new Set(r.modalityScope) })),
      );
    });
  }

  protected modalityLabel(modality: ModalityDto): string {
    return MODALITY_LABELS[modality];
  }

  protected toggleModality(region: RegionDraft, modality: ModalityDto): void {
    if (region.modalityScope.has(modality)) region.modalityScope.delete(modality);
    else region.modalityScope.add(modality);
    this.drafts.set([...this.drafts()]);
  }

  protected save(region: RegionDraft): void {
    if (region.modalityScope.size === 0) {
      this.toast.error('Una región debe servir al menos una modalidad.');
      return;
    }
    if (!region.abbreviation.trim()) {
      this.toast.error('La abreviatura es requerida.');
      return;
    }

    this.savingId.set(region.id);
    this.api
      .put(`/catalog/regions/${region.id}`, { modalityScope: [...region.modalityScope], abbreviation: region.abbreviation.trim() })
      .pipe(
        catchError(() => {
          this.toast.error('No se pudo guardar la región.');
          return of(null);
        }),
        finalize(() => this.savingId.set(null)),
      )
      .subscribe((result) => {
        if (result === null) return;
        this.toast.success('Región actualizada.');
        this.refreshTick.update((n) => n + 1);
      });
  }
}
