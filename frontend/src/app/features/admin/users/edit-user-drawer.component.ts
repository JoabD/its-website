import { Component, ElementRef, effect, inject, input, output, signal, viewChild } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { of } from 'rxjs';
import { catchError, map } from 'rxjs/operators';
import { ApiClient } from '../../../core/http/api-client';
import { AdminUpdateUserRequestDto, UserListItemDto } from '../../../api/schema';
import { ButtonComponent } from '../../../shared/ui/button/button.component';
import { InputComponent } from '../../../shared/ui/input/input.component';
import { ToastService } from '../../../shared/ui/toast/toast.service';
import { SwipeToCloseDirective } from '../../../shared/gestures/swipe-to-close.directive';

/**
 * Panel de Usuarios → editar (side panel, mismo patrón visual que AddUserDrawerComponent/
 * AddStudentDrawerComponent). A diferencia de "Agregar usuario", aquí la contraseña es OPCIONAL y,
 * cuando se captura, es la que el admin escribe — no una temporal aleatoria (pedido explícito del
 * cliente: "debe ser un password que pueda agregarse"). Dejar el campo vacío conserva la contraseña
 * vigente del usuario.
 *
 * Al guardar, el backend (AdminUpdateUserCommandHandler) siempre notifica por correo al usuario con
 * su información actualizada, incluyendo la contraseña en texto plano solo si de verdad se cambió.
 */
@Component({
  selector: 'shk-edit-user-drawer',
  imports: [ButtonComponent, InputComponent, FormsModule, SwipeToCloseDirective],
  template: `
    @if (open()) {
      <div class="fixed inset-0 z-40 flex justify-end" (keydown.escape)="close.emit()">
        <div class="absolute inset-0 bg-slate-900/40 backdrop-blur-[2px]" (click)="close.emit()"></div>

        <aside
          #panel
          shkSwipeToClose
          (swipeClose)="close.emit()"
          class="relative flex h-full w-full max-w-xl flex-col bg-[var(--shk-color-surface)] shadow-2xl focus:outline-none"
          role="dialog"
          aria-modal="true"
          tabindex="-1"
        >
          @if (user(); as u) {
            <header class="flex items-start justify-between gap-4 border-b border-slate-100 px-6 py-5">
              <div>
                <h2 class="text-xl font-bold text-slate-900">Editar usuario</h2>
                <p class="mt-1 text-sm text-slate-500">Matrícula {{ u.enrollmentNumber }} · {{ u.role }}</p>
              </div>
              <button
                type="button"
                class="rounded-full p-2 text-slate-400 transition hover:bg-slate-100 hover:text-slate-600"
                aria-label="Cerrar"
                (click)="close.emit()"
              >
                <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" class="h-5 w-5">
                  <path stroke-linecap="round" stroke-linejoin="round" d="M6 18 18 6M6 6l12 12" />
                </svg>
              </button>
            </header>

            <div class="flex-1 overflow-y-auto px-6 py-5">
              <div class="space-y-4">
                <shk-input label="Nombre completo" [required]="true" [ngModel]="fullName()" (ngModelChange)="fullName.set($event)" [ngModelOptions]="{standalone: true}" />
                <shk-input label="Correo" type="email" [required]="true" [ngModel]="email()" (ngModelChange)="email.set($event)" [ngModelOptions]="{standalone: true}" />
                <shk-input label="Teléfono" [required]="true" [ngModel]="phone()" (ngModelChange)="phone.set($event)" [ngModelOptions]="{standalone: true}" />

                <div class="rounded-xl border border-dashed border-slate-300 p-4">
                  <shk-input
                    label="Nueva contraseña (opcional)"
                    type="text"
                    placeholder="Déjalo vacío para no cambiarla"
                    [ngModel]="newPassword()"
                    (ngModelChange)="newPassword.set($event)"
                    [ngModelOptions]="{standalone: true}"
                  />
                  <p class="mt-1.5 text-xs leading-relaxed text-slate-500">
                    Se guarda tal como la escribas (mínimo 8 caracteres) — no es temporal, el usuario no tendrá que cambiarla.
                    Al guardar se le envía por correo junto con el resto de su información.
                  </p>
                </div>

                @if (error()) {
                  <p class="text-sm text-red-600">{{ error() }}</p>
                }
              </div>
            </div>

            <footer class="flex flex-col-reverse gap-3 border-t border-slate-100 px-6 py-4 sm:flex-row sm:items-center sm:justify-end">
              <shk-button variant="ghost" (click)="close.emit()">Cancelar</shk-button>
              <shk-button variant="primary" [loading]="submitting()" (click)="submit()">Guardar cambios</shk-button>
            </footer>
          }
        </aside>
      </div>
    }
  `,
  styles: [
    `
    .shk-field {
      border-radius: 0.75rem;
      border: 1px solid #e2e8f0;
      background: #fff;
      padding: 0.625rem 1rem;
      font-size: 0.875rem;
    }
    `,
  ],
})
export class EditUserDrawerComponent {
  private readonly api = inject(ApiClient);
  private readonly toast = inject(ToastService);
  private readonly panelRef = viewChild<ElementRef<HTMLElement>>('panel');

  readonly open = input<boolean>(false);
  readonly user = input<UserListItemDto | null>(null);
  readonly close = output<void>();
  readonly updated = output<void>();

  protected readonly submitting = signal(false);
  protected readonly error = signal<string | null>(null);

  protected readonly fullName = signal('');
  protected readonly email = signal('');
  protected readonly phone = signal('');
  protected readonly newPassword = signal('');

  private lastFocusedElement: HTMLElement | null = null;

  constructor() {
    effect(() => {
      const isOpen = this.open();
      const u = this.user();
      if (!isOpen) {
        if (this.lastFocusedElement) {
          this.lastFocusedElement.focus();
          this.lastFocusedElement = null;
        }
        return;
      }

      this.lastFocusedElement = (document.activeElement as HTMLElement) ?? null;
      this.fullName.set(u?.fullName ?? '');
      this.email.set(u?.email ?? '');
      this.phone.set(u?.phone ?? '');
      this.newPassword.set('');
      this.error.set(null);
      queueMicrotask(() => this.panelRef()?.nativeElement.focus());
    });
  }

  protected submit(): void {
    const u = this.user();
    if (!u) return;

    this.error.set(null);

    if (!this.fullName().trim() || !this.email().trim() || !this.phone().trim()) {
      this.error.set('Completa todos los campos obligatorios.');
      return;
    }

    const password = this.newPassword().trim();
    if (password && password.length < 8) {
      this.error.set('La nueva contraseña debe tener al menos 8 caracteres.');
      return;
    }

    const body: AdminUpdateUserRequestDto = {
      fullName: this.fullName().trim(),
      email: this.email().trim(),
      phone: this.phone().trim(),
      newPassword: password || null,
    };

    this.submitting.set(true);
    this.api
      .put<void>(`/users/${u.id}/profile`, body)
      .pipe(
        map(() => ({ ok: true as const })),
        catchError((error) => of({ ok: false as const, error })),
      )
      .subscribe((result) => {
        this.submitting.set(false);
        if (!result.ok) {
          this.error.set(result.error?.error?.detail ?? 'No se pudo guardar el usuario.');
          return;
        }
        this.toast.success('Usuario actualizado — se le notificó por correo.');
        this.updated.emit();
        this.close.emit();
      });
  }
}
