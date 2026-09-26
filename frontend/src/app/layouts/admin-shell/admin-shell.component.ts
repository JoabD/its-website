import { Component, ElementRef, HostListener, inject, signal, viewChild } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { IonTabBar, IonTabButton } from '@ionic/angular';
import { AuthStore } from '../../core/auth/auth.store';
import { ROLE_LABELS } from '../../domain/models';

interface NavItem {
  readonly label: string;
  readonly path: string;
  readonly icon: string;
  readonly roles?: readonly string[];
}

interface NavGroup {
  readonly label: string | null;
  readonly items: readonly NavItem[];
}

/**
 * Menú agrupado en 3 categorías (pedido del cliente, 2026-09: "dale orden al menu, agrupa en 3
 * grandes categorias"). "Inicio" queda suelto arriba, sin encabezado, como es estándar en un
 * dashboard (patrón "Home" separado de las secciones). Criterio de agrupación:
 * - Administración: gestión institucional — avisos, inscripciones, staff, académico, calificaciones,
 *   pagos y el calendario institucional.
 * - Alumnos: todo lo referente al alumnado, tanto la vista administrativa (listado, Kardex de
 *   cualquier alumno) como la vista propia del alumno (Mis materias/Mi Kardex/Mi información).
 * - Configuración: catálogos de bajo cambio que alimentan al resto del sistema (regiones, checklist
 *   de documentos de inscripción).
 */
const NAV_GROUPS: readonly NavGroup[] = [
  {
    label: null,
    items: [{ label: 'Inicio', path: '/admin', icon: 'bi-house-door' }],
  },
  {
    label: 'Administración',
    items: [
      { label: 'Avisos', path: '/admin/avisos', icon: 'bi-megaphone' },
      { label: 'Inscripciones', path: '/admin/inscripciones', icon: 'bi-journal-check', roles: ['Administrator'] },
      { label: 'Docentes', path: '/admin/docentes', icon: 'bi-person-workspace', roles: ['Administrator'] },
      { label: 'Usuarios', path: '/admin/usuarios', icon: 'bi-people', roles: ['Administrator'] },
      { label: 'Académico', path: '/admin/academico', icon: 'bi-diagram-3', roles: ['Administrator'] },
      { label: 'Calificaciones', path: '/admin/calificaciones', icon: 'bi-clipboard-check', roles: ['Teacher', 'Administrator'] },
      { label: 'Pagos', path: '/admin/pagos', icon: 'bi-cash-coin', roles: ['Administrator', 'RegionalCoordinator', 'RegionalSecretary'] },
      { label: 'Calendario', path: '/admin/calendario', icon: 'bi-calendar-event', roles: ['Administrator', 'RegionalCoordinator', 'RegionalSecretary'] },
    ],
  },
  {
    label: 'Alumnos',
    items: [
      { label: 'Alumnos', path: '/admin/alumnos', icon: 'bi-mortarboard', roles: ['Administrator', 'RegionalCoordinator', 'RegionalSecretary'] },
      { label: 'Kardex', path: '/admin/kardex', icon: 'bi-file-earmark-text', roles: ['Administrator', 'RegionalCoordinator', 'RegionalSecretary'] },
      { label: 'Mis materias', path: '/admin/mis-materias', icon: 'bi-book', roles: ['Student'] },
      { label: 'Mi Kardex', path: '/admin/mi-kardex', icon: 'bi-file-earmark-text', roles: ['Student'] },
      { label: 'Mi información', path: '/admin/mi-perfil', icon: 'bi-person-circle', roles: ['Student'] },
    ],
  },
  {
    label: 'Configuración',
    items: [
      { label: 'Regiones', path: '/admin/configuracion/regiones', icon: 'bi-geo-alt', roles: ['Administrator'] },
      { label: 'Documentos de inscripción', path: '/admin/configuracion/checklist', icon: 'bi-ui-checks', roles: ['Administrator'] },
    ],
  },
];

