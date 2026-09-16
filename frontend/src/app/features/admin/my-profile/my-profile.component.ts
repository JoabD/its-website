import { Component, inject, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { of } from 'rxjs';
import { catchError, tap } from 'rxjs/operators';
import { ApiClient } from '../../../core/http/api-client';
import { AuthStore } from '../../../core/auth/auth.store';
import { CardComponent } from '../../../shared/ui/card/card.component';
import { ButtonComponent } from '../../../shared/ui/button/button.component';
import { ToastService } from '../../../shared/ui/toast/toast.service';

/** RN-23: el alumno puede actualizar datos de contacto propios; matrícula/rol/estatus son de solo lectura aquí. */
@Component({
  selector: 'shk-my-profile',
  imports: [ReactiveFormsModule, CardComponent, ButtonComponent],
  template: `
    <h1 class="font-[var(--shk-font-heading)] text-2xl font-bold text-[var(--shk-color-primary)]">Mi información</h1>

    <shk-card class="mt-6 max-w-xl">
      <dl class="grid grid-cols-2 gap-4 text-sm text-slate-600">
        <div><dt class="font-semibold text-slate-500">Matrícula</dt><dd class="mt-0.5 text-slate-800">{{ auth.user()?.enrollmentNumber }}</dd></div>
        <div><dt class="font-semibold text-slate-500">Región</dt><dd class="mt-0.5 text-slate-800">{{ auth.user()?.regionName ?? '—' }}</dd></div>
        <div><dt class="font-semibold text-slate-500">Cuatrimestre</dt><dd class="mt-0.5 text-slate-800">{{ auth.user()?.currentTerm ?? '—' }}</dd></div>
      </dl>

      <form [formGroup]="form" class="mt-6 space-y-4 border-t border-slate-100 pt-6" (ngSubmit)="submit()">
        <label class="flex flex-col gap-1.5 text-sm">
          <span class="font-medium text-slate-700">Correo electrónico</span>
          <input formControlName="email" type="email" class="shk-field" />
        </label>
        <label class="flex flex-col gap-1.5 text-sm">
          <span class="font-medium text-slate-700">Teléfono</span>
          <input formControlName="phone" class="shk-field" />
        </label>
        <shk-button type="submit" [loading]="saving()" [disabled]="form.invalid">Guardar cambios</shk-button>
      </form>
    </shk-card>
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
export class MyProfileComponent {
  private readonly fb = inject(NonNullableFormBuilder);
  private readonly api = inject(ApiClient);
  private readonly toast = inject(ToastService);
  protected readonly auth = inject(AuthStore);

  protected readonly saving = signal(false);

  protected readonly form = this.fb.group({
    email: this.fb.control(this.auth.user()?.email ?? '', [Validators.required, Validators.email]),
    phone: this.fb.control('', Validators.required),
  });

  protected submit(): void {
    if (this.form.invalid) return;
    this.saving.set(true);

    this.api.put('/students/me/profile', this.form.getRawValue()).pipe(
      tap({
        next: () => this.toast.success('Información actualizada.'),
        error: () => this.toast.error('No se pudo actualizar la información.'),
      }),
      catchError(() => of(null)),
    ).subscribe(() => this.saving.set(false));
  }
}
