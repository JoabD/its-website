import { Component, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { AuthStore } from '../../../core/auth/auth.store';
import { CardComponent } from '../../../shared/ui/card/card.component';
import { ROLE_LABELS } from '../../../domain/models';

/** Punto de entrada tras login: atajos según el rol, sin datos sensibles adicionales. */
@Component({
  selector: 'shk-dashboard',
  imports: [RouterLink, CardComponent],
  template: `
    <h1 class="text-2xl font-bold text-slate-900">
      Bienvenido{{ auth.user()?.fullName ? ', ' + auth.user()?.fullName : '' }}
    </h1>
    <p class="mt-1 text-sm text-slate-500">
      Rol: {{ auth.role() ? roleLabel(auth.role()!) : '—' }}
    </p>

    <div class="mt-8 grid gap-4 md:grid-cols-3">
      @if (auth.role() === 'Administrator') {
        <shk-card><a routerLink="/admin/inscripciones" class="font-semibold text-[var(--shk-color-primary)]">Bandeja de inscripciones</a></shk-card>
        <shk-card><a routerLink="/admin/docentes" class="font-semibold text-[var(--shk-color-primary)]">Docentes</a></shk-card>
        <shk-card><a routerLink="/admin/academico" class="font-semibold text-[var(--shk-color-primary)]">Gestión académica</a></shk-card>
      }
      @if (auth.role() === 'Administrator' || auth.role() === 'RegionalCoordinator' || auth.role() === 'RegionalSecretary') {
        <shk-card><a routerLink="/admin/alumnos" class="font-semibold text-[var(--shk-color-primary)]">Alumnos</a></shk-card>
      }
      @if (auth.role() === 'Teacher' || auth.role() === 'Administrator') {
        <shk-card><a routerLink="/admin/calificaciones" class="font-semibold text-[var(--shk-color-primary)]">Calificaciones</a></shk-card>
      }
      @if (auth.role() === 'Student') {
        <shk-card><a routerLink="/admin/mis-materias" class="font-semibold text-[var(--shk-color-primary)]">Mis materias</a></shk-card>
        <shk-card><a routerLink="/admin/mi-perfil" class="font-semibold text-[var(--shk-color-primary)]">Mi información</a></shk-card>
      }
      @if (auth.role() === 'Administrator' || auth.role() === 'RegionalCoordinator' || auth.role() === 'RegionalSecretary') {
        <shk-card><a routerLink="/admin/pagos" class="font-semibold text-[var(--shk-color-primary)]">Pagos</a></shk-card>
        <shk-card><a routerLink="/admin/calendario" class="font-semibold text-[var(--shk-color-primary)]">Calendario</a></shk-card>
      }
    </div>
  `,
})
export class DashboardComponent {
  protected readonly auth = inject(AuthStore);

  protected roleLabel(role: string): string {
    return ROLE_LABELS[role as keyof typeof ROLE_LABELS] ?? role;
  }
}