/**
 * Rediseño del panel admin (feedback del usuario: "se ve muy simple y fea, debe ser algo más
 * elegante, digno de un dashboard"). Cambios de fondo, no solo estéticos:
 * - "Volver al sitio" explícito: antes no había ninguna forma de regresar al sitio público desde
 *   el panel salvo editando la URL a mano.
 * - Menú de usuario (avatar + nombre + rol) con "Cambiar contraseña" / "Ver sitio público" /
 *   "Cerrar sesión" — el patrón estándar de cualquier dashboard, en vez de un botón suelto.
 * - Sidebar oscura con iconos Bootstrap Icons (ya cargados globalmente en index.html) en vez de
 *   una lista de texto plano; colapsable en móvil.
 *
 * Usabilidad móvil (pedido del cliente, 2026-09: "agregar Ionic para darle más versatilidad móvil"
 * — alcance acordado: piezas puntuales, sin tocar el sistema de diseño propio): además del sidebar
 * de arriba (que se sigue usando tal cual en escritorio y como menú "Más" completo en móvil), se
 * agrega una barra de pestañas inferior (ion-tab-bar) SOLO visible por debajo de "lg" — el patrón
 * de navegación estándar en apps móviles, con los primeros accesos visibles del rol actual a un
 * toque del pulgar, en vez de tener que abrir el menú hamburguesa para todo. "Más" reabre el mismo
 * sidebar de siempre (sidebarOpen) para el resto de las opciones — no se duplica lógica de menú.
 */
