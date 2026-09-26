import { Component, ElementRef, effect, inject, input, output, signal, viewChild } from '@angular/core';
import { DatePipe } from '@angular/common';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { toSignal } from '@angular/core/rxjs-interop';
import { of } from 'rxjs';
import { catchError, finalize, map } from 'rxjs/operators';
import { ApiClient } from '../../../core/http/api-client';
import { CalendarEventDto, RegionListItemDto } from '../../../api/schema';
import { AuthStore } from '../../../core/auth/auth.store';
import { ButtonComponent } from '../../../shared/ui/button/button.component';
import { BadgeComponent } from '../../../shared/ui/badge/badge.component';
import { ToastService } from '../../../shared/ui/toast/toast.service';
import { SwipeToCloseDirective } from '../../../shared/gestures/swipe-to-close.directive';

type DrawerMode = 'create' | 'view';

/**
 * Calendario institucional (panel admin visual, angular-calendar) → drawer para publicar un
 * evento nuevo (click en un día vacío o botón "Agregar evento") o ver/eliminar uno existente
 * (click en un evento). Estructura calcada de los otros drawers del panel (encabezado/cuerpo/pie
 * fijos, Esc cierra, foco regresa a quien abrió el drawer) para consistencia visual.
 *
 * Reutiliza ReactiveFormsModule + FormGroup (no ngModel suelto) a propósito: ya hubo un bug real
 * en el drawer de alumnos por usar [ngModel] sin FormGroup dentro de un <form> (NG01352) — con
 * FormGroup ese problema no existe.
 */
@Component({
  selector: 'shk-calendar-event-drawer',
  imports: [ButtonComponent, BadgeComponent, ReactiveFormsModule, DatePipe, SwipeToCloseDirective],
  template: `
    @if (open()) {
      <div class="fixed inset-0 z-40 flex justify-end" (keydown.escape)="close.emit()">
        <div class="absolute inset-0 bg-slate-900/40 backdrop-blur-[2px]" (click)="close.emit()"></div>

        <aside
          #panel
          shkSwipeToClose
          (swipeClose)="close.emit()"
          class="relative flex h-full w-full max-w-lg flex-col bg-[var(--shk-color-surface)] shadow-2xl focus:outline-none"
          role="dialog"
          aria-modal="true"
          tabindex="-1"
        >
          <header class="flex items-start justify-between gap-4 border-b border-slate-100 px-6 py-5">
            <div>
              <h2 class="text-xl font-bold text-slate-900">
                {{ mode() === 'create' ? 'Agregar evento' : 'Detalle del evento' }}
              </h2>
              <p class="mt-1 text-sm text-slate-500">
                {{ mode() === 'create' ? 'Se publicará en el calendario institucional (/calendario).' : 'Evento publicado en el calendario institucional.' }}
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

          <div class="flex-1 overflow-y-auto px-6 py-5">
            @if (mode() === 'create') {
              <form [formGroup]="form" class="space-y-4">
                <label class="flex flex-col gap-1.5 text-sm">
                  <span class="font-medium text-slate-700">Título <span class="text-[var(--shk-color-accent-dark)]">*</span></span>
                  <input formControlName="title" type="text" class="shk-field" placeholder="Ej. Examen final de cuatrimestre" />
                </label>

                <label class="flex flex-col gap-1.5 text-sm">
                  <span class="font-medium text-slate-700">Descripción (opcional)</span>
                  <textarea formControlName="description" rows="3" class="shk-field"></textarea>
                </label>

                <div class="grid grid-cols-1 gap-3 sm:grid-cols-2">
                  <label class="flex flex-col gap-1.5 text-sm">
                    <span class="font-medium text-slate-700">Inicia <span class="text-[var(--shk-color-accent-dark)]">*</span></span>
                    <input formControlName="startAt" type="datetime-local" class="shk-field" />
                  </label>
                  <label class="flex flex-col gap-1.5 text-sm">
                    <span class="font-medium text-slate-700">Termina (opcional)</span>
                    <input formControlName="endAt" type="datetime-local" class="shk-field" />
                  </label>
                </div>

                @if (auth.role() === 'Administrator') {
                  <label class="flex flex-col gap-1.5 text-sm">
                    <span class="font-medium text-slate-700">Región</span>
                    <select formControlName="regionId" class="shk-field">
                      <option [value]="null">General (todas las regiones)</option>
                      @for (region of regions(); track region.id) {
                        <option [value]="region.id">{{ region.name }}</option>
                      }
                    </select>
                  </label>
                } @else {
                  <p class="rounded-lg bg-slate-50 px-3 py-2 text-xs text-slate-500">
                    Se publicará únicamente para tu región.
                  </p>
                }

                @if (formError()) {
                  <p class="text-sm text-red-600">{{ formError() }}</p>
                }
              </form>
            } @else if (viewEvent(); as e) {
              <div class="space-y-5">
                <div>
                  <div class="flex items-start justify-between gap-3">
                    <h3 class="text-lg font-semibold text-slate-900">{{ e.title }}</h3>
                    @if (e.regionName) {
                      <shk-badge tone="neutral">{{ e.regionName }}</shk-badge>
                    } @else {
                      <shk-badge tone="success">Institucional</shk-badge>
                    }
                  </div>
                  @if (e.description) {
                    <p class="mt-3 whitespace-pre-line text-sm leading-relaxed text-slate-600">{{ e.description }}</p>
                  }
                </div>

                <div class="rounded-xl bg-slate-50 p-4 text-sm">
                  <div class="flex items-center gap-2 text-slate-700">
                    <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" class="h-4 w-4 text-[var(--shk-color-accent-dark)]">
                      <path stroke-linecap="round" stroke-linejoin="round" d="M6.75 3v2.25M17.25 3v2.25M3.75 8.25h16.5M4.5 6h15a.75.75 0 0 1 .75.75V19.5a.75.75 0 0 1-.75.75h-15a.75.75 0 0 1-.75-.75V6.75A.75.75 0 0 1 4.5 6Z" />
                    </svg>
                    <span>{{ e.startAtUtc | date: 'EEEE d MMMM y, HH:mm' }}</span>
                  </div>
                  @if (e.endAtUtc) {
                    <div class="mt-1.5 flex items-center gap-2 text-slate-500">
                      <span class="ml-6">Hasta {{ e.endAtUtc | date: 'EEEE d MMMM y, HH:mm' }}</span>
                    </div>
                  }
                </div>

                @if (deleteError()) {
                  <p class="text-sm text-red-600">{{ deleteError() }}</p>
                }
              </div>
            }
          </div>

          <footer class="flex flex-col-reverse gap-3 border-t border-slate-100 px-6 py-4 sm:flex-row sm:items-center sm:justify-end">
            @if (mode() === 'create') {
              <shk-button variant="ghost" (click)="close.emit()">Cancelar</shk-button>
              <shk-button variant="primary" [loading]="saving()" (click)="submit()">Publicar evento</shk-button>
            } @else {
              <shk-button variant="danger" [loading]="deleting()" (click)="deleteEvent()">Eliminar evento</shk-button>
              <shk-button variant="ghost" (click)="close.emit()">Cerrar</shk-button>
            }
          </footer>
        </aside>
      </div>
    }
  `,
  styles: [
    `
    .shk-field {
      border-radius: 0.5rem;
      border: 1px solid #e2e8f0;
      background: #fff;
      padding: 0.5rem 0.75rem;
      font-size: 0.875rem;
      width: 100%;
    }
    .shk-field:focus { outline: none; border-color: var(--shk-color-accent); }
    `,
  ],
})
export class CalendarEventDrawerComponent {
  private readonly fb = inject(NonNullableFormBuilder);
  private readonly api = inject(ApiClient);
  private readonly toast = inject(ToastService);
  protected readonly auth = inject(AuthStore);
  private readonly panelRef = viewChild<ElementRef<HTMLElement>>('panel');

