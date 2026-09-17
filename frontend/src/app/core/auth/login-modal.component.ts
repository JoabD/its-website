import { Component, ElementRef, HostListener, inject, viewChild } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { AuthStore } from './auth.store';
import { LoginModalService } from './login-modal.service';
import { ButtonComponent } from '../../shared/ui/button/button.component';

/**
 * Modal de acceso global (montado una sola vez en app.html, fuera del router-outlet), en vez de
 * la antigua página `/admin/login` a pantalla completa. Se abre desde cualquier punto de la app
 * (botón "Acceder" del header público, o el authGuard cuando alguien intenta entrar a /admin sin
 * sesión) llamando a LoginModalService.open() — este componente solo reacciona a esa señal.
 * RN-07: login por correo electrónico + contraseña; el backend decide bloqueo/intentos fallidos,
 * este formulario solo refleja el mensaje de error que venga del dominio.
 */
@Component({
  selector: 'shk-login-modal',
  imports: [ReactiveFormsModule, ButtonComponent],
  template: `
    @if (loginModal.isOpen()) {
      <div class="shk-modal-backdrop" (click)="onBackdropClick($event)">
        <div class="shk-modal-panel" role="dialog" aria-modal="true" aria-labelledby="shk-login-title" #panel>
          <button type="button" class="shk-modal-close" aria-label="Cerrar" (click)="close()">
            <svg viewBox="0 0 24 24" width="18" height="18" fill="none" stroke="currentColor" stroke-width="2">
              <path d="M6 6l12 12M18 6L6 18" stroke-linecap="round" />
            </svg>
          </button>

          <div class="flex flex-col items-center text-center">
            <span class="flex h-12 w-12 items-center justify-center rounded-xl bg-[var(--shk-color-primary)] text-[var(--shk-color-accent)]">
              <svg viewBox="0 0 24 24" width="22" height="22" fill="none" stroke="currentColor" stroke-width="1.8">
                <path d="M2 5.5C4 4.2 7 3.5 9.5 4v14.5C7 18 4 18.7 2 20V5.5Z" />
                <path d="M22 5.5C20 4.2 17 3.5 14.5 4v14.5c2.5-.5 5.5.2 7.5 1.5V5.5Z" />
              </svg>
            </span>
            <h1 id="shk-login-title" class="mt-4 font-[var(--shk-font-heading)] text-lg font-bold text-[var(--shk-color-primary)]">
              Instituto Teológico Shekinah
            </h1>
            <p class="mt-1 text-sm text-slate-500">Acceso alumnos / staff</p>
          </div>

          <form [formGroup]="form" class="mt-8 space-y-4" (ngSubmit)="submit()">
            <label class="flex flex-col gap-1.5 text-sm">
              <span class="font-medium text-slate-700">Correo electrónico</span>
              <input
                formControlName="email"
                type="email"
                autocomplete="email"
                class="shk-field"
                placeholder="tucorreo@ejemplo.com"
              />
            </label>
            <label class="flex flex-col gap-1.5 text-sm">
              <span class="font-medium text-slate-700">Contraseña</span>
              <input formControlName="password" type="password" autocomplete="current-password" class="shk-field" placeholder="••••••••" />
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
    }
  `,
  styles: [
    `
    .shk-modal-backdrop {
      position: fixed;
      inset: 0;
      z-index: 1000;
      display: flex;
      align-items: center;
      justify-content: center;
      padding: 1rem;
      background: rgba(16, 28, 54, 0.55);
      backdrop-filter: blur(2px);
      animation: shk-fade-in 0.15s ease-out;
    }

    .shk-modal-panel {
      position: relative;
      width: 100%;
      max-width: 24rem;
      border-radius: 1rem;
      background: #fff;
      padding: 2rem;
      box-shadow: 0 20px 60px rgba(0, 0, 0, 0.25);
      animation: shk-rise-in 0.18s ease-out;
    }

    .shk-modal-close {
      position: absolute;
      top: 0.75rem;
      right: 0.75rem;
      display: flex;
      height: 2rem;
      width: 2rem;
      align-items: center;
      justify-content: center;
      border-radius: 9999px;
      color: #64748b;
      transition: background 0.15s, color 0.15s;
    }
    .shk-modal-close:hover { background: #f1f5f9; color: #1a2744; }

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

    @keyframes shk-fade-in { from { opacity: 0; } to { opacity: 1; } }
    @keyframes shk-rise-in { from { opacity: 0; transform: translateY(8px) scale(0.98); } to { opacity: 1; transform: none; } }
    `,
  ],
})
export class LoginModalComponent {
  private readonly fb = inject(NonNullableFormBuilder);
  protected readonly auth = inject(AuthStore);
  protected readonly loginModal = inject(LoginModalService);

  private readonly panel = viewChild<ElementRef<HTMLElement>>('panel');

  protected readonly form = this.fb.group({
    email: this.fb.control('', [Validators.required, Validators.email]),
    password: this.fb.control('', Validators.required),
  });

  @HostListener('document:keydown.escape')
  protected onEscape(): void {
    if (this.loginModal.isOpen()) {
      this.close();
    }
  }

  protected onBackdropClick(event: MouseEvent): void {
    if (!this.panel()?.nativeElement.contains(event.target as Node)) {
      this.close();
    }
  }

  protected close(): void {
    this.form.reset();
    this.loginModal.close();
  }

  protected submit(): void {
    if (this.form.invalid) return;
    const { email, password } = this.form.getRawValue();
    this.auth.login({ email, password });
  }
}
