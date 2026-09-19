import { Component, computed, inject, signal } from '@angular/core';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { of } from 'rxjs';
import { catchError, switchMap } from 'rxjs/operators';
import { ApiClient } from '../../../core/http/api-client';
import { PagedResultDto, UserListItemDto } from '../../../api/schema';
import { CardComponent } from '../../../shared/ui/card/card.component';
import { BadgeComponent } from '../../../shared/ui/badge/badge.component';
import { ButtonComponent } from '../../../shared/ui/button/button.component';
import { MODALITY_LABELS, STUDY_PLAN_LABELS } from '../../../domain/models';
import { AddStudentDrawerComponent } from './add-student-drawer.component';

/**
 * Plan de control escolar, fase 5: "Alumnos" separado de la antigua página genérica "Usuarios"
 * (que mezclaba todos los roles). Reutiliza GetUsersQuery filtrado por Role=Student — el backend
 * ya acota automáticamente por región cuando quien consulta es RegionalCoordinator/
 * RegionalSecretary (RN-09, ver GetUsersQueryHandler), así que aquí no hace falta repetir ese
 * chequeo: si esta pantalla devuelve datos, ya vienen correctamente acotados.
 *
 * Nota: la llamada anterior (en el viejo "Usuarios") tipaba la respuesta como arreglo plano
 * (`UserListItemDto[]`) cuando el backend siempre devolvió `PagedResult<UserListItem>` — un bug
 * que hacía que la tabla nunca pintara filas. Se corrige aquí leyendo `.items`.
 */
@Component({
  selector: 'shk-students',
  imports: [CardComponent, BadgeComponent, ButtonComponent, AddStudentDrawerComponent],
  template: `
    <div class="flex items-center justify-between">
      <h1 class="text-2xl font-bold text-slate-900">Alumnos</h1>
      <shk-button variant="primary" (click)="drawerOpen.set(true)">Agregar alumno</shk-button>
    </div>

    <shk-add-student-drawer
      [open]="drawerOpen()"
      (close)="drawerOpen.set(false)"
      (added)="refreshTick.set(refreshTick() + 1)"
    />

    <div class="mt-4 flex flex-wrap gap-3">
      <input
        type="search"
        placeholder="Buscar por nombre o matrícula…"
        class="shk-field w-64"
        (input)="searchText.set($any($event.target).value)"
      />
      <select class="shk-field" (change)="modalityFilter.set($any($event.target).value || null)">
        <option value="">Todas las modalidades</option>
        <option value="Onsite">Presencial</option>
        <option value="Online">Virtual</option>
        <option value="Diploma">Diplomado</option>
      </select>
      <select class="shk-field" (change)="statusFilter.set($any($event.target).value || null)">
        <option value="">Todos los estatus</option>
        <option value="Active">Activo</option>
        <option value="Blocked">Bloqueado</option>
        <option value="Inactive">Inactivo</option>
      </select>
    </div>

    <shk-card class="mt-6 overflow-x-auto">
      <table class="w-full text-left text-sm">
        <thead class="text-slate-500">
          <tr>
            <th class="py-2">No. control</th>
            <th class="py-2">Matrícula</th>
            <th class="py-2">Nombre</th>
            <th class="py-2">Correo</th>
            <th class="py-2">Región</th>
            <th class="py-2">Modalidad</th>
            <th class="py-2">Plan</th>
            <th class="py-2">Cuatrimestre</th>
            <th class="py-2">Estatus</th>
          </tr>
        </thead>
        <tbody>
          @for (student of filteredStudents(); track student.id) {
            <tr class="border-t border-slate-100">
              <td class="py-2 text-slate-500">{{ student.enrollmentNumber }}</td>
              <td class="py-2 font-medium">{{ student.matricula ?? '—' }}</td>
              <td class="py-2">{{ student.fullName }}</td>
              <td class="py-2 text-slate-500">{{ student.email }}</td>
              <td class="py-2">{{ student.regionName ?? '—' }}</td>
              <td class="py-2">{{ student.modality ? modalityLabel(student.modality) : '—' }}</td>
              <td class="py-2">{{ student.plan ? planLabel(student.plan) : '—' }}</td>
              <td class="py-2">{{ student.currentTerm ?? '—' }}</td>
              <td class="py-2"><shk-badge [tone]="student.status === 'Active' ? 'success' : 'neutral'">{{ statusLabel(student.status) }}</shk-badge></td>
            </tr>
          } @empty {
            <tr><td colspan="9" class="py-6 text-center text-slate-400">Sin alumnos que coincidan con el filtro.</td></tr>
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
export class StudentsComponent {
  private readonly api = inject(ApiClient);
  protected readonly refreshTick = signal(0);
  protected readonly drawerOpen = signal(false);

  protected readonly searchText = signal('');
  protected readonly modalityFilter = signal<string | null>(null);
  protected readonly statusFilter = signal<string | null>(null);

  private readonly students = toSignal(
    toObservable(this.refreshTick).pipe(
      switchMap(() =>
        this.api
          .get<PagedResultDto<UserListItemDto>>('/users', { role: 'Student', pageSize: 500 })
          .pipe(catchError(() => of<PagedResultDto<UserListItemDto>>({ items: [], page: 1, pageSize: 500, totalItems: 0, totalPages: 0 }))),
      ),
    ),
    { initialValue: null },
  );

  protected readonly filteredStudents = computed(() => {
    const items = this.students()?.items ?? [];
    const search = this.searchText().trim().toLowerCase();
    const modality = this.modalityFilter();
    const status = this.statusFilter();

    return items.filter((student) => {
      const matchesSearch =
        !search || student.fullName.toLowerCase().includes(search) || String(student.enrollmentNumber).includes(search);
      const matchesModality = !modality || student.modality === modality;
      const matchesStatus = !status || student.status === status;
      return matchesSearch && matchesModality && matchesStatus;
    });
  });

  protected modalityLabel(modality: string): string {
    return MODALITY_LABELS[modality as keyof typeof MODALITY_LABELS] ?? modality;
  }

  protected planLabel(plan: string): string {
    return STUDY_PLAN_LABELS[plan as keyof typeof STUDY_PLAN_LABELS] ?? plan;
  }

  protected statusLabel(status: string): string {
    return { Active: 'Activo', Blocked: 'Bloqueado', Inactive: 'Inactivo' }[status] ?? status;
  }
}
