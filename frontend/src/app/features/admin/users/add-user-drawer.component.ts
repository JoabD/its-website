import { Component, ElementRef, effect, inject, input, output, signal, viewChild } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { of } from 'rxjs';
import { catchError, map } from 'rxjs/operators';
import { ApiClient } from '../../../core/http/api-client';
import { CreateUserRequestDto, CreateUserResponseDto, RegionListItemDto } from '../../../api/schema';
import { ButtonComponent } from '../../../shared/ui/button/button.component';
import { InputComponent } from '../../../shared/ui/input/input.component';
import { ToastService } from '../../../shared/ui/toast/toast.service';
import { SwipeToCloseDirective } from '../../../shared/gestures/swipe-to-close.directive';
import { ROLE_LABELS, UserRole } from '../../../domain/models';

/** Roles que se pueden dar de alta desde este panel — Student queda fuera a propósito: tiene su
 * propio flujo en Alumnos → "Agregar alumno" (matrícula, plan, cuatrimestre), que este panel no
 * replica. El backend igual lo rechaza (CreateUserCommandValidator), esto es solo para no ofrecer
 * una opción que de todos modos va a fallar. */
const ASSIGNABLE_ROLES: readonly UserRole[] = ['Administrator', 'Teacher', 'RegionalCoordinator', 'RegionalSecretary'];

/**
 * Panel de Usuarios → "Agregar usuario" (RN-12: solo Administrator). Mismo patrón visual que
 * AddStudentDrawerComponent (encabezado/cuerpo/pie fijos, Esc cierra, foco vuelve a quien abrió),
 * pero de un solo paso — sin pestaña de importación masiva, que no aplica a roles de staff.
 *
 * Tras crear al usuario se muestra la contraseña temporal en el propio drawer (no en un toast, que
 * desaparece solo) para que el admin tenga tiempo de copiarla y comunicársela — el usuario nuevo
 * debe cambiarla en su primer inicio de sesión (RN-24, mustChangePassword).
 */
