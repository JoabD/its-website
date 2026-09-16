import { Component, inject, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { of } from 'rxjs';
import { catchError, tap } from 'rxjs/operators';
import { ApiClient } from '../../../core/http/api-client';
import { AuthStore } from '../../../core/auth/auth.store';
import { CardComponent } from '../../../shared/ui/card/card.component';
import { ButtonComponent } from '../../../shared/ui/button/button.component';
import { ToastService } from '../../../shared/ui/toast/toast.service';

/**
 * RN-24: cambio de contraseña obligatorio (temporal o migrada de PHP). Ningún otro endpoint
 * responde hasta completar este paso (ver mustChangePasswordGuard); el backend es quien decide
 * cuándo mustChangePassword pasa a false — esta pantalla solo refresca /auth/me tras el éxito.
 */
@Component({
  selector: 'shk-change-password',
  imports: [ReactiveFormsModule, CardComponent, ButtonComponent],
  template: `
    <div class="flex min-h-[60vh] items-center justify-center px-4">
      <shk-card class="w-full max-w-sm">
        <div class="flex flex-col items-center text-center">
          <span class="flex h-11 w-11 items-center justify-center rounded-xl bg-[var(--shk-color-accent)]/15 text-[var(--shk-color-accent-dark)]">🔒</span>
          <h1 class="mt-3 font-[var(--shk-font-heading)] text-lg font-bold text-[var(--shk-color-primary)]">Cambio de contraseña obligatorio</h1>
          <p class="mt-1 text-sm text-slate-500">Por seguridad debes establecer una nueva contraseña antes de continuar.</p>
        </div>

        <form [formGroup]="form" class="mt-6 space-y-4" (ngSubmit)="submit()">
          <label class="flex flex-col gap-1.5 text-sm">
            <span class="font-medium text-slate-700">Contraseña actual</span>
            <input formControlName="currentPassword" type="password" class="shk-field" />
          </label>
          <label class="flex flex-col gap-1.5 text-sm">
            <span class="font-medium text-slate-700">Nueva contraseña</span>
            <input formControlName="newPassword" type="password" class="shk-field" />
          </label>

          @if (error(); as message) {
            <p class="rounded-lg bg-red-50 px-3 py-2 text-sm text-red-600">{{ message }}</p>
          }

          <shk-button type="submit" [loading]="saving()" [disabled]="form.invalid" class="block w-full [&>button]:w-full">Guardar</shk-button>
        </form>
      </shk-card>
    </div>
  `,
  styles: [
    `
    .shk-field {
      border-radius: 0.75rem;
      border: 1px solid #e2e8f0;
      background: #fff;
      padding: 0.625rem 1rem;
      font-size: 0.875rem;
      line-height: 1.25rem;
      box-shadow: 0 1px 2px rgba(0,0,0,0.03);
      transition: border-color 0.15s, box-shadow 0.15s;
    }
    .shk-field:focus {
      outline: none;
      border-color: var(--shk-color-accent);
      box-shadow: 0 0 0 3px rgba(200,162,80,0.25);
    }
    `,
  ],
})
export class ChangePasswordComponent {
  private readonly fb = inject(NonNullableFormBuilder);
  private readonly api = inject(ApiClient);
  private readonly auth = inject(AuthStore);
  private readonly router = inject(Router);
  private readonly toast = inject(ToastService);

  protected readonly saving = signal(false);
  protected readonly error = signal<string | null>(null);

  protected readonly form = this.fb.group({
    currentPassword: this.fb.control('', Validators.required),
    newPassword: this.fb.control('', [Validators.required, Validators.minLength(8)]),
  });

  protected submit(): void {
    if (this.form.invalid) return;
    this.saving.set(true);
    this.error.set(null);

    this.api.post('/auth/change-password', this.form.getRawValue()).pipe(
      tap({
        next: () => {
          this.toast.success('Contraseña actualizada.');
          this.auth.loadCurrentUser();
          void this.router.navigateByUrl('/admin');
        },
        error: () => this.error.set('No se pudo cambiar la contraseña. Verifica la contraseña actual.'),
      }),
      catchError(() => of(null)),
    ).subscribe(() => this.saving.set(false));
  }
}
