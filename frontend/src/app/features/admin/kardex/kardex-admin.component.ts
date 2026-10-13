import { Component, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute } from '@angular/router';
import { of } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { ApiClient } from '../../../core/http/api-client';
import { PagedResultDto, UserListItemDto } from '../../../api/schema';
import { AuthStore } from '../../../core/auth/auth.store';
import { CardComponent } from '../../../shared/ui/card/card.component';
import { KardexViewComponent } from './kardex-view.component';

/**
 * Plan de control escolar, fase 8: Kardex desde el panel admin. Administrator/RegionalCoordinator/
 * RegionalSecretary buscan un alumno (mismo endpoint /users?role=Student que ya usa "Alumnos" —
 * el backend acota por región para Coordinador/Secretario, RN-09) y ven su Kardex embebido.
 *
 * Rediseño del flujo de asignación (2026-10): esta pantalla ES el "detalle del alumno" donde se
 * asignan/corrigen calificaciones por cuatrimestre/semestre que pidió el cliente — antes solo
 * tenía su propio buscador, sin relación con "Alumnos"; ahora también se puede llegar aquí
 * directamente desde el botón "Calificaciones" de cada fila en Alumnos (?studentId=...), y para
 * Administrator el Kardex se abre en modo editable (ver KardexViewComponent.canEdit).
 */
@Component({
  selector: 'shk-kardex-admin',
  imports: [CardComponent, KardexViewComponent],
  template: `
    <h1 class="text-2xl font-bold text-slate-900">Kardex</h1>
    <p class="mt-1 text-sm text-slate-500">
      Busca un alumno para consultar, descargar o enviar su Kardex{{ isAdmin() ? ', o capturar/corregir sus calificaciones por cuatrimestre/semestre' : '' }}.
    </p>

    <shk-card class="mt-6">
      <input
        type="text"
        class="shk-field w-full"
        placeholder="Buscar alumno por nombre…"
        [value]="searchText()"
        (input)="onSearchInput($any($event.target).value)"
      />

      @if (searchText().length > 1) {
        <div class="mt-3 divide-y divide-slate-100">
          @for (student of students(); track student.id) {
            <button
              type="button"
              class="flex w-full items-center justify-between py-2 text-left hover:bg-slate-50"
              (click)="selectedStudentId.set(student.id)"
            >
              <span class="font-medium text-slate-700">{{ student.fullName }}</span>
              <span class="text-xs text-slate-400">{{ student.email ?? 'Sin correo' }} · Mat. {{ student.enrollmentNumber }}</span>
            </button>
          } @empty {
            <p class="py-4 text-center text-sm text-slate-400">Sin resultados.</p>
          }
        </div>
      }
    </shk-card>

    @if (selectedStudentId(); as studentId) {
      <div class="mt-6">
        <shk-kardex-view [studentId]="studentId" [editable]="isAdmin()" />
      </div>
    }
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
export class KardexAdminComponent {
  private readonly api = inject(ApiClient);
  private readonly auth = inject(AuthStore);
  private readonly route = inject(ActivatedRoute);

  protected readonly searchText = signal('');
  protected readonly selectedStudentId = signal<string | null>(null);
  protected readonly isAdmin = () => this.auth.role() === 'Administrator';

  private static readonly EMPTY_RESULT: PagedResultDto<UserListItemDto> = { items: [], page: 1, pageSize: 500, totalItems: 0, totalPages: 0 };

  private readonly allStudents = toSignal(
    this.api.get<PagedResultDto<UserListItemDto>>('/users', { role: 'Student', pageSize: 500 }).pipe(
      catchError(() => of(KardexAdminComponent.EMPTY_RESULT)),
    ),
    { initialValue: KardexAdminComponent.EMPTY_RESULT },
  );

  constructor() {
    // Llegada directa desde "Alumnos" → botón "Calificaciones" de una fila (?studentId=...): abre
    // el Kardex de ese alumno de inmediato, sin obligar a volver a buscarlo por nombre.
    this.route.queryParamMap.subscribe((params) => {
      const studentId = params.get('studentId');
      if (studentId) this.selectedStudentId.set(studentId);
    });
  }

  protected onSearchInput(value: string): void {
    this.searchText.set(value);
  }

  protected students(): UserListItemDto[] {
    const text = this.searchText().trim().toLowerCase();
    const items = this.allStudents().items;
    return text.length > 1 ? items.filter((s) => s.fullName.toLowerCase().includes(text)) : [];
  }
}
