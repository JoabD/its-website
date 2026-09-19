import { Component, computed, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { AuthStore } from '../../../core/auth/auth.store';
import { ROLE_LABELS } from '../../../domain/models';

interface DashboardLink {
  readonly routerLink: string;
  readonly title: string;
  readonly description: string;
  readonly icon: string;
}

const ICONS = {
  inbox:
    'M2.25 8.25 5.4 3.9a1.5 1.5 0 0 1 1.212-.615h10.776a1.5 1.5 0 0 1 1.212.615l3.15 4.35m-18.5 0v9.6a1.5 1.5 0 0 0 1.5 1.5h15.5a1.5 1.5 0 0 0 1.5-1.5v-9.6m-18.5 0h5.03a.75.75 0 0 1 .713.513l.696 2.087a.75.75 0 0 0 .712.513h3.7a.75.75 0 0 0 .712-.513l.696-2.087a.75.75 0 0 1 .713-.513h5.03',
  users:
    'M15 19.128a9.38 9.38 0 0 0 2.625.372 9.337 9.337 0 0 0 4.121-.952 4.125 4.125 0 0 0-7.533-2.493M15 19.128v-.003c0-1.113-.285-2.16-.786-3.07M15 19.128v.106A12.318 12.318 0 0 1 8.624 21c-2.331 0-4.512-.645-6.374-1.766l-.001-.109a6.375 6.375 0 0 1 11.964-3.07M12 6.375a3.375 3.375 0 1 1-6.75 0 3.375 3.375 0 0 1 6.75 0Zm8.25 2.25a2.625 2.625 0 1 1-5.25 0 2.625 2.625 0 0 1 5.25 0Z',
  academic: 'M4.26 10.147a60.436 60.436 0 0 0-.491 6.347A48.62 48.62 0 0 1 12 20.904a48.62 48.62 0 0 1 8.232-4.41 60.46 60.46 0 0 0-.491-6.347m-15.482 0a50.636 50.636 0 0 0-2.658-.813A59.906 59.906 0 0 1 12 3.493a59.903 59.903 0 0 1 10.399 5.84c-.896.248-1.783.52-2.658.814m-15.482 0A50.717 50.717 0 0 1 12 13.489a50.702 50.702 0 0 1 7.74-3.342M6.75 15a.75.75 0 1 0 0-1.5.75.75 0 0 0 0 1.5Zm0 0v-3.675A55.378 55.378 0 0 1 12 8.443',
  grades:
    'M16.5 18.75h-9m9 0a3 3 0 0 1 3 3h-15a3 3 0 0 1 3-3m9 0v-3.375c0-.621-.503-1.125-1.125-1.125h-.871M7.5 18.75v-3.375c0-.621.504-1.125 1.125-1.125h.872m5.007 0H9.497m5.007 0a7.454 7.454 0 0 1-.982-3.172M9.497 14.25a7.454 7.454 0 0 0 .981-3.172M5.25 4.236c-.982.143-1.954.317-2.916.52A6.003 6.003 0 0 0 7.73 9.728M5.25 4.236V4.5c0 2.108.966 3.99 2.48 5.228M5.25 4.236V2.721C7.456 2.41 9.71 2.25 12 2.25c2.291 0 4.545.16 6.75.47v1.516M7.73 9.728a6.726 6.726 0 0 0 2.748 1.35m8.272-6.842V4.5c0 2.108-.966 3.99-2.48 5.228m2.48-5.492a46.32 46.32 0 0 1 2.916.52 6.003 6.003 0 0 1-5.395 4.972m0 0a6.726 6.726 0 0 1-2.749 1.35',
  book: 'M12 6.042A8.967 8.967 0 0 0 6 3.75c-1.052 0-2.062.18-3 .512v14.25A8.987 8.987 0 0 1 6 18c2.305 0 4.408.867 6 2.292m0-14.25a8.966 8.966 0 0 1 6-2.292c1.052 0 2.062.18 3 .512v14.25A8.987 8.987 0 0 0 18 18a8.967 8.967 0 0 0-6 2.292m0-14.25v14.25',
  kardex:
    'M9 12.75 11.25 15 15 9.75M21 12a9 9 0 1 1-18 0 9 9 0 0 1 18 0Z',
  profile: 'M17.982 18.725A7.488 7.488 0 0 0 12 15.75a7.488 7.488 0 0 0-5.982 2.975m11.963 0a9 9 0 1 0-11.963 0m11.963 0A8.966 8.966 0 0 1 12 21a8.966 8.966 0 0 1-5.982-2.275M15 9.75a3 3 0 1 1-6 0 3 3 0 0 1 6 0Z',
  payments:
    'M2.25 8.25h19.5M2.25 9h19.5m-16.5 5.25h6m-6 2.25h3m-3.75 3h15a1.5 1.5 0 0 0 1.5-1.5V6a1.5 1.5 0 0 0-1.5-1.5h-15A1.5 1.5 0 0 0 2.25 6v12a1.5 1.5 0 0 0 1.5 1.5Z',
  calendar:
    'M6.75 3v2.25M17.25 3v2.25M3 18.75V7.5a2.25 2.25 0 0 1 2.25-2.25h13.5A2.25 2.25 0 0 1 21 7.5v11.25m-18 0A2.25 2.25 0 0 0 5.25 21h13.5A2.25 2.25 0 0 0 21 18.75m-18 0v-7.5A2.25 2.25 0 0 1 5.25 9h13.5A2.25 2.25 0 0 1 21 11.25v7.5',
} as const;

/**
 * Punto de entrada tras login: atajos según el rol, sin datos sensibles adicionales.
 * Rediseño de las tarjetas de acceso (antes: enlaces de texto plano dentro de shk-card): ahora
 * cada una lleva un ícono en insignia, una descripción de una línea y una flecha que se desplaza
 * al pasar el mouse, para que el panel de administración se vea a la altura del resto del sitio.
 */
@Component({
  selector: 'shk-dashboard',
  imports: [RouterLink],
  template: `
    <h1 class="text-2xl font-bold text-slate-900">
      Bienvenido{{ auth.user()?.fullName ? ', ' + auth.user()?.fullName : '' }}
    </h1>
    <p class="mt-1 text-sm text-slate-500">
      Rol: {{ auth.role() ? roleLabel(auth.role()!) : '—' }}
    </p>

    <div class="mt-8 grid gap-5 sm:grid-cols-2 lg:grid-cols-3">
      @for (link of links(); track link.routerLink) {
        <a
          [routerLink]="link.routerLink"
          class="group relative flex flex-col overflow-hidden rounded-2xl border border-slate-100 bg-[var(--shk-color-surface)]
                 p-6 shadow-[0_2px_16px_rgba(26,39,68,0.06)] transition-all duration-300
                 hover:-translate-y-0.5 hover:border-transparent hover:shadow-[0_12px_32px_rgba(26,39,68,0.14)]"
        >
          <span
            class="pointer-events-none absolute inset-x-0 top-0 h-1 origin-left scale-x-0 bg-gradient-to-r
                   from-[var(--shk-color-accent)] to-[var(--shk-color-accent-dark)] transition-transform duration-300 group-hover:scale-x-100"
          ></span>

          <span
            class="inline-flex h-11 w-11 items-center justify-center rounded-xl bg-[var(--shk-color-primary)]/[0.07]
                   text-[var(--shk-color-primary)] transition-colors duration-300 group-hover:bg-[var(--shk-color-accent)]/15 group-hover:text-[var(--shk-color-accent-dark)]"
          >
            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.6" class="h-5.5 w-5.5">
              <path stroke-linecap="round" stroke-linejoin="round" [attr.d]="link.icon" />
            </svg>
          </span>

          <span class="mt-4 font-[var(--shk-font-heading)] text-base font-semibold text-slate-900">{{ link.title }}</span>
          <span class="mt-1 text-sm leading-relaxed text-slate-500">{{ link.description }}</span>

          <span class="mt-4 inline-flex items-center gap-1.5 text-sm font-semibold text-[var(--shk-color-primary)] transition-colors group-hover:text-[var(--shk-color-accent-dark)]">
            Ir
            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" class="h-4 w-4 transition-transform duration-300 group-hover:translate-x-1">
              <path stroke-linecap="round" stroke-linejoin="round" d="M17.25 8.25 21 12m0 0-3.75 3.75M21 12H3" />
            </svg>
          </span>
        </a>
      }
    </div>
  `,
})
export class DashboardComponent {
  protected readonly auth = inject(AuthStore);

  protected readonly links = computed<DashboardLink[]>(() => {
    const role = this.auth.role();
    const items: DashboardLink[] = [];

    if (role === 'Administrator') {
      items.push(
        { routerLink: '/admin/inscripciones', title: 'Bandeja de inscripciones', description: 'Revisa, aprueba o rechaza solicitudes nuevas.', icon: ICONS.inbox },
        { routerLink: '/admin/docentes', title: 'Docentes', description: 'Administra el cuerpo docente del instituto.', icon: ICONS.users },
        { routerLink: '/admin/academico', title: 'Gestión académica', description: 'Periodos, materias y grupos por región.', icon: ICONS.academic },
      );
    }
    if (role === 'Administrator' || role === 'RegionalCoordinator' || role === 'RegionalSecretary') {
      items.push({ routerLink: '/admin/alumnos', title: 'Alumnos', description: 'Consulta la matrícula activa y su estatus.', icon: ICONS.users });
    }
    if (role === 'Teacher' || role === 'Administrator') {
      items.push({ routerLink: '/admin/calificaciones', title: 'Calificaciones', description: 'Captura y revisa calificaciones por grupo.', icon: ICONS.grades });
    }
    if (role === 'Student') {
      items.push(
        { routerLink: '/admin/mis-materias', title: 'Mis materias', description: 'Consulta tus cursos y calificaciones.', icon: ICONS.book },
        { routerLink: '/admin/mi-kardex', title: 'Mi Kardex', description: 'Tu historial académico completo.', icon: ICONS.kardex },
        { routerLink: '/admin/mi-perfil', title: 'Mi información', description: 'Datos personales y de contacto.', icon: ICONS.profile },
      );
    }
    if (role === 'Administrator' || role === 'RegionalCoordinator' || role === 'RegionalSecretary') {
      items.push(
        { routerLink: '/admin/pagos', title: 'Pagos', description: 'Seguimiento de pagos mensuales por alumno.', icon: ICONS.payments },
        { routerLink: '/admin/calendario', title: 'Calendario', description: 'Eventos y fechas institucionales.', icon: ICONS.calendar },
        { routerLink: '/admin/kardex', title: 'Kardex', description: 'Historial académico de cualquier alumno.', icon: ICONS.kardex },
      );
    }

    return items;
  });

  protected roleLabel(role: string): string {
    return ROLE_LABELS[role as keyof typeof ROLE_LABELS] ?? role;
  }
}
