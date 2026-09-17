import { Component, computed, inject, signal } from '@angular/core';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { of } from 'rxjs';
import { catchError, switchMap } from 'rxjs/operators';
import { ApiClient } from '../../../core/http/api-client';
import { PagedResultDto, UserListItemDto } from '../../../api/schema';
import { CardComponent } from '../../../shared/ui/card/card.component';
import { BadgeComponent } from '../../../shared/ui/badge/badge.component';

/**
 * Plan de control escolar, fase 5: "Docentes" separado de la antigua página genérica "Usuarios".
 * Reutiliza GetUsersQuery filtrado por Role=Teacher. A diferencia de Alumnos, esta pantalla queda
 * restringida a Administrator (roleGuard de la ruta): en el modelo de dominio actual un Teacher no
 * tiene región asignada (User.Region es null para ese rol — solo Student/RegionalCoordinator/
 * RegionalSecretary la requieren), así que el alcance regional de un coordinador no tiene nada que
 * filtrar aquí; abrir esta vista a coordinadores no les mostraría nada útil.
 *
 * Pendiente natural de una siguiente vuelta (no incluido aquí): columna de "materias asignadas"
 * por docente — requeriría una consulta agregada nueva contra CourseOffering/AssignTeacher, que no
 * existe todavía como read model listo para usarse.
 */
@Component({
  selector: 'shk-teachers',
  imports: [CardComponent, BadgeComponent],
  template: `
    <div class="flex items-center justify-between">
      <h1 class="text-2xl font-bold text-slate-900">Docentes</h1>
    </div>

    <div class="mt-4">
      <input
        type="search"
        placeholder="Buscar por nombre o matrícula…"
        class="shk-field w-64"
        (input)="searchText.set($any($event.target).value)"
      />
    </div>

    <shk-card class="mt-6 overflow-x-auto">
      <table class="w-full text-left text-sm">
        <thead class="text-slate-500">
          <tr>
            <th class="py-2">Matrícula</th>
            <th class="py-2">Nombre</th>
            <th class="py-2">Correo</th>
            <th class="py-2">Estatus</th>
          </tr>
        </thead>
        <tbody>
          @for (teacher of filteredTeachers(); track teacher.id) {
            <tr class="border-t border-slate-100">
              <td class="py-2 font-medium">{{ teacher.enrollmentNumber }}</td>
              <td class="py-2">{{ teacher.fullName }}</td>
              <td class="py-2 text-slate-500">{{ teacher.email }}</td>
              <td class="py-2"><shk-badge [tone]="teacher.status === 'Active' ? 'success' : 'neutral'">{{ statusLabel(teacher.status) }}</shk-badge></td>
            </tr>
          } @empty {
            <tr><td colspan="4" class="py-6 text-center text-slate-400">Sin docentes que coincidan con el filtro.</td></tr>
          }
        </tbody>
      </table>
    </shk-card>
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
export class TeachersComponent {
  private readonly api = inject(ApiClient);
  private readonly refreshTick = signal(0);

  protected readonly searchText = signal('');

  private readonly teachers = toSignal(
    toObservable(this.refreshTick).pipe(
      switchMap(() =>
        this.api
          .get<PagedResultDto<UserListItemDto>>('/users', { role: 'Teacher', pageSize: 500 })
          .pipe(catchError(() => of<PagedResultDto<UserListItemDto>>({ items: [], page: 1, pageSize: 500, totalItems: 0, totalPages: 0 }))),
      ),
    ),
    { initialValue: null },
  );

  protected readonly filteredTeachers = computed(() => {
    const items = this.teachers()?.items ?? [];
    const search = this.searchText().trim().toLowerCase();
    if (!search) return items;
    return items.filter((teacher) => teacher.fullName.toLowerCase().includes(search) || String(teacher.enrollmentNumber).includes(search));
  });

  protected statusLabel(status: string): string {
    return { Active: 'Activo', Blocked: 'Bloqueado', Inactive: 'Inactivo' }[status] ?? status;
  }
}
