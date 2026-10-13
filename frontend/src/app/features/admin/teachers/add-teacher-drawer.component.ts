import { Component, ElementRef, effect, inject, input, output, signal, viewChild } from '@angular/core';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { of } from 'rxjs';
import { catchError, debounceTime, distinctUntilChanged, map, switchMap } from 'rxjs/operators';
import { ApiClient } from '../../../core/http/api-client';
import {
  AssignTeacherRoleRequestDto,
  CreateUserRequestDto,
  CreateUserResponseDto,
  PagedResultDto,
  RegionListItemDto,
  UserListItemDto,
} from '../../../api/schema';
import { ButtonComponent } from '../../../shared/ui/button/button.component';
import { BadgeComponent } from '../../../shared/ui/badge/badge.component';
import { InputComponent } from '../../../shared/ui/input/input.component';
import { ToastService } from '../../../shared/ui/toast/toast.service';
import { SwipeToCloseDirective } from '../../../shared/gestures/swipe-to-close.directive';
import { ROLE_LABELS } from '../../../domain/models';

type Mode = 'new' | 'existing';

/** ¿Esta persona ya puede dar clases, por rol principal o adicional? Ver User.CanTeach (backend). */
function canTeach(user: UserListItemDto): boolean {
  return user.role === 'Teacher' || user.extraRoles.includes('Teacher');
}

/**
 * "Docentes" → "+ Agregar profesor" (2026-10, tercera vuelta tras probar las dos versiones
 * anteriores): dos ajustes más, pedidos directamente por la usuaria.
 *
 * 1) La fecha de nacimiento dejó de ser obligatoria — de hecho se quitó por completo de este
 *    formulario — y se reemplazó por "¿en qué región(es) da clases?" ("un profesor puede ser
 *    asignado a varias regiones y tener múltiples materias"). Es un selector de varias casillas, no
 *    un <select> de una sola región, a propósito: un profesor puede dar clases en más de una región
 *    desde el primer día.
 *
 * 2) El modo "Ya tiene cuenta" YA NO reemplaza el rol de la persona. Antes, si alguien ya era por
 *    ejemplo Coordinador regional, asignarlo como profesor le quitaba ese rol — "no necesitamos
 *    revocar ni un rol, si es Admin así que se quede, puede mantener el rol admin y el de maestro,
 *    un usuario puede tener múltiples roles". Ahora este modo llama a un endpoint nuevo
 *    (POST /users/{id}/teacher-role, ver AssignTeacherRoleCommand) que AGREGA Teacher como rol
 *    adicional sin tocar el rol principal — y el texto en pantalla lo deja así de claro, en vez de
 *    advertir que "va a perder su rol".
 *
 * Es un componente propio (no una variante de AddUserDrawerComponent) a propósito: el selector
 * "nuevo/existente" y el multi-selector de regiones solo tienen sentido aquí, y así el panel
 * genérico de "Usuarios" (que sí necesita correo desde el inicio, para Administrador/Coordinación/
 * Secretaría, y no maneja regiones de enseñanza) no se complica con un caso que no le aplica.
 */
