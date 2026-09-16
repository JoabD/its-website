import { Component, inject, signal } from '@angular/core';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { of } from 'rxjs';
import { catchError, switchMap, tap } from 'rxjs/operators';
import { ApiClient } from '../../../core/http/api-client';
import { EnrollmentRowDto, OfferingListItemDto } from '../../../api/schema';
import { CardComponent } from '../../../shared/ui/card/card.component';
import { ButtonComponent } from '../../../shared/ui/button/button.component';
import { ToastService } from '../../../shared/ui/toast/toast.service';

/** RN-16: captura de calificaciones (0-10), validación de rango la hace siempre el dominio (Grade VO). */
@Component({
  selector: 'shk-grades',
  imports: [FormsModule, CardComponent, ButtonComponent],
  template: `
    <h1 class="text-2xl font-bold text-slate-900">Calificaciones</h1>

    <shk-card class="mt-6">
      <label class="flex flex-col gap-1 text-sm md:w-96">
        <span class="font-medium text-slate-700">Grupo</span>
        <select class="shk-field" [ngModel]="selectedOfferingId()" (ngModelChange)="selectOffering($event)">
          <option value="" disabled>Selecciona un grupo</option>
          @for (offering of offerings(); track offering.id) {
            <option [value]="offering.id">{{ offering.subjectName }} — {{ offering.regionName }}</option>
          }
        </select>
      </label>
    </shk-card>

    @if (selectedOfferingId()) {
      <shk-card class="mt-6">
        <table class="w-full text-left text-sm">
          <thead class="text-slate-500"><tr><th class="py-2">Matrícula</th><th class="py-2">Alumno</th><th class="py-2">Calificación</th><th class="py-2"></th></tr></thead>
          <tbody>
            @for (row of enrollments(); track row.studentId) {
              <tr class="border-t border-slate-100">
                <td class="py-2 font-medium">{{ row.enrollmentNumber }}</td>
                <td class="py-2">{{ row.studentName }}</td>
                <td class="py-2">
                  <input type="number" min="0" max="10" step="0.1" class="shk-field w-24" [(ngModel)]="draftGrades()[row.studentId]" />
                </td>
                <td class="py-2">
                  <shk-button variant="secondary" (click)="saveGrade(row.studentId)">Guardar</shk-button>
                </td>
              </tr>
            } @empty {
              <tr><td colspan="4" class="py-6 text-center text-slate-400">Sin alumnos inscritos.</td></tr>
            }
          </tbody>
        </table>
      </shk-card>
    }
  `,
  styles: [
    `
    .shk-field {
      border-radius: var(--shk-radius);
      border: 1px solid #cbd5e1;
      padding: 0.5rem 0.75rem;
      font-size: 0.875rem;
      line-height: 1.25rem;
    }
    `,
  ],
})
export class GradesComponent {
  private readonly api = inject(ApiClient);
  private readonly toast = inject(ToastService);

  protected readonly selectedOfferingId = signal('');
  private readonly refreshTick = signal(0);
  protected readonly draftGrades = signal<Record<string, number | null>>({});

  protected readonly offerings = toSignal(
    this.api.get<OfferingListItemDto[]>('/academic/offerings').pipe(catchError(() => of<OfferingListItemDto[]>([]))),
    { initialValue: [] as OfferingListItemDto[] },
  );

  protected readonly enrollments = toSignal(
    toObservable(this.refreshTick).pipe(
      switchMap(() => {
        const offeringId = this.selectedOfferingId();
        if (!offeringId) return of<EnrollmentRowDto[]>([]);
        return this.api.get<EnrollmentRowDto[]>(`/academic/offerings/${offeringId}/enrollments`).pipe(
          tap((rows) => {
            const drafts: Record<string, number | null> = {};
            for (const row of rows) drafts[row.studentId] = row.grade;
            this.draftGrades.set(drafts);
          }),
          catchError(() => of<EnrollmentRowDto[]>([])),
        );
      }),
    ),
    { initialValue: [] as EnrollmentRowDto[] },
  );

  protected selectOffering(offeringId: string): void {
    this.selectedOfferingId.set(offeringId);
    this.refreshTick.update((n) => n + 1);
  }

  protected saveGrade(studentId: string): void {
    const offeringId = this.selectedOfferingId();
    const grade = this.draftGrades()[studentId];
    if (grade === null || grade === undefined) return;

    this.api.put(`/academic/offerings/${offeringId}/enrollments/${studentId}/grade`, { grade }).pipe(
      tap({
        next: () => this.toast.success('Calificación guardada.'),
        error: () => this.toast.error('No se pudo guardar la calificación.'),
      }),
      catchError(() => of(null)),
    ).subscribe();
  }
}