@Component({
  selector: 'shk-admin-shell',
  imports: [RouterLink, RouterLinkActive, RouterOutlet, IonTabBar, IonTabButton],
  template: `
    <div class="flex min-h-screen bg-slate-50">
      <!-- Overlay móvil al abrir el sidebar -->
      @if (sidebarOpen()) {
        <div class="fixed inset-0 z-30 bg-slate-900/40 lg:hidden" (click)="sidebarOpen.set(false)"></div>
      }

      <aside
        class="fixed inset-y-0 left-0 z-40 flex w-64 shrink-0 flex-col bg-[linear-gradient(180deg,var(--shk-color-primary),var(--shk-color-primary-dark))]
               text-white transition-transform duration-200 lg:static lg:translate-x-0"
        [class.-translate-x-full]="!sidebarOpen()"
      >
        <div class="flex items-center gap-3 px-5 py-6">
          <img src="/img/shekina-logo.png" alt="Logo" class="h-9 w-9 rounded-full bg-white/10 object-contain p-1" />
          <div class="leading-tight">
            <div class="font-[var(--shk-font-heading)] text-sm font-bold">Instituto Shekinah</div>
            <div class="text-[11px] uppercase tracking-wide text-[var(--shk-color-accent-light)]">Panel de control escolar</div>
          </div>
        </div>

        <nav class="flex-1 space-y-4 overflow-y-auto px-3 pb-4">
          @for (group of visibleGroups(); track group.label ?? '$root') {
            <div>
              @if (group.label) {
                <div class="px-3 pb-1.5 pt-2 text-[11px] font-semibold uppercase tracking-wider text-white/40">{{ group.label }}</div>
              }
              <div class="space-y-1">
                @for (item of group.items; track item.path) {
                  <a
                    [routerLink]="item.path"
                    [routerLinkActiveOptions]="{ exact: item.path === '/admin' }"
                    routerLinkActive="bg-white/10 text-white shadow-inner"
                    class="group flex items-center gap-3 rounded-lg px-3 py-2.5 text-sm font-medium text-white/70 transition hover:bg-white/5 hover:text-white"
                    (click)="sidebarOpen.set(false)"
                  >
                    <i class="bi {{ item.icon }} text-base text-[var(--shk-color-accent-light)] group-hover:text-[var(--shk-color-accent)]"></i>
                    {{ item.label }}
                  </a>
                }
              </div>
            </div>
          }
        </nav>

        <div class="border-t border-white/10 px-3 py-4">
          <a
            routerLink="/"
            class="flex items-center gap-3 rounded-lg px-3 py-2.5 text-sm font-medium text-white/70 transition hover:bg-white/5 hover:text-white"
          >
            <i class="bi bi-box-arrow-left text-base text-[var(--shk-color-accent-light)]"></i>
            Volver al sitio público
          </a>
        </div>
      </aside>

      <div class="flex min-h-screen flex-1 flex-col lg:pl-0">
        <header class="sticky top-0 z-20 flex items-center justify-between gap-4 border-b border-slate-200 bg-white/90 px-4 py-3 backdrop-blur sm:px-6">
          <div class="flex items-center gap-3">
            <button
              type="button"
              class="grid h-9 w-9 place-items-center rounded-lg text-slate-500 hover:bg-slate-100 lg:hidden"
              (click)="sidebarOpen.set(true)"
              aria-label="Abrir menú"
            >
              <i class="bi bi-list text-xl"></i>
            </button>
            <div>
              <div class="font-[var(--shk-font-heading)] text-base font-bold text-slate-900">{{ pageTitle() }}</div>
              <div class="text-xs text-slate-400">Instituto Teológico Shekinah</div>
            </div>
          </div>

          <div class="flex items-center gap-2">
            <a
              routerLink="/"
              class="hidden items-center gap-2 rounded-full border border-slate-200 px-4 py-2 text-sm font-medium text-slate-600 transition hover:border-[var(--shk-color-accent)] hover:text-[var(--shk-color-primary)] sm:flex"
            >
              <i class="bi bi-globe2"></i> Ver sitio público
            </a>

            <div class="relative" #userMenuEl>
              <button
                type="button"
                class="flex items-center gap-2 rounded-full border border-slate-200 py-1.5 pl-1.5 pr-3 text-sm transition hover:border-[var(--shk-color-accent)]"
                (click)="userMenuOpen.set(!userMenuOpen())"
              >
                <span class="grid h-8 w-8 place-items-center rounded-full bg-[var(--shk-color-primary)] text-xs font-bold text-white">
                  {{ initials() }}
                </span>
                <span class="hidden text-left leading-tight sm:block">
                  <span class="block text-sm font-medium text-slate-800">{{ auth.user()?.fullName ?? 'Mi cuenta' }}</span>
                  <span class="block text-[11px] text-slate-400">{{ auth.role() ? roleLabel(auth.role()!) : '' }}</span>
                </span>
                <i class="bi bi-chevron-down text-xs text-slate-400"></i>
              </button>

              @if (userMenuOpen()) {
                <div class="absolute right-0 z-30 mt-2 w-56 overflow-hidden rounded-xl border border-slate-200 bg-white py-1.5 shadow-lg">
                  <div class="border-b border-slate-100 px-4 py-3 sm:hidden">
                    <div class="text-sm font-medium text-slate-800">{{ auth.user()?.fullName ?? 'Mi cuenta' }}</div>
                    <div class="text-xs text-slate-400">{{ auth.role() ? roleLabel(auth.role()!) : '' }}</div>
                  </div>
                  <a routerLink="/admin/cambiar-password" class="flex items-center gap-2.5 px-4 py-2.5 text-sm text-slate-700 hover:bg-slate-50" (click)="userMenuOpen.set(false)">
                    <i class="bi bi-key text-slate-400"></i> Cambiar contraseña
                  </a>
                  <a routerLink="/" class="flex items-center gap-2.5 px-4 py-2.5 text-sm text-slate-700 hover:bg-slate-50 sm:hidden" (click)="userMenuOpen.set(false)">
                    <i class="bi bi-globe2 text-slate-400"></i> Ver sitio público
                  </a>
                  <button
                    type="button"
                    class="flex w-full items-center gap-2.5 border-t border-slate-100 px-4 py-2.5 text-left text-sm text-red-600 hover:bg-red-50"
                    (click)="logout()"
                  >
                    <i class="bi bi-box-arrow-right"></i> Cerrar sesión
                  </button>
                </div>
              }
            </div>
          </div>
        </header>

        <main class="flex-1 p-4 pb-20 sm:p-6 lg:pb-6">
          <router-outlet />
        </main>
      </div>

      <!-- Barra de pestañas inferior — solo móvil/tablet angosto (lg:hidden), a un toque del
           pulgar. Los primeros accesos visibles del rol actual + "Más" para el resto (reabre el
           sidebar de siempre). safe-area-inset ya lo maneja ion-tab-bar por su cuenta (notch/home
           indicator de iOS). -->
      <ion-tab-bar class="fixed inset-x-0 bottom-0 z-30 lg:hidden" style="--background: var(--shk-color-primary-dark); --border: none">
        @for (item of mobileTabs(); track item.path) {
          <ion-tab-button
            [routerLink]="item.path"
            [routerLinkActiveOptions]="{ exact: item.path === '/admin' }"
            routerLinkActive="text-[var(--shk-color-accent)]"
            class="text-white/60"
            style="--color: inherit; --color-selected: var(--shk-color-accent)"
          >
            <i class="bi {{ item.icon }} text-lg"></i>
            <span class="mt-0.5 text-[10px]">{{ item.label }}</span>
          </ion-tab-button>
        }
        <ion-tab-button (click)="sidebarOpen.set(true)" class="text-white/60" style="--color: inherit">
          <i class="bi bi-grid-3x3-gap text-lg"></i>
          <span class="mt-0.5 text-[10px]">Más</span>
        </ion-tab-button>
      </ion-tab-bar>
    </div>
  `,
})
export class AdminShellComponent {
  protected readonly auth = inject(AuthStore);
  private readonly userMenuEl = viewChild<ElementRef<HTMLElement>>('userMenuEl');

