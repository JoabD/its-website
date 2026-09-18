import { Component, inject, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { DatePipe } from '@angular/common';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { of } from 'rxjs';
import { catchError, switchMap, tap } from 'rxjs/operators';
import { ApiClient } from '../../../core/http/api-client';
import { CalendarEventDto, RegionListItemDto } from '../../../api/schema';
import { AuthStore } from '../../../core/auth/auth.store';
import { CardComponent } from '../../../shared/ui/card/card.component';
import { ButtonComponent } from '../../../shared/ui/button/button.component';
import { ToastService } from '../../../shared/ui/toast/toast.service';

/**
 * Plan de control escolar, fase 7: administración del calendario institucional. Administrator
 * puede crear eventos generales (sin región) o de cualquier región; RegionalCoordinator y
 * RegionalSecretary solo ven/eligen su propia región en este formulario — el backend además
 * fuerza esto en el handler vía IRegionScopeResolver (RN-09), así que aunque alguien manipulara
 * el <select> en el navegador, el servidor ignora cualquier región distinta a la suya para esos
 * dos roles.
 */
@Component({
  selector: 'shk-calendar-admin',
  imports: [CardComponent, ButtonComponent, ReactiveFormsModule, DatePipe],
  template: `
    <h1 class="text-2xl font-bold text-slate-900">Calendario</h1>
    <p class="mt-1 text-sm text-slate-500">
      Estos eventos se muestran públicamente en /calendario, sin necesidad de iniciar sesión.
    </p>

    <shk-card class="mt-6">
      <form [formGroup]="form" class="space-y-3" (ngSubmit)="submit()">
        <label class="flex flex-col gap-1.5 text-sm">
          <span class="font-medium text-slate-700">Título</span>
          <input formControlName="title" type="text" class="shk-field" placeholder="Ej. Examen final de cuatrimestre" />
        </label>

        <label class="flex flex-col gap-1.5 text-sm">
          <span class="font-medium text-slate-700">Descripción (opcional)</span>
          <textarea formControlName="description" rows="3" class="shk-field"></textarea>
        </label>

        <div class="grid grid-cols-1 gap-3 sm:grid-cols-2">
          <label class="flex flex-col gap-1.5 text-sm">
            <span class="font-medium text-slate-700">Inicia</span>
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
          <p class="text-xs text-slate-500">Se publicará únicamente para tu región.</p>
        }

        <shk-button type="submit" [loading]="creating()" [disabled]="form.invalid">Publicar evento</shk-button>
      </form>
    </shk-card>

    <div class="mt-6 space-y-3">
      @for (item of events(); track item.id) {
        <shk-card>
          <div class="flex items-start justify-between gap-4">
            <div>
              <h2 class="font-semibold text-slate-900">{{ item.title }}</h2>
              @if (item.description) {
                <p class="mt-1 text-sm text-slate-600">{{ item.description }}</p>
              }
            </div>
            <div class="shrink-0 text-right text-xs text-slate-400">
              <div>{{ item.startAtUtc | date: 'dd/MM/yyyy HH:mm' }}</div>
              <div class="mt-1 font-medium text-slate-500">{{ item.regionName ?? 'General' }}</div>
            </div>
          </div>
        </shk-card>
      } @empty {
        <p class="py-6 text-center text-slate-400">Sin eventos próximos.</p>
      }
    </div>
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
export class CalendarAdminComponent {
  private readonly fb = inject(NonNullableFormBuilder);
  private readonly api = inject(ApiClient);
  private readonly toast = inject(ToastService);
  protected readonly auth = inject(AuthStore);

  protected readonly creating = signal(false);
  private readonly refreshTick = signal(0);

  protected readonly form = this.fb.group({
    title: this.fb.control('', [Validators.required, Validators.maxLength(160)]),
    description: this.fb.control(''),
    startAt: this.fb.control('', Validators.required),
    endAt: this.fb.control(''),
    regionId: this.fb.control<string | null>(null),
  });

  protected readonly regions = toSignal(
    this.api.get<RegionListItemDto[]>('/catalog/regions').pipe(catchError(() => of<RegionListItemDto[]>([]))),
    { initialValue: [] as RegionListItemDto[] },
  );

  protected readonly events = toSignal(
    toObservable(this.refreshTick).pipe(
      switchMap(() =>
        this.api.get<CalendarEventDto[]>('/calendar').pipe(catchError(() => of<CalendarEventDto[]>([]))),
      ),
    ),
    { initialValue: [] as CalendarEventDto[] },
  );

  protected submit(): void {
    if (this.form.invalid) return;
    this.creating.set(true);

    const raw = this.form.getRawValue();
    const payload = {
      title: raw.title,
      description: raw.description || null,
      startAtUtc: new Date(raw.startAt).toISOString(),
      endAtUtc: raw.endAt ? new Date(raw.endAt).toISOString() : null,
      regionId: this.auth.role() === 'Administrator' ? raw.regionId : null,
    };

    this.api.post('/calendar', payload).pipe(
      tap({
        next: () => {
          this.toast.success('Evento publicado en el calendario.');
          this.form.reset({ title: '', description: '', startAt: '', endAt: '', regionId: null });
          this.refreshTick.update((n) => n + 1);
        },
        error: () => this.toast.error('No se pudo publicar el evento.'),
      }),
      catchError(() => of(null)),
    ).subscribe(() => this.creating.set(false));
  }
}