  readonly open = input<boolean>(false);
  readonly mode = input<DrawerMode>('create');
  readonly viewEvent = input<CalendarEventDto | null>(null);
  readonly defaultDate = input<Date | null>(null);
  readonly close = output<void>();
  readonly saved = output<void>();
  readonly deleted = output<void>();

  protected readonly saving = signal(false);
  protected readonly deleting = signal(false);
  protected readonly formError = signal<string | null>(null);
  protected readonly deleteError = signal<string | null>(null);

  protected readonly regions = toSignal(
    this.api.get<RegionListItemDto[]>('/catalog/regions').pipe(catchError(() => of<RegionListItemDto[]>([]))),
    { initialValue: [] as RegionListItemDto[] },
  );

  protected readonly form = this.fb.group({
    title: this.fb.control('', [Validators.required, Validators.maxLength(160)]),
    description: this.fb.control(''),
    startAt: this.fb.control('', Validators.required),
    endAt: this.fb.control(''),
    regionId: this.fb.control<string | null>(null),
  });

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
      this.formError.set(null);
      this.deleteError.set(null);

      if (this.mode() === 'create') {
        const base = this.defaultDate() ?? new Date();
        const prefilled = new Date(base);
        if (!this.defaultDate()) {
          // Botón "Agregar evento" sin día preseleccionado: sugiere la próxima hora en punto.
          prefilled.setMinutes(0, 0, 0);
          prefilled.setHours(prefilled.getHours() + 1);
        } else {
          prefilled.setHours(9, 0, 0, 0);
        }
        this.form.reset({ title: '', description: '', startAt: toDatetimeLocal(prefilled), endAt: '', regionId: null });
      }

      queueMicrotask(() => this.panelRef()?.nativeElement.focus());
    });
  }

  protected submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      this.formError.set('Completa el título y la fecha de inicio.');
      return;
    }

    this.formError.set(null);
    this.saving.set(true);

    const raw = this.form.getRawValue();
    const payload = {
      title: raw.title,
      description: raw.description || null,
      startAtUtc: new Date(raw.startAt).toISOString(),
      endAtUtc: raw.endAt ? new Date(raw.endAt).toISOString() : null,
      regionId: this.auth.role() === 'Administrator' ? raw.regionId : null,
    };

    this.api
      .post('/calendar', payload)
      .pipe(
        catchError((error) => {
          this.formError.set(error?.error?.detail ?? 'No se pudo publicar el evento.');
          return of(null);
        }),
        finalize(() => this.saving.set(false)),
      )
      .subscribe((result) => {
        if (result === null) return;
        this.toast.success('Evento publicado en el calendario.');
        this.saved.emit();
        this.close.emit();
      });
  }

  protected deleteEvent(): void {
    const e = this.viewEvent();
    if (!e) return;

    this.deleteError.set(null);
    this.deleting.set(true);

    this.api
      .delete(`/calendar/${e.id}`)
      .pipe(
        map(() => ({ ok: true as const })),
        catchError((error) => of({ ok: false as const, error })),
        finalize(() => this.deleting.set(false)),
      )
      .subscribe((result) => {
        if (!result.ok) {
          this.deleteError.set(result.error?.error?.detail ?? 'No se pudo eliminar el evento.');
          return;
        }
        this.toast.success('Evento eliminado.');
        this.deleted.emit();
        this.close.emit();
      });
  }
}

function toDatetimeLocal(date: Date): string {
  const pad = (n: number) => n.toString().padStart(2, '0');
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}T${pad(date.getHours())}:${pad(date.getMinutes())}`;
}
