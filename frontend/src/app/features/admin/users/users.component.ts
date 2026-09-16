import { Component, inject, signal } from '@angular/core';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { of } from 'rxjs';
import { catchError, switchMap } from 'rxjs/operators';
import { ApiClient } from '../../../core/http/api-client';
import { UserListItemDto } from '../../../api/schema';
import { CardComponent } from '../../../shared/ui/card/card.component';
import { BadgeComponent } from '../../../shared/ui/badge/badge.component';
import { ROLE_LABELS, UserRole } from '../../../domain/models';

/**
 * RN-08/RN-23: alta y consulta de usuarios; la asignación de rol/alcance regional la revalida
 * siempre el backend (AuthorizationBehavior) — esta tabla es de solo consulta por ahora.
 */
@Component({
  selector: 'shk-users',
  imports: [CardComponent, BadgeComponent],
  template: `
    <h1 class="text-2xl font-bold text-slate-900">Usuarios</h1>

    <shk-card class="mt-6">
      <table class="w-full text-left text-sm">
        <thead class="text-slate-500">
          <tr>
            <th class="py-2">Matrícula</th>
            <th class="py-2">Nombre</th>
            <th class="py-2">Correo</th>
            <th class="py-2">Rol</th>
            <th class="py-2">Región</th>
            <th class="py-2">Estatus</th>
          </tr>
        </thead>
        <tbody>
          @for (user of users(); track user.id) {
            <tr class="border-t border-slate-100">
              <td class="py-2 font-medium">{{ user.enrollmentNumber }}</td>
              <td class="py-2">{{ user.fullName }}</td>
              <td class="py-2 text-slate-500">{{ user.email }}</td>
              <td class="py-2">{{ roleLabel(user.role) }}</td>
              <td class="py-2">{{ user.regionName ?? '—' }}</td>
              <td class="py-2"><shk-badge [tone]="user.status === 'Active' ? 'success' : 'neutral'">{{ user.status }}</shk-badge></td>
            </tr>
          } @empty {
            <tr><td colspan="6" class="py-6 text-center text-slate-400">Sin usuarios.</td></tr>
          }
        </tbody>
      </table>
    </shk-card>
  `,
})
export class UsersComponent {
  private readonly api = inject(ApiClient);
  private readonly refreshTick = signal(0);

  protected readonly users = toSignal(
    toObservable(this.refreshTick).pipe(
      switchMap(() => this.api.get<UserListItemDto[]>('/users').pipe(catchError(() => of<UserListItemDto[]>([])))),
    ),
    { initialValue: [] as UserListItemDto[] },
  );

  protected roleLabel(role: UserRole): string {
    return ROLE_LABELS[role];
  }
}
