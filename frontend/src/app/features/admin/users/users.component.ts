import { Component, computed, inject, signal } from '@angular/core';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { of } from 'rxjs';
import { catchError, map, switchMap } from 'rxjs/operators';
import { ApiClient } from '../../../core/http/api-client';
import { AuthStore } from '../../../core/auth/auth.store';
import { PagedResultDto, ResetPasswordResponseDto, UserListItemDto } from '../../../api/schema';
import { CardComponent } from '../../../shared/ui/card/card.component';
import { BadgeComponent } from '../../../shared/ui/badge/badge.component';
import { ButtonComponent } from '../../../shared/ui/button/button.component';
import { ToastService } from '../../../shared/ui/toast/toast.service';
import { ROLE_LABELS, UserRole } from '../../../domain/models';
import { AddUserDrawerComponent } from './add-user-drawer.component';
import { EditUserDrawerComponent } from './edit-user-drawer.component';

// Student queda fuera a propósito (pedido explícito del cliente, 2026-09: "bloqueame el acceso de
// los alumnos como usuarios del sistema y que no aparezcan en la tabla"). Alcance confirmado con el
// cliente: esto es solo ocultarlos de ESTE panel — su acceso real a Mis materias/Mi Kardex/Mi perfil
// no se toca aquí. Los alumnos se gestionan desde "Alumnos", que tiene su propio flujo completo.
const ROLE_FILTER_OPTIONS: readonly UserRole[] = ['Administrator', 'Teacher', 'RegionalCoordinator', 'RegionalSecretary'];

/**
 * Panel de Usuarios (RN-12: alta/gestión de cuentas es exclusiva de Administrator — ruta protegida
 * con roleGuard(['Administrator'])). Reutiliza GetUsersQuery/CreateUserCommand/ResetPasswordCommand/
 * AdminUpdateUserCommand/DeleteUserCommand/SetUserStatusCommand.
 *
 * Alumnos se filtra en el cliente (línea `role !== 'Student'` en filteredUsers) en vez de tocar
 * GetUsersQuery en el backend, porque esa query la comparte la pantalla de Alumnos (filtrada por
 * Role=Student) — cambiarla ahí rompería esa otra pantalla.
 *
 * Regla de negocio explícita del cliente: el usuario en sesión no puede editarse/restablecerse su
 * propia contraseña ni inhabilitarse/eliminarse a sí mismo desde aquí — el backend ya lo rechaza en
 * cada handler, y aquí además se deshabilitan los botones en su propia fila para que la restricción
 * sea obvia antes de intentarlo, no solo un error después de hacer clic.
 */
