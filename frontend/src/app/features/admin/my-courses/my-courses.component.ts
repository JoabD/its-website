import { Component, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { of } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { ApiClient } from '../../../core/http/api-client';
import { StudentCourseRowDto } from '../../../api/schema';
import { CardComponent } from '../../../shared/ui/card/card.component';
import { BadgeComponent } from '../../../shared/ui/badge/badge.component';

/** Vista del alumno sobre sus materias del periodo activo; grade null se muestra como "Sin calificar" (nunca 0). */
@Component({
  selector: 'shk-my-courses',
  imports: [CardComponent, BadgeComponent],
  template: `
    <h1 class="text-2xl font-bold text-slate-900">Mis materias</h1>

    <shk-card class="mt-6">
      <table class="w-full text-left text-sm">
        <thead class="text-slate-500">
          <tr><th class="py-2">Materia</th><th class="py-2">Profesor</th><th class="py-2">Región</th><th class="py-2">Periodo</th><th class="py-2">Calificación</th></tr>
        </thead>
        <tbody>
          @for (course of courses(); track course.offeringId) {
            <tr class="border-t border-slate-100">
              <td class="py-2 font-medium">{{ course.subjectName }}</td>
              <td class="py-2">{{ course.teacherName }}</td>
              <td class="py-2">{{ course.regionName }}</td>
              <td class="py-2">{{ course.periodCode }}</td>
              <td class="py-2">
                @if (course.grade !== null) {
                  <shk-badge [tone]="course.grade >= 6 ? 'success' : 'danger'">{{ course.grade }}</shk-badge>
                } @else {
                  <shk-badge tone="neutral">Sin calificar</shk-badge>
                }
              </td>
            </tr>
          } @empty {
            <tr><td colspan="5" class="py-6 text-center text-slate-400">Sin materias inscritas en este periodo.</td></tr>
          }
        </tbody>
      </table>
    </shk-card>
  `,
})
export class MyCoursesComponent {
  private readonly api = inject(ApiClient);

  protected readonly courses = toSignal(
    this.api.get<StudentCourseRowDto[]>('/students/me/courses').pipe(catchError(() => of<StudentCourseRowDto[]>([]))),
    { initialValue: [] as StudentCourseRowDto[] },
  );
}
