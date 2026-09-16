import { Component, inject } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { AuthStore } from '../../../core/auth/auth.store';
import { ButtonComponent } from '../../../shared/ui/button/button.component';

/**
 * RN-07: login por matrícula (número) + password; bloqueo tras intentos fallidos lo decide
 * siempre el backend — este formulario solo refleja el mensaje de error que venga del dominio.
 */
@Component({
  selector: 'shk-login',
  imports: [ReactiveFormsModule, ButtonComponent],
  template: `
    <div class="flex min-h-screen items-center justify-center bg-[var(--shk-color-primary)] px-4 py-12">
      <div class="w-full max-w-sm rounded-2xl bg-white p-8 shadow-[0_20px_60px_rgba(0,0,0,0.25)]">
        <div class="flex flex-col items-center text-center">
          <span class="flex h-12 w-12 items-center justify-center rounded-xl bg-[var(--shk-color-primary)] text-[var(--shk-color-accent)]">
            <svg viewBox="0 0 24 24" width="22" height="22" fill="none" stroke="currentColor" stroke-width="1.8">
              <path d="M2 5.5C4 4.2 7 3.5 9.5 4v14.5C7 18 4 18.7 2 20V5.5Z" />
              <path d="M22 5.5C20 4.2 17 3.5 14.5 4v14.5c2.5-.5 5.5.2 7.5 1.5V5.5Z" />
            </svg>
          </span>
          <h1 class="mt-4 font-[var(--shk-font-heading)] text-lg font-bold text-[var(--shk-color-primary)]">
            Instituto Teológico Shekinah
          </h1>
          <p class="mt-1 text-sm text-slate-500">Acceso alumnos / staff</p>
        </div>

        <form [formGroup]="form" class="mt-8 space-y-4" (ngSubmit)="submit()">
          <label class="flex flex-col gap-1.5 text-sm">
            <span class="font-medium text-slate-700">Matrícula</span>
            <input formControlName="enrollmentNumber" type="number" class="shk-field" placeholder="Ej. 1024" />
          </label>
          <label class="flex flex-col gap-1.5 text-sm">
            <span class="font-medium text-slate-700">Contraseña</span>
            <input formControlName="password" type="password" class="shk-field" placeholder="••••••••" />
          </label>

          @if (auth.error(); as error) {
            <p class="rounded-lg bg-red-50 px-3 py-2 text-sm text-red-600">{{ error }}</p>
          }

          <shk-button type="submit" [loading]="auth.loading()" [disabled]="form.invalid" class="block w-full [&>button]:w-full">
            Iniciar sesión
          </shk-button>
        </form>
      </div>
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
export class LoginComponent {
  private readonly fb = inject(NonNullableFormBuilder);
  protected readonly auth = inject(AuthStore);

  protected readonly form = this.fb.group({
    enrollmentNumber: this.fb.control(0, [Validators.required, Validators.min(1)]),
    password: this.fb.control('', Validators.required),
  });

  protected submit(): void {
    if (this.form.invalid) return;
    const { enrollmentNumber, password } = this.form.getRawValue();
    this.auth.login({ enrollmentNumber, password });
  }
}
