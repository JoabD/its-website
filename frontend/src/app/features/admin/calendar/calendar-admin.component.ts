import { Component, computed, inject, signal } from '@angular/core';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { of } from 'rxjs';
import { catchError, switchMap } from 'rxjs/operators';
import {
  CalendarEvent as NgxCalendarEvent,
  CalendarMonthViewComponent,
  CalendarPreviousViewDirective,
  CalendarNextViewDirective,
  CalendarTodayDirective,
  CalendarMonthViewDay,
} from 'angular-calendar';
import { ApiClient } from '../../../core/http/api-client';
import { CalendarEventDto } from '../../../api/schema';
import { AuthStore } from '../../../core/auth/auth.store';
import { CardComponent } from '../../../shared/ui/card/card.component';
import { ButtonComponent } from '../../../shared/ui/button/button.component';
import { CalendarEventDrawerComponent } from './calendar-event-drawer.component';

/**
 * Plan de control escolar, fase 7 — mejora visual: calendario institucional del panel admin
 * mostrado como cuadrícula de mes (angular-calendar), al estilo Google Calendar, en vez de la
 * lista cronológica original. Clic en un día vacío abre el drawer para publicar un evento nuevo
 * con esa fecha precargada; clic en un evento abre su detalle con opción de eliminar. Reutiliza el
 * mismo hash determinístico región→color que /calendario (público) para que el mismo color de
 * sede se vea igual en ambas pantallas.
 */
@Component({
  selector: 'shk-calendar-admin',
  imports: [CardComponent, ButtonComponent, CalendarMonthViewComponent, CalendarPreviousViewDirective, CalendarNextViewDirective, CalendarTodayDirective, CalendarEventDrawerComponent],
  template: `
    <div class="flex flex-wrap items-center justify-between gap-3">
      <div>
        <h1 class="text-2xl font-bold text-slate-900">Calendario</h1>
        <p class="mt-1 text-sm text-slate-500">
          Estos eventos se muestran públicamente en /calendario, sin necesidad de iniciar sesión.
        </p>
      </div>
      <shk-button variant="primary" (click)="openCreate(null)">
        <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2" class="h-4 w-4">
          <path stroke-linecap="round" stroke-linejoin="round" d="M12 4.5v15m7.5-7.5h-15" />
        </svg>
        Agregar evento
      </shk-button>
    </div>

    @if (regionesActivas().length) {
      <div class="mt-4 flex flex-wrap gap-2.5">
        @for (region of regionesActivas(); track region) {
          <span class="chip-region" [style.--chip-color]="colorFor(region)">
            <span class="punto"></span>{{ region }}
          </span>
        }
        <span class="chip-region chip-general">
          <span class="punto"></span>Institucional
        </span>
      </div>
    }

    <shk-card class="mt-6 calendar-card">
      <div class="calendar-toolbar">
        <div class="calendar-toolbar-nav">
          <button type="button" class="nav-btn" mwlCalendarPreviousView view="month" [viewDate]="viewDate()" (viewDateChange)="viewDate.set($event)" aria-label="Mes anterior">
            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2" class="h-4 w-4">
              <path stroke-linecap="round" stroke-linejoin="round" d="M15.75 19.5 8.25 12l7.5-7.5" />
            </svg>
          </button>
          <button type="button" class="nav-btn nav-btn-today" mwlCalendarToday [viewDate]="viewDate()" (viewDateChange)="viewDate.set($event)">
            Hoy
          </button>
          <button type="button" class="nav-btn" mwlCalendarNextView view="month" [viewDate]="viewDate()" (viewDateChange)="viewDate.set($event)" aria-label="Mes siguiente">
            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2" class="h-4 w-4">
              <path stroke-linecap="round" stroke-linejoin="round" d="m8.25 4.5 7.5 7.5-7.5 7.5" />
            </svg>
          </button>
        </div>
        <h2 class="calendar-month-label">{{ monthLabel() }}</h2>
      </div>

      <mwl-calendar-month-view
        [viewDate]="viewDate()"
        [events]="ngxEvents()"
        locale="es-MX"
        [weekStartsOn]="1"
        (dayClicked)="onDayClicked($event.day)"
        (eventClicked)="onEventClicked($event.event)"
      />
    </shk-card>

    <shk-calendar-event-drawer
      [open]="drawerOpen()"
      [mode]="drawerMode()"
      [viewEvent]="drawerEvent()"
      [defaultDate]="drawerDefaultDate()"
      (close)="drawerOpen.set(false)"
      (saved)="refreshTick.set(refreshTick() + 1)"
      (deleted)="refreshTick.set(refreshTick() + 1)"
    />
  `,
  styles: [
    `
    .chip-region {
      display: inline-flex; align-items: center; gap: 7px;
      font-family: var(--shk-font-heading); font-size: 12.5px; font-weight: 600;
      color: var(--shk-color-primary); background: var(--shk-color-bg, #f5f6f9);
      border: 1px solid rgba(16,28,54,0.08); padding: 5px 13px; border-radius: 20px;
    }
    .chip-general { color: var(--shk-color-text-muted, #4a5568); }
    .chip-general .punto { background: rgba(16,28,54,0.2); }
    .punto { width: 8px; height: 8px; border-radius: 50%; background: var(--chip-color, #c8a250); display: inline-block; }

    .calendar-card { padding: 0; overflow: hidden; }

    .calendar-toolbar {
      display: flex; align-items: center; justify-content: space-between; gap: 12px;
      padding: 18px 20px; border-bottom: 1px solid #eef0f4;
      background: linear-gradient(135deg, var(--shk-color-primary), var(--shk-color-primary-dark));
    }
    .calendar-toolbar-nav { display: flex; align-items: center; gap: 6px; }
    .nav-btn {
      display: inline-flex; align-items: center; justify-content: center;
      height: 34px; min-width: 34px; padding: 0 12px; border-radius: 999px; border: none;
      background: rgba(255,255,255,0.12); color: #fff; font-size: 13px; font-weight: 600;
      font-family: var(--shk-font-heading); cursor: pointer; transition: background 0.15s ease;
    }
    .nav-btn:hover { background: rgba(255,255,255,0.22); }
    .nav-btn-today { padding: 0 16px; }
    .calendar-month-label {
      margin: 0; font-family: var(--shk-font-heading); font-weight: 700; font-size: 17px;
      color: #fff; text-transform: capitalize;
    }

    :host ::ng-deep .cal-month-view {
      background: var(--shk-color-surface);
    }
    :host ::ng-deep .cal-month-view .cal-cell-top {
      min-height: 90px;
    }
    :host ::ng-deep .cal-month-view .cal-day-cell {
      transition: background 0.12s ease;
    }
    :host ::ng-deep .cal-month-view .cal-day-cell:hover {
      background: color-mix(in srgb, var(--shk-color-accent) 6%, transparent);
      cursor: pointer;
    }
    :host ::ng-deep .cal-month-view .cal-day-badge {
      background: var(--shk-color-accent);
    }
    :host ::ng-deep .cal-month-view .cal-header .cal-cell {
      font-family: var(--shk-font-heading); font-weight: 600; font-size: 12px;
      text-transform: uppercase; letter-spacing: 0.04em; color: var(--shk-color-text-muted);
      padding: 10px 0;
    }
    :host ::ng-deep .cal-month-view .cal-day-number {
      font-family: var(--shk-font-heading); font-weight: 600; font-size: 13px;
      color: var(--shk-color-primary); opacity: 1;
    }
    :host ::ng-deep .cal-month-view .cal-today {
      background: color-mix(in srgb, var(--shk-color-accent) 10%, transparent);
    }
    :host ::ng-deep .cal-month-view .cal-today .cal-day-number {
      color: var(--shk-color-accent-dark);
    }
    :host ::ng-deep .cal-month-view .cal-event {
      border-radius: 999px;
    }
    :host ::ng-deep .cal-month-view .cal-events-row {
      margin-top: 4px;
    }
    `,
  ],
})
export class CalendarAdminComponent {
  private readonly api = inject(ApiClient);
  protected readonly auth = inject(AuthStore);

