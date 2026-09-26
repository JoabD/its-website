import { Component, computed, inject, signal } from '@angular/core';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { of } from 'rxjs';
import { catchError, map, switchMap } from 'rxjs/operators';
import { ApiClient } from '../../../core/http/api-client';
import { PagedResultDto, UserListItemDto } from '../../../api/schema';
import { CardComponent } from '../../../shared/ui/card/card.component';
import { BadgeComponent } from '../../../shared/ui/badge/badge.component';
import { ButtonComponent } from '../../../shared/ui/button/button.component';
import { ToastService } from '../../../shared/ui/toast/toast.service';
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
 *
 * "Dar de baja"/"Reactivar" y "Eliminar" (pedido explícito del cliente, 2026-09): mismo patrón de
 * UI/UX que el panel de Usuarios (confirm() + toast + botón deshabilitado mientras se guarda), pero
 * "Eliminar" además solo se habilita cuando el alumno ya está Inactivo — el backend
 * (DeleteStudentCommandHandler) lo exige igual, este candado en el botón es solo para que la
 * restricción sea obvia antes de intentarlo, no solo un error después de hacer clic.
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
            <th class="py-2"></th>
          </tr>
        </thead>
        <tbody>
          @for (student of filteredStudents(); track student.id) {
            <tr class="border-t border-slate-100">
              <td class="py-2 text-slate-500">{{ student.enrollmentNumber }}</td>
              <td class="py-2 font-medium">{{ student.matricula ?? '—' }}</td>
              <td class="py-2">{{ student.fullName }}</td>
              <td class="py-2 text-slate-500">{{ student.email ?? 'Sin correo' }}</td>
              <td class="py-2">{{ student.regionName ?? '—' }}</td>
              <td class="py-2">{{ student.modality ? modalityLabel(student.modality) : '—' }}</td>
              <td class="py-2">{{ student.plan ? planLabel(student.plan) : '—' }}</td>
              <td class="py-2">{{ student.currentTerm ?? '—' }}</td>
              <td class="py-2"><shk-badge [tone]="student.status === 'Active' ? 'success' : 'neutral'">{{ statusLabel(student.status) }}</shk-badge></td>
              <td class="py-2">
                <div class="flex flex-wrap items-center justify-end gap-1.5">
                  <button
                    type="button"
                    class="rounded-lg border border-slate-200 px-3 py-1.5 text-xs font-semibold text-slate-600 transition hover:bg-slate-50 disabled:cursor-not-allowed disabled:opacity-40"
                    [disabled]="statusUpdatingId() === student.id"
                    [title]="student.status === 'Inactive' ? 'Reactivar alumno' : 'Dar de baja'"
                    (click)="toggleStatus(student)"
                  >
                    {{ statusUpdatingId() === student.id ? 'Guardando…' : (student.status === 'Inactive' ? 'Reactivar' : 'Dar de baja') }}
                  </button>
                  <button
                    type="button"
                    class="rounded-lg border border-red-200 px-3 py-1.5 text-xs font-semibold text-red-600 transition hover:bg-red-50 disabled:cursor-not-allowed disabled:opacity-40"
                    [disabled]="student.status !== 'Inactive' || deletingId() === student.id"
                    [title]="student.status !== 'Inactive' ? 'Primero da de baja al alumno para poder eliminarlo.' : 'Eliminar alumno'"
                    (click)="deleteStudent(student)"
                  >
                    {{ deletingId() === student.id ? 'Eliminando…' : 'Eliminar' }}
                  </button>
                </div>
              </td>
            </tr>
          } @empty {
            <tr><td colspan="10" class="py-6 text-center text-slate-400">Sin alumnos que coincidan con el filtro.</td></tr>
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
  private readonly toast = inject(ToastService);
  protected readonly refreshTick = signal(0);
  protected readonly drawerOpen = signal(false);

  protected readonly searchText = signal('');
  protected readonly modalityFilter = signal<string | null>(null);
  protected readonly statusFilter = signal<string | null>(null);
  protected readonly statusUpdatingId = signal<string | null>(null);
  protected readonly deletingId = signal<string | null>(null);

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

  protected toggleStatus(student: UserListItemDto): void {
    if (this.statusUpdatingId() !== null) return;

    const activating = student.status === 'Inactive';
    const verb = activating ? 'reactivar' : 'dar de baja a';
    if (!confirm(`¿Seguro que deseas ${verb} ${student.fullName}?`)) return;

    this.statusUpdatingId.set(student.id);
    this.api
      .post<void>(`/students/${student.id}/status`, { active: activating })
      .pipe(
        map(() => ({ ok: true as const })),
        catchError((error) => of({ ok: false as const, error })),
      )
      .subscribe((result) => {
        this.statusUpdatingId.set(null);
        if (!result.ok) {
          this.toast.error(result.error?.error?.detail ?? 'No se pudo actualizar el estatus del alumno.');
          return;
        }
        this.toast.success(activating ? 'Alumno reactivado.' : 'Alumno dado de baja.');
        this.refreshTick.update((n) => n + 1);
      });
  }

  protected deleteStudent(student: UserListItemDto): void {
    if (student.status !== 'Inactive' || this.deletingId() !== null) return;

    if (!confirm(`¿Eliminar permanentemente a ${student.fullName}? Esta acción no se puede deshacer.`)) return;

    this.deletingId.set(student.id);
    this.api
      .delete<void>(`/students/${student.id}`)
      .pipe(
        map(() => ({ ok: true as const })),
        catchError((error) => of({ ok: false as const, error })),
      )
      .subscribe((result) => {
        this.deletingId.set(null);
        if (!result.ok) {
          this.toast.error(result.error?.error?.detail ?? 'No se pudo eliminar al alumno.');
          return;
        }
        this.toast.success('Alumno eliminado.');
        this.refreshTick.update((n) => n + 1);
      });
  }
}