@Component({
  selector: 'shk-add-teacher-drawer',
  imports: [ButtonComponent, BadgeComponent, InputComponent, FormsModule, SwipeToCloseDirective],
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
              <h2 class="text-xl font-bold text-slate-900">Agregar profesor</h2>
              <p class="mt-1 text-sm text-slate-500">
                {{ mode() === 'new' ? 'Da de alta a alguien que no está en el sistema todavía.' : 'Agrega el rol de profesor a alguien que ya tiene cuenta.' }}
              </p>
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

          @if (!createdResult() && !assignedUser()) {
            <div class="flex gap-1 border-b border-slate-100 px-6 pt-4">
              <button
                type="button"
                class="rounded-t-lg px-4 py-2 text-sm font-semibold transition"
                [class]="mode() === 'new' ? 'border-b-2 border-[var(--shk-color-accent)] text-slate-900' : 'text-slate-400 hover:text-slate-600'"
                (click)="setMode('new')"
              >
                Profesor nuevo
              </button>
              <button
                type="button"
                class="rounded-t-lg px-4 py-2 text-sm font-semibold transition"
                [class]="mode() === 'existing' ? 'border-b-2 border-[var(--shk-color-accent)] text-slate-900' : 'text-slate-400 hover:text-slate-600'"
                (click)="setMode('existing')"
              >
                Ya tiene cuenta
              </button>
            </div>
          }

          <div class="flex-1 overflow-y-auto px-6 py-5">
            @if (mode() === 'new') {
              @if (createdResult(); as result) {
                <div class="space-y-4">
                  <div class="flex items-center gap-2 rounded-xl bg-emerald-50 px-4 py-3 text-emerald-800">
                    <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" class="h-5 w-5 shrink-0">
                      <path stroke-linecap="round" stroke-linejoin="round" d="m4.5 12.75 6 6 9-13.5" />
                    </svg>
                    <p class="text-sm font-medium">Profesor agregado correctamente.</p>
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
                      @if (lastEmailProvided()) {
                        También se le envió por correo. Deberá cambiarla en su primer inicio de sesión.
                      } @else {
                        No capturaste un correo, así que no se envió por ningún lado — cópiala y compártesela directamente. Cuando agregues su correo (menú "Editar"), podrá usarla para entrar al sistema.
                      }
                    </p>
                  </div>
                </div>
              } @else {
                <div class="space-y-4">
                  <shk-input label="Nombre completo" [required]="true" [ngModel]="fullName()" (ngModelChange)="fullName.set($event)" [ngModelOptions]="{standalone: true}" />

                  <div class="rounded-xl border border-dashed border-slate-300 p-4 space-y-4">
                    <p class="text-xs leading-relaxed text-slate-500">
                      Correo y teléfono son opcionales por ahora — agrégalos cuando quieras que pueda iniciar sesión en el sistema.
                    </p>
                    <shk-input label="Correo (opcional)" type="email" [ngModel]="email()" (ngModelChange)="email.set($event)" [ngModelOptions]="{standalone: true}" />
                    <shk-input label="Teléfono (opcional)" [ngModel]="phone()" (ngModelChange)="phone.set($event)" [ngModelOptions]="{standalone: true}" />
                  </div>

                  <fieldset class="flex flex-col gap-1.5 text-sm">
                    <legend class="font-medium text-slate-700">¿En qué región(es) da clases? <span class="text-[var(--shk-color-accent-dark)]">*</span></legend>
                    <p class="text-xs text-slate-500">Elige al menos una. Puedes marcar varias si da clases en más de un lugar.</p>
                    <div class="mt-1.5 grid gap-2 sm:grid-cols-2">
                      @for (region of regions(); track region.id) {
                        <label class="flex items-center gap-2 rounded-lg border border-slate-200 px-3 py-2 text-sm text-slate-700 transition hover:bg-slate-50">
                          <input
                            type="checkbox"
                            class="h-4 w-4 rounded border-slate-300 text-[var(--shk-color-accent)] focus:ring-[var(--shk-color-accent)]"
                            [checked]="teachingRegionIds().includes(region.id)"
                            (change)="toggleRegion(region.id)"
                          />
                          {{ region.name }}
                        </label>
                      }
                    </div>
                  </fieldset>

                  @if (error()) {
                    <p class="text-sm text-red-600">{{ error() }}</p>
                  }
                </div>
              }
            } @else {
              @if (assignedUser(); as assigned) {
                <div class="flex items-center gap-2 rounded-xl bg-emerald-50 px-4 py-3 text-emerald-800">
                  <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" class="h-5 w-5 shrink-0">
                    <path stroke-linecap="round" stroke-linejoin="round" d="m4.5 12.75 6 6 9-13.5" />
                  </svg>
                  <p class="text-sm font-medium">
                    {{ assigned.fullName }} ahora también es profesor — conserva su rol de {{ roleLabel(assigned.role) }}.
                  </p>
                </div>
              } @else if (selectedUser(); as selected) {
                <div class="space-y-4">
                  <div class="rounded-xl border border-slate-200 p-4">
                    <p class="font-semibold text-slate-900">{{ selected.fullName }}</p>
                    <p class="mt-0.5 text-sm text-slate-500">Matrícula {{ selected.enrollmentNumber }} @if (selected.email) { · {{ selected.email }} }</p>
                    <p class="mt-2 flex flex-wrap items-center gap-1.5 text-sm">
                      Rol actual: <shk-badge tone="neutral">{{ roleLabel(selected.role) }}</shk-badge>
                      @for (extra of selected.extraRoles; track extra) {
                        <shk-badge tone="success">{{ roleLabel(extra) }}</shk-badge>
                      }
                    </p>
                  </div>

                  <div class="rounded-xl bg-emerald-50 px-4 py-3 text-sm text-emerald-800">
                    No se le quita nada: <strong>{{ selected.fullName }}</strong> conserva su rol de "{{ roleLabel(selected.role) }}" y además podrá dar clases como profesor. Un usuario puede tener varios roles a la vez.
                  </div>

                  <fieldset class="flex flex-col gap-1.5 text-sm">
                    <legend class="font-medium text-slate-700">¿En qué región(es) va a dar clases? <span class="text-[var(--shk-color-accent-dark)]">*</span></legend>
                    <p class="text-xs text-slate-500">Elige al menos una. Puedes marcar varias si da clases en más de un lugar.</p>
                    <div class="mt-1.5 grid gap-2 sm:grid-cols-2">
                      @for (region of regions(); track region.id) {
                        <label class="flex items-center gap-2 rounded-lg border border-slate-200 px-3 py-2 text-sm text-slate-700 transition hover:bg-slate-50">
                          <input
                            type="checkbox"
                            class="h-4 w-4 rounded border-slate-300 text-[var(--shk-color-accent)] focus:ring-[var(--shk-color-accent)]"
                            [checked]="teachingRegionIds().includes(region.id)"
                            (change)="toggleRegion(region.id)"
                          />
                          {{ region.name }}
                        </label>
                      }
                    </div>
                  </fieldset>

                  @if (error()) {
                    <p class="text-sm text-red-600">{{ error() }}</p>
                  }

                  <button type="button" class="text-sm font-semibold text-slate-500 underline hover:text-slate-700" (click)="selectedUser.set(null)">
                    ← Elegir a otra persona
                  </button>
                </div>
              } @else {
                <div class="space-y-4">
                  <shk-input
                    label="Buscar por nombre o correo"
                    placeholder="Escribe al menos 2 letras…"
                    [ngModel]="searchText()"
                    (ngModelChange)="searchText.set($event)"
                    [ngModelOptions]="{standalone: true}"
                  />

                  @if (searchText().trim().length < 2) {
                    <p class="text-sm text-slate-400">Escribe un nombre o correo para empezar a buscar.</p>
                  } @else if (searchResults() === null) {
                    <p class="text-sm text-slate-400">Buscando…</p>
                  } @else if (searchResults()!.length === 0) {
                    <p class="text-sm text-slate-400">No se encontró a nadie con ese nombre o correo.</p>
                  } @else {
                    <ul class="divide-y divide-slate-100 rounded-xl border border-slate-200">
                      @for (candidate of searchResults()!; track candidate.id) {
                        <li>
                          <button
                            type="button"
                            class="flex w-full items-center justify-between gap-3 px-4 py-3 text-left transition hover:bg-slate-50"
                            (click)="selectedUser.set(candidate)"
                          >
                            <span>
                              <span class="block font-medium text-slate-900">{{ candidate.fullName }}</span>
                              <span class="block text-xs text-slate-500">{{ candidate.email ?? 'Sin correo' }}</span>
                            </span>
                            <shk-badge tone="neutral">{{ roleLabel(candidate.role) }}</shk-badge>
                          </button>
                        </li>
                      }
                    </ul>
                  }
                </div>
              }
            }
          </div>

          <footer class="flex flex-col-reverse gap-3 border-t border-slate-100 px-6 py-4 sm:flex-row sm:items-center sm:justify-end">
            @if (createdResult() || assignedUser()) {
              <shk-button variant="primary" (click)="finish()">Listo</shk-button>
            } @else if (mode() === 'new') {
              <shk-button variant="ghost" (click)="close.emit()">Cancelar</shk-button>
              <shk-button variant="primary" [loading]="submitting()" (click)="submitNew()">Agregar profesor</shk-button>
            } @else if (selectedUser()) {
              <shk-button variant="ghost" (click)="close.emit()">Cancelar</shk-button>
              <shk-button variant="primary" [loading]="submitting()" (click)="submitExisting()">
                Agregar rol de profesor
              </shk-button>
            } @else {
              <shk-button variant="ghost" (click)="close.emit()">Cancelar</shk-button>
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
    `,
  ],
})
export class AddTeacherDrawerComponent {
  private readonly api = inject(ApiClient);
  private readonly toast = inject(ToastService);
  private readonly panelRef = viewChild<ElementRef<HTMLElement>>('panel');

  readonly open = input<boolean>(false);
  readonly close = output<void>();
  readonly added = output<void>();

  protected readonly mode = signal<Mode>('new');

  // --- Catálogo de regiones (ver AddUserDrawerComponent, mismo endpoint) ---
  private readonly regionsResource = toSignal(
    this.api.get<RegionListItemDto[]>('/catalog/regions').pipe(catchError(() => of<RegionListItemDto[]>([]))),
    { initialValue: [] as RegionListItemDto[] },
  );
  protected readonly regions = () => this.regionsResource();

  // --- Modo "nuevo" ---
  protected readonly submitting = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly createdResult = signal<CreateUserResponseDto | null>(null);
  protected readonly lastEmailProvided = signal(false);
  protected readonly copied = signal(false);

  protected readonly fullName = signal('');
  protected readonly email = signal('');
  protected readonly phone = signal('');

  // Compartido entre ambos modos (2026-10, "un profesor puede ser asignado a varias regiones"):
  // regiones donde la persona va a dar clases. Se vacía cada vez que cambia el modo o la persona
  // seleccionada, para no arrastrar regiones elegidas para alguien más.
  protected readonly teachingRegionIds = signal<string[]>([]);

  // --- Modo "ya tiene cuenta" ---
  protected readonly searchText = signal('');
  protected readonly selectedUser = signal<UserListItemDto | null>(null);
  protected readonly assignedUser = signal<UserListItemDto | null>(null);

  private readonly searchResultsSignal = toSignal(
    toObservable(this.searchText).pipe(
      debounceTime(300),
      distinctUntilChanged(),
      switchMap((term) => {
        const trimmed = term.trim();
        if (trimmed.length < 2) return of<UserListItemDto[] | null>(null);
        return this.api
          .get<PagedResultDto<UserListItemDto>>('/users', { search: trimmed, pageSize: 10 })
          .pipe(
            // Ya no filtra solo por role !== 'Teacher': alguien con Teacher como rol ADICIONAL
            // (ver canTeach()) ya no tiene nada que ganar aquí — pero alguien cuyo rol principal YA
            // es Teacher y que no ha sido excluido antes sí debía quedar fuera igual.
            map((response) => response.items.filter((u) => !canTeach(u))),
            catchError(() => of<UserListItemDto[]>([])),
          );
      }),
    ),
    { initialValue: null },
  );
  protected readonly searchResults = () => this.searchResultsSignal();

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

  protected roleLabel(role: UserListItemDto['role']): string {
    return ROLE_LABELS[role];
  }

  protected setMode(mode: Mode): void {
    this.mode.set(mode);
    this.error.set(null);
    this.teachingRegionIds.set([]);
  }

  protected toggleRegion(regionId: string): void {
    const current = this.teachingRegionIds();
    this.teachingRegionIds.set(current.includes(regionId) ? current.filter((id) => id !== regionId) : [...current, regionId]);
  }

  private resetForm(): void {
    this.mode.set('new');
    this.fullName.set('');
    this.email.set('');
    this.phone.set('');
    this.teachingRegionIds.set([]);
    this.error.set(null);
    this.createdResult.set(null);
    this.copied.set(false);
    this.searchText.set('');
    this.selectedUser.set(null);
    this.assignedUser.set(null);
  }

  protected submitNew(): void {
    this.error.set(null);

    if (!this.fullName().trim()) {
      this.error.set('Completa el nombre.');
      return;
    }

    if (this.teachingRegionIds().length === 0) {
      this.error.set('Elige al menos una región donde dará clases.');
      return;
    }

    const email = this.email().trim();
    const phone = this.phone().trim();

    const body: CreateUserRequestDto = {
      role: 'Teacher',
      fullName: this.fullName().trim(),
      email: email || null,
      phone: phone || null,
      regionIds: this.teachingRegionIds(),
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
          this.error.set(result.error?.error?.detail ?? 'No se pudo agregar al profesor.');
          return;
        }
        this.lastEmailProvided.set(!!email);
        this.createdResult.set(result.response);
        this.added.emit();
      });
  }

  protected submitExisting(): void {
    const user = this.selectedUser();
    if (!user) return;

    this.error.set(null);

    if (this.teachingRegionIds().length === 0) {
      this.error.set('Elige al menos una región donde dará clases.');
      return;
    }

    // Ver AssignTeacherRoleCommand (backend): a diferencia de la versión anterior, esto YA NO llama
    // a PUT /users/{id} (que reemplazaría el rol principal) — agrega Teacher como rol adicional.
    const body: AssignTeacherRoleRequestDto = { regionIds: this.teachingRegionIds() };

    this.submitting.set(true);
    this.api
      .post<void>(`/users/${user.id}/teacher-role`, body)
      .pipe(
        map(() => ({ ok: true as const })),
        catchError((error) => of({ ok: false as const, error })),
      )
      .subscribe((result) => {
        this.submitting.set(false);
        if (!result.ok) {
          this.error.set(result.error?.error?.detail ?? 'No se pudo agregar el rol de profesor.');
          return;
        }
        this.assignedUser.set(user);
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