  private static readonly PALETTE = ['#c8a250', '#4a7c8c', '#8c4a6a', '#4a8c5f', '#8c6a4a', '#5a4a8c', '#8c4a4a', '#4a648c'];

  protected readonly viewDate = signal(new Date());
  protected readonly refreshTick = signal(0);

  protected readonly drawerOpen = signal(false);
  protected readonly drawerMode = signal<'create' | 'view'>('create');
  protected readonly drawerEvent = signal<CalendarEventDto | null>(null);
  protected readonly drawerDefaultDate = signal<Date | null>(null);

  // Rango consultado: el mes visible con una semana de colchón a cada lado, para cubrir los días
  // de los meses adyacentes que la cuadrícula también muestra.
  private readonly range = computed(() => {
    const d = this.viewDate();
    const from = new Date(d.getFullYear(), d.getMonth(), 1);
    from.setDate(from.getDate() - 7);
    const to = new Date(d.getFullYear(), d.getMonth() + 1, 0);
    to.setDate(to.getDate() + 7);
    return { from, to };
  });

  private readonly events = toSignal(
    toObservable(computed(() => ({ range: this.range(), tick: this.refreshTick() }))).pipe(
      switchMap(({ range }) =>
        this.api
          .get<CalendarEventDto[]>('/calendar', { from: range.from.toISOString(), to: range.to.toISOString() })
          .pipe(catchError(() => of<CalendarEventDto[]>([]))),
      ),
    ),
    { initialValue: [] as CalendarEventDto[] },
  );

  protected readonly monthLabel = computed(() =>
    this.viewDate().toLocaleDateString('es-MX', { month: 'long', year: 'numeric' }),
  );

  protected readonly regionesActivas = computed(() => {
    const nombres = new Set<string>();
    for (const evento of this.events()) {
      if (evento.regionName) nombres.add(evento.regionName);
    }
    return [...nombres].sort();
  });

  protected readonly ngxEvents = computed<NgxCalendarEvent[]>(() =>
    this.events().map((e) => {
      const color = e.regionName ? this.colorFor(e.regionName) : '#8994a8';
      return {
        id: e.id,
        title: e.title,
        start: new Date(e.startAtUtc),
        end: e.endAtUtc ? new Date(e.endAtUtc) : undefined,
        color: { primary: color, secondary: `color-mix(in srgb, ${color} 15%, white)` },
        meta: e,
      };
    }),
  );

  protected onDayClicked(day: CalendarMonthViewDay): void {
    this.openCreate(day.date);
  }

  protected onEventClicked(event: NgxCalendarEvent): void {
    this.drawerMode.set('view');
    this.drawerEvent.set((event.meta as CalendarEventDto) ?? null);
    this.drawerDefaultDate.set(null);
    this.drawerOpen.set(true);
  }

  protected openCreate(date: Date | null): void {
    this.drawerMode.set('create');
    this.drawerEvent.set(null);
    this.drawerDefaultDate.set(date);
    this.drawerOpen.set(true);
  }

  protected colorFor(regionName: string): string {
    let hash = 0;
    for (let i = 0; i < regionName.length; i++) {
      hash = (hash * 31 + regionName.charCodeAt(i)) >>> 0;
    }
    return CalendarAdminComponent.PALETTE[hash % CalendarAdminComponent.PALETTE.length] ?? '#c8a250';
  }
}