  protected readonly sidebarOpen = signal(false);
  protected readonly userMenuOpen = signal(false);

  protected visibleGroups(): readonly NavGroup[] {
    const role = this.auth.role();
    return NAV_GROUPS.map((group) => ({
      label: group.label,
      items: group.items.filter((item) => !item.roles || (role !== null && item.roles.includes(role))),
    })).filter((group) => group.items.length > 0);
  }

  /** Los primeros 4 accesos visibles del rol actual (siempre incluye "Inicio", por ser el primero
   * en NAV_GROUPS) — el resto vive detrás de "Más". Misma fuente que el sidebar (visibleGroups()),
   * así que un rol nuevo o un cambio de menú no requiere tocar esto aparte. */
  protected mobileTabs(): readonly NavItem[] {
    return this.visibleGroups()
      .flatMap((group) => group.items)
      .slice(0, 4);
  }

  protected pageTitle(): string {
    const role = this.auth.role();
    if (role === 'Student') return 'Mi espacio';
    return 'Panel administrativo';
  }

  protected roleLabel(role: string): string {
    return ROLE_LABELS[role as keyof typeof ROLE_LABELS] ?? role;
  }

  protected initials(): string {
    const name = this.auth.user()?.fullName?.trim();
    if (!name) return '?';
    const parts = name.split(/\s+/).filter(Boolean);
    return ((parts[0]?.[0] ?? '') + (parts[1]?.[0] ?? '')).toUpperCase() || name[0]!.toUpperCase();
  }

  protected logout(): void {
    this.userMenuOpen.set(false);
    this.auth.logout();
  }

  /** Cierra el menú de usuario al hacer clic fuera de él (patrón estándar de dropdown). */
  @HostListener('document:click', ['$event'])
  protected onDocumentClick(event: MouseEvent): void {
    if (!this.userMenuOpen()) return;
    const container = this.userMenuEl()?.nativeElement;
    if (container && !container.contains(event.target as Node)) {
      this.userMenuOpen.set(false);
    }
  }
}