@Component({
  selector: 'shk-users',
  imports: [CardComponent, BadgeComponent, ButtonComponent, AddUserDrawerComponent, EditUserDrawerComponent],
  template: `
    <div class="flex items-center justify-between">
      <h1 class="text-2xl font-bold text-slate-900">Usuarios</h1>
      <shk-button variant="primary" (click)="drawerOpen.set(true)">Agregar usuario</shk-button>
    </div>

    <p class="mt-2 text-sm text-slate-500">
      Alta y administración de cuentas de personal (administradores, docentes, coordinación regional). Para alumnos usa "Alumnos".
    </p>

    <shk-add-user-drawer
      [open]="drawerOpen()"
      (close)="drawerOpen.set(false)"
      (added)="refreshTick.update(n => n + 1)"
    />

    <shk-edit-user-drawer
      [open]="editingUser() !== null"
      [user]="editingUser()"
      (close)="editingUser.set(null)"
      (updated)="refreshTick.update(n => n + 1)"
    />

    <div class="mt-4 flex flex-wrap gap-3">
      <input
        type="search"
        placeholder="Buscar por nombre o matrícula…"
        class="shk-field w-64"
        (input)="searchText.set($any($event.target).value)"
      />
      <select class="shk-field" (change)="roleFilter.set($any($event.target).value || null)">
        <option value="">Todos los roles</option>
        @for (role of roleFilterOptions; track role) {
          <option [value]="role">{{ roleLabel(role) }}</option>
        }
      </select>
    </div>

    <shk-card class="mt-6 overflow-x-auto">
      <table class="w-full text-left text-sm">
        <thead class="text-slate-500">
          <tr>
            <th class="py-2">Matrícula</th>
            <th class="py-2">Nombre</th>
            <th class="py-2">Correo</th>
            <th class="py-2">Rol</th>
            <th class="py-2">Región</th>
            <th class="py-2">Estatus</th>
            <th class="py-2"></th>
          </tr>
        </thead>
        <tbody>
          @for (user of filteredUsers(); track user.id) {
            <tr class="border-t border-slate-100">
              <td class="py-2 font-medium">{{ user.enrollmentNumber }}</td>
              <td class="py-2">
                {{ user.fullName }}
                @if (isSelf(user)) {
                  <shk-badge tone="neutral" class="ml-1.5">Tú</shk-badge>
                }
              </td>
              <td class="py-2 text-slate-500">{{ user.email }}</td>
              <td class="py-2">{{ roleLabel(user.role) }}</td>
              <td class="py-2">{{ user.regionName ?? '—' }}</td>
              <td class="py-2"><shk-badge [tone]="user.status === 'Active' ? 'success' : 'neutral'">{{ user.status }}</shk-badge></td>
              <td class="py-2">
                <div class="flex flex-wrap items-center justify-end gap-1.5">
                  <button
                    type="button"
                    class="rounded-lg border border-slate-200 px-3 py-1.5 text-xs font-semibold text-slate-600 transition hover:bg-slate-50 disabled:cursor-not-allowed disabled:opacity-40"
                    [disabled]="isSelf(user)"
                    [title]="isSelf(user) ? 'No puedes editarte a ti mismo aquí.' : 'Editar usuario'"
                    (click)="editingUser.set(user)"
                  >
                    Editar
                  </button>
                  <button
                    type="button"
                    class="rounded-lg border border-slate-200 px-3 py-1.5 text-xs font-semibold text-slate-600 transition hover:bg-slate-50 disabled:cursor-not-allowed disabled:opacity-40"
                    [disabled]="isSelf(user) || resettingId() === user.id"
                    [title]="isSelf(user) ? 'No puedes restablecer tu propia contraseña aquí — usa Cambiar contraseña.' : 'Restablecer contraseña'"
                    (click)="resetPassword(user)"
                  >
                    {{ resettingId() === user.id ? 'Restableciendo…' : 'Restablecer contraseña' }}
                  </button>
                  <button
                    type="button"
                    class="rounded-lg border border-slate-200 px-3 py-1.5 text-xs font-semibold text-slate-600 transition hover:bg-slate-50 disabled:cursor-not-allowed disabled:opacity-40"
                    [disabled]="isSelf(user) || statusUpdatingId() === user.id"
                    [title]="isSelf(user) ? 'No puedes inhabilitarte a ti mismo.' : (user.status === 'Active' ? 'Inhabilitar' : 'Activar')"
                    (click)="toggleStatus(user)"
                  >
                    {{ statusUpdatingId() === user.id ? 'Guardando…' : (user.status === 'Active' ? 'Inhabilitar' : 'Activar') }}
                  </button>
                  <button
                    type="button"
                    class="rounded-lg border border-red-200 px-3 py-1.5 text-xs font-semibold text-red-600 transition hover:bg-red-50 disabled:cursor-not-allowed disabled:opacity-40"
                    [disabled]="isSelf(user) || deletingId() === user.id"
                    [title]="isSelf(user) ? 'No puedes eliminar tu propio usuario.' : 'Eliminar usuario'"
                    (click)="deleteUser(user)"
                  >
                    {{ deletingId() === user.id ? 'Eliminando…' : 'Eliminar' }}
                  </button>
                </div>
              </td>
            </tr>
          } @empty {
            <tr><td colspan="7" class="py-6 text-center text-slate-400">Sin usuarios que coincidan con el filtro.</td></tr>
          }
        </tbody>
      </table>
    </shk-card>

    @if (lastResetPassword(); as reset) {
      <div class="fixed inset-0 z-50 flex items-center justify-center bg-slate-900/40 px-4" (click)="lastResetPassword.set(null)">
        <div class="w-full max-w-sm rounded-2xl bg-white p-6 shadow-2xl" (click)="$event.stopPropagation()">
          <h2 class="text-lg font-bold text-slate-900">Contraseña restablecida</h2>
          <p class="mt-1 text-sm text-slate-500">Compártela por un medio seguro — deberá cambiarla al iniciar sesión. También se le notificó por correo.</p>
          <div class="mt-4 flex items-center gap-2">
            <code class="flex-1 rounded-lg bg-slate-50 px-3 py-2 font-mono text-sm text-slate-800">{{ reset.temporaryPassword }}</code>
            <button
              type="button"
              class="rounded-lg border border-slate-200 px-3 py-2 text-xs font-semibold text-slate-600 transition hover:bg-slate-50"
              (click)="copyPassword(reset.temporaryPassword)"
            >
              {{ copied() ? 'Copiado' : 'Copiar' }}
            </button>
          </div>
          <shk-button variant="primary" class="mt-5 block w-full [&>button]:w-full" (click)="lastResetPassword.set(null)">Listo</shk-button>
        </div>
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
export class UsersComponent {
  private readonly api = inject(ApiClient);
  private readonly auth = inject(AuthStore);
  private readonly toast = inject(ToastService);

  protected readonly roleFilterOptions = ROLE_FILTER_OPTIONS;

  protected readonly refreshTick = signal(0);
  protected readonly drawerOpen = signal(false);
  protected readonly editingUser = signal<UserListItemDto | null>(null);
  protected readonly searchText = signal('');
  protected readonly roleFilter = signal<UserRole | null>(null);
  protected readonly resettingId = signal<string | null>(null);
  protected readonly statusUpdatingId = signal<string | null>(null);
  protected readonly deletingId = signal<string | null>(null);
  protected readonly lastResetPassword = signal<ResetPasswordResponseDto | null>(null);
  protected readonly copied = signal(false);

  private readonly users = toSignal(
    toObservable(this.refreshTick).pipe(
      switchMap(() =>
        this.api
          .get<PagedResultDto<UserListItemDto>>('/users', { pageSize: 500 })
          .pipe(catchError(() => of<PagedResultDto<UserListItemDto>>({ items: [], page: 1, pageSize: 500, totalItems: 0, totalPages: 0 }))),
      ),
    ),
    { initialValue: null },
  );

  protected readonly filteredUsers = computed(() => {
    const items = this.users()?.items ?? [];
    const search = this.searchText().trim().toLowerCase();
    const role = this.roleFilter();

    return items.filter((user) => {
      if (user.role === 'Student') return false;
      const matchesSearch =
        !search || user.fullName.toLowerCase().includes(search) || String(user.enrollmentNumber).includes(search) || (user.matricula ?? '').toLowerCase().includes(search);
      const matchesRole = !role || user.role === role;
      return matchesSearch && matchesRole;
    });
  });

  protected roleLabel(role: UserRole): string {
    return ROLE_LABELS[role];
  }

  protected isSelf(user: UserListItemDto): boolean {
    return user.id === this.auth.user()?.id;
  }

  protected resetPassword(user: UserListItemDto): void {
    if (this.isSelf(user) || this.resettingId() !== null) return;

    if (!confirm(`¿Restablecer la contraseña de ${user.fullName}? Se generará una nueva contraseña temporal y se le notificará por correo.`)) {
      return;
    }

    this.resettingId.set(user.id);
    this.api
      .post<ResetPasswordResponseDto>(`/users/${user.id}/reset-password`, {})
      .pipe(
        map((response) => ({ ok: true as const, response })),
        catchError((error) => of({ ok: false as const, error })),
      )
      .subscribe((result) => {
        this.resettingId.set(null);
        if (!result.ok) {
          this.toast.error(result.error?.error?.detail ?? 'No se pudo restablecer la contraseña.');
          return;
        }
        this.lastResetPassword.set(result.response);
      });
  }

  protected toggleStatus(user: UserListItemDto): void {
    if (this.isSelf(user) || this.statusUpdatingId() !== null) return;

    const activating = user.status !== 'Active';
    const verb = activating ? 'activar' : 'inhabilitar';
    if (!confirm(`¿Seguro que deseas ${verb} a ${user.fullName}?`)) return;

    this.statusUpdatingId.set(user.id);
    this.api
      .post<void>(`/users/${user.id}/status`, { active: activating })
      .pipe(
        map(() => ({ ok: true as const })),
        catchError((error) => of({ ok: false as const, error })),
      )
      .subscribe((result) => {
        this.statusUpdatingId.set(null);
        if (!result.ok) {
          this.toast.error(result.error?.error?.detail ?? 'No se pudo actualizar el estatus del usuario.');
          return;
        }
        this.toast.success(activating ? 'Usuario activado.' : 'Usuario inhabilitado.');
        this.refreshTick.update((n) => n + 1);
      });
  }

  protected deleteUser(user: UserListItemDto): void {
    if (this.isSelf(user) || this.deletingId() !== null) return;

    if (!confirm(`¿Eliminar permanentemente a ${user.fullName}? Esta acción no se puede deshacer.`)) return;

    this.deletingId.set(user.id);
    this.api
      .delete<void>(`/users/${user.id}`)
      .pipe(
        map(() => ({ ok: true as const })),
        catchError((error) => of({ ok: false as const, error })),
      )
      .subscribe((result) => {
        this.deletingId.set(null);
        if (!result.ok) {
          this.toast.error(result.error?.error?.detail ?? 'No se pudo eliminar al usuario.');
          return;
        }
        this.toast.success('Usuario eliminado.');
        this.refreshTick.update((n) => n + 1);
      });
  }

  protected copyPassword(password: string): void {
    navigator.clipboard
      ?.writeText(password)
      .then(() => {
        this.copied.set(true);
        setTimeout(() => this.copied.set(false), 2000);
      })
      .catch(() => this.toast.error('No se pudo copiar automáticamente — selecciónala manualmente.'));
  }
}
