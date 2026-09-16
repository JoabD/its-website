import { Component, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { of } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { ApiClient } from '../../../core/http/api-client';
import { OfferingListItemDto, PeriodListItemDto } from '../../../api/schema';
import { CardComponent } from '../../../shared/ui/card/card.component';
import { BadgeComponent } from '../../../shared/ui/badge/badge.component';

/**
 * RN-13: solo un periodo académico "Active" a la vez (garantizado por índice único parcial en
 * Mongo, ver M001_CreateIndexesAndValidators). RN-15: auto-inscripción al abrir grupos, ejecutada
 * por el backend — esta pantalla es de consulta de periodos y ofertas de grupo.
 */
@Component({
  selector: 'shk-academic',
  imports: [CardComponent, BadgeComponent],
  template: `
    <h1 class="text-2xl font-bold text-slate-900">Gestión académica</h1>

    <div class="mt-6 grid gap-6 lg:grid-cols-2">
      <shk-card>
        <h2 class="font-semibold text-[var(--shk-color-primary)]">Periodos</h2>
        <table class="mt-3 w-full text-left text-sm">
          <thead class="text-slate-500"><tr><th class="py-2">Código</th><th class="py-2">Nombre</th><th class="py-2">Estatus</th></tr></thead>
          <tbody>
            @for (period of periods(); track period.id) {
              <tr class="border-t border-slate-100">
                <td class="py-2 font-medium">{{ period.code }}</td>
                <td class="py-2">{{ period.name }}</td>
                <td class="py-2"><shk-badge [tone]="period.status === 'Active' ? 'success' : 'neutral'">{{ period.status }}</shk-badge></td>
              </tr>
            } @empty {
              <tr><td colspan="3" class="py-6 text-center text-slate-400">Sin periodos.</td></tr>
            }
          </tbody>
        </table>
      </shk-card>

      <shk-card>
        <h2 class="font-semibold text-[var(--shk-color-primary)]">Grupos / Ofertas</h2>
        <table class="mt-3 w-full text-left text-sm">
          <thead class="text-slate-500"><tr><th class="py-2">Materia</th><th class="py-2">Región</th><th class="py-2">Profesor</th><th class="py-2">Alumnos</th></tr></thead>
          <tbody>
            @for (offering of offerings(); track offering.id) {
              <tr class="border-t border-slate-100">
                <td class="py-2 font-medium">{{ offering.subjectName }}</td>
                <td class="py-2">{{ offering.regionName }}</td>
                <td class="py-2">{{ offering.teacherName }}</td>
                <td class="py-2">{{ offering.enrollmentCount }}</td>
              </tr>
            } @empty {
              <tr><td colspan="4" class="py-6 text-center text-slate-400">Sin grupos.</td></tr>
            }
          </tbody>
        </table>
      </shk-card>
    </div>
  `,
})
export class AcademicComponent {
  private readonly api = inject(ApiClient);

  protected readonly periods = toSignal(
    this.api.get<PeriodListItemDto[]>('/academic/periods').pipe(catchError(() => of<PeriodListItemDto[]>([]))),
    { initialValue: [] as PeriodListItemDto[] },
  );

  protected readonly offerings = toSignal(
    this.api.get<OfferingListItemDto[]>('/academic/offerings').pipe(catchError(() => of<OfferingListItemDto[]>([]))),
    { initialValue: [] as OfferingListItemDto[] },
  );
}
