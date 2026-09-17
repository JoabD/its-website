import { Component, inject } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthStore } from '../../core/auth/auth.store';
import { ButtonComponent } from '../../shared/ui/button/button.component';
import { ROLE_LABELS } from '../../domain/models';

interface NavItem {
  readonly label: string;
  readonly path: string;
  readonly roles?: readonly string[];
}

const NAV_ITEMS: readonly NavItem[] = [
  { label: 'Inscripciones', path: '/admin/inscripciones', roles: ['Administrator'] },
  { label: 'Usuarios', path: '/admin/usuarios', roles: ['Administrator'] },
  { label: 'Académico', path: '/admin/academico', roles: ['Administrator'] },
  { label: 'Calificaciones', path: '/admin/calificaciones', roles: ['Teacher', 'Administrator'] },
  { label: 'Mis materias', path: '/admin/mis-materias', roles: ['Student'] },
  { label: 'Mi información', path: '/admin/mi-perfil', roles: ['Student'] },
  { label: 'Pagos', path: '/admin/pagos', roles: ['Administrator', 'RegionalCoordinator', 'RegionalSecretary'] },
];

@Component({
  selector: 'shk-admin-shell',
  imports: [RouterLink, RouterLinkActive, RouterOutlet, ButtonComponent],
  template: `
    <div class="flex min-h-screen bg-slate-50">
      <aside class="w-64 shrink-0 border-r border-slate-200 bg-white p-4">
        <div class="mb-6 text-lg font-bold text-[var(--shk-color-primary)]">Panel ITS</div>
        <nav class="flex flex-col gap-1 text-sm">
          @for (item of visibleItems(); track item.path) {
            <a
              [routerLink]="item.path"
              routerLinkActive="bg-slate-100 font-semibold text-[var(--shk-color-primary)]"
              class="rounded-[var(--shk-radius)] px-3 py-2 text-slate-700 hover:bg-slate-100"
            >
              {{ item.label }}
            </a>
          }
        </nav>
      </aside>

      <div class="flex flex-1 flex-col">
        <header class="flex items-center justify-between border-b border-slate-200 bg-white px-6 py-3">
          <div class="text-sm text-slate-500">
            @if (auth.role(); as role) {
              {{ roleLabel(role) }}
            }
          </div>
          <shk-button variant="ghost" (click)="auth.logout()">Cerrar sesión</shk-button>
        </header>

        <main class="flex-1 p-6">
          <router-outlet />
        </main>
      </div>
    </div>
  `,
})
export class AdminShellComponent {
  protected readonly auth = inject(AuthStore);

  protected visibleItems(): readonly NavItem[] {
    const role = this.auth.role();
    return NAV_ITEMS.filter((item) => !item.roles || (role !== null && item.roles.includes(role)));
  }

  protected roleLabel(role: string): string {
    return ROLE_LABELS[role as keyof typeof ROLE_LABELS] ?? role;
  }
}