@Component({
  selector: 'shk-add-user-drawer',
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
          <header class="flex items-start justify-between gap-4 border-b border-slate-100 px-6 py-5">
            <div>
              <h2 class="text-xl font-bold text-slate-900">Agregar usuario</h2>
              <p class="mt-1 text-sm text-slate-500">Personal administrativo, docentes y coordinación — para alumnos usa "Alumnos".</p>
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
            @if (createdResult(); as result) {
              <div class="space-y-4">
                <div class="flex items-center gap-2 rounded-xl bg-emerald-50 px-4 py-3 text-emerald-800">
                  <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" class="h-5 w-5 shrink-0">
                    <path stroke-linecap="round" stroke-linejoin="round" d="m4.5 12.75 6 6 9-13.5" />
                  </svg>
                  <p class="text-sm font-medium">Usuario creado correctamente.</p>
                </div>

                <div class="rounded-xl border border-slate-200 p-4">
                  <p class="text-xs font-medium uppercase tracking-wide text-slate-400">Contraseña temporal</p>
                  <div class="mt-1.5 flex items-center gap-2">
                    <code class="flex-1 rounded-lg bg-slate-50 px-3 py-2 font-mono text-sm text-slate-800">{{ result.temporaryPassword }}</code>
                    <button
                      type="button"
                      class="rounded-lg border border-slate-200 px-3 py-2 text-xs font-semibold text-slate-600 transition hover:bg-slate-50"
                      (click)="copyPassword(result.temporaryPassword)"
                    >
                      {{ copied() ? 'Copiado' : 'Copiar' }}
                    </button>
                  </div>
                  <p class="mt-2 text-xs leading-relaxed text-slate-500">
                    Compártela por un medio seguro con el nuevo usuario. Deberá cambiarla al iniciar sesión por primera vez.
                  </p>
                </div>
              </div>
            } @else {
              <div class="space-y-4">
                <label class="flex flex-col gap-1.5 text-sm">
                  <span class="font-medium text-slate-700">Rol <span class="text-[var(--shk-color-accent-dark)]">*</span></span>
                  <!-- BUG REAL encontrado: un <select> con [value]/(change) planos, cuyas <option> se
                       generan con @for, puede desincronizarse del valor real seleccionado por el
                       usuario y seguir enviando el valor inicial del signal ("Teacher") sin importar
                       qué se elija en pantalla. [ngModel]/(ngModelChange) (mismo patrón que el resto
                       del formulario) usa el SelectControlValueAccessor de Angular, que sí registra
                       cada <option> y mantiene la selección sincronizada de forma confiable. -->
                  <select class="shk-field" [ngModel]="role()" (ngModelChange)="role.set($event)" [ngModelOptions]="{standalone: true}" name="role">
                    @for (r of assignableRoles; track r) {
                      <option [value]="r">{{ roleLabel(r) }}</option>
                    }
                  </select>
                </label>

                <shk-input label="Nombre completo" [required]="true" [ngModel]="fullName()" (ngModelChange)="fullName.set($event)" [ngModelOptions]="{standalone: true}" />
                <shk-input label="Correo" type="email" [required]="true" [ngModel]="email()" (ngModelChange)="email.set($event)" [ngModelOptions]="{standalone: true}" />
                <shk-input label="Teléfono" [required]="true" [ngModel]="phone()" (ngModelChange)="phone.set($event)" [ngModelOptions]="{standalone: true}" />
                <shk-input label="Fecha de nacimiento" type="date" [required]="true" [ngModel]="birthDate()" (ngModelChange)="birthDate.set($event)" [ngModelOptions]="{standalone: true}" />

                @if (needsRegion()) {
                  <label class="flex flex-col gap-1.5 text-sm">
                    <span class="font-medium text-slate-700">Región <span class="text-[var(--shk-color-accent-dark)]">*</span></span>
                    <select class="shk-field" [ngModel]="regionId()" (ngModelChange)="regionId.set($event)" [ngModelOptions]="{standalone: true}" name="regionId">
                      <option value="">Selecciona una región…</option>
                      @for (region of regions(); track region.id) {
                        <option [value]="region.id">{{ region.name }}</option>
                      }
                    </select>
                  </label>
                }

                @if (error()) {
                  <p class="text-sm text-red-600">{{ error() }}</p>
                }
              </div>
            }
          </div>

          <footer class="flex flex-col-reverse gap-3 border-t border-slate-100 px-6 py-4 sm:flex-row sm:items-center sm:justify-end">
            @if (createdResult()) {
              <shk-button variant="primary" (click)="finish()">Listo</shk-button>
            } @else {
              <shk-button variant="ghost" (click)="close.emit()">Cancelar</shk-button>
              <shk-button variant="primary" [loading]="submitting()" (click)="submit()">Agregar usuario</shk-button>
            }
          </footer>
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
    .shk-field:focus { outline: none; border-color: var(--shk-color-accent); box-shadow: 0 0 0 2px color-mix(in srgb, var(--shk-color-accent) 30%, transparent); }
    `,
  ],
})
export class AddUserDrawerComponent {
  private readonly api = inject(ApiClient);
  private readonly toast = inject(ToastService);
  private readonly panelRef = viewChild<ElementRef<HTMLElement>>('panel');

  readonly open = input<boolean>(false);
  readonly close = output<void>();
  readonly added = output<void>();

  protected readonly assignableRoles = ASSIGNABLE_ROLES;

  protected readonly submitting = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly createdResult = signal<CreateUserResponseDto | null>(null);
  protected readonly copied = signal(false);

  protected readonly role = signal<UserRole>('Teacher');
  protected readonly fullName = signal('');
  protected readonly email = signal('');
  protected readonly phone = signal('');
  protected readonly birthDate = signal('');
  protected readonly regionId = signal('');

  protected readonly needsRegion = () => this.role() === 'RegionalCoordinator' || this.role() === 'RegionalSecretary';

  private readonly regionsResource = toSignal(
    this.api.get<RegionListItemDto[]>('/catalog/regions').pipe(catchError(() => of<RegionListItemDto[]>([]))),
    { initialValue: [] as RegionListItemDto[] },
  );
  protected readonly regions = () => this.regionsResource();

  private lastFocusedElement: HTMLElement | null = null;

  constructor() {
    effect(() => {
      const isOpen = this.open();
      if (!isOpen) {
        if (this.lastFocusedElement) {
          this.lastFocusedElement.focus();
          this.lastFocusedElement = null;
        }
        return;
      }

      this.lastFocusedElement = (document.activeElement as HTMLElement) ?? null;
      this.resetForm();
      queueMicrotask(() => this.panelRef()?.nativeElement.focus());
    });
  }

  protected roleLabel(role: UserRole): string {
    return ROLE_LABELS[role];
  }

  private resetForm(): void {
    this.role.set('Teacher');
    this.fullName.set('');
    this.email.set('');
    this.phone.set('');
    this.birthDate.set('');
    this.regionId.set('');
    this.error.set(null);
    this.createdResult.set(null);
    this.copied.set(false);
  }

  protected submit(): void {
    this.error.set(null);

    if (!this.fullName().trim() || !this.email().trim() || !this.phone().trim() || !this.birthDate()) {
      this.error.set('Completa todos los campos obligatorios.');
      return;
    }

    if (this.needsRegion() && !this.regionId()) {
      this.error.set('Selecciona una región para este rol.');
      return;
    }

    const body: CreateUserRequestDto = {
      role: this.role(),
      fullName: this.fullName().trim(),
      email: this.email().trim(),
      phone: this.phone().trim(),
      birthDate: this.birthDate(),
      regionId: this.needsRegion() ? this.regionId() : null,
    };

    this.submitting.set(true);
    this.api
      .post<CreateUserResponseDto>('/users', body)
      .pipe(
        map((response) => ({ ok: true as const, response })),
        catchError((error) => of({ ok: false as const, error })),
      )
      .subscribe((result) => {
        this.submitting.set(false);
        if (!result.ok) {
          this.error.set(result.error?.error?.detail ?? 'No se pudo agregar al usuario.');
          return;
        }
        this.createdResult.set(result.response);
        this.added.emit();
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

  protected finish(): void {
    this.close.emit();
  }
}
