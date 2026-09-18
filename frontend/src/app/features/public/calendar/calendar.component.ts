import { Component, computed, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { toSignal } from '@angular/core/rxjs-interop';
import { of } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { ApiClient } from '../../../core/http/api-client';
import { CalendarEventDto } from '../../../api/schema';

interface CalendarDay {
  key: string;
  dayNumber: string;
  monthLabel: string;
  weekdayLabel: string;
  events: CalendarEventDto[];
}

/**
 * Plan de control escolar, fase 7: calendario institucional público — se consulta SIN necesidad de
 * iniciar sesión (confirmado por el usuario, análogo a /programas). Cada evento con región asignada
 * recibe un distintivo de color propio de esa sede (regionColor), calculado de forma determinística
 * a partir del nombre para que la misma región siempre luzca el mismo color sin necesitar catálogo
 * aparte. Los eventos generales/institucionales (sin región) se muestran sin distintivo.
 */
@Component({
  selector: 'shk-public-calendar',
  imports: [RouterLink],
  template: `
    <section id="hero">
      <div class="contenedor">
        <div class="hero-contenido">
          <span class="eyebrow">Calendario Institucional</span>
          <h1>Fechas y eventos del Instituto</h1>
          <p>
            Consulta exámenes, vacaciones y actividades institucionales. Los eventos marcados con un
            color pertenecen a una sede específica; los demás aplican para todas las regiones.
          </p>
        </div>
      </div>
    </section>

    <section class="seccion">
      <div class="contenedor" style="max-width: 820px;">
        @if (regionesActivas().length) {
          <div class="leyenda">
            @for (region of regionesActivas(); track region) {
              <span class="chip-region" [style.--chip-color]="colorFor(region)">
                <span class="punto"></span>{{ region }}
              </span>
            }
          </div>
        }

        <div class="linea-tiempo">
          @for (dia of dias(); track dia.key) {
            <div class="dia-bloque">
              <div class="dia-fecha">
                <span class="dia-numero">{{ dia.dayNumber }}</span>
                <span class="dia-mes">{{ dia.monthLabel }}</span>
                <span class="dia-semana">{{ dia.weekdayLabel }}</span>
              </div>
              <div class="dia-eventos">
                @for (evento of dia.events; track evento.id) {
                  <div class="evento-card" [class.general]="!evento.regionName" [style.--chip-color]="evento.regionName ? colorFor(evento.regionName) : null">
                    <div class="evento-cabecera">
                      <h3>{{ evento.title }}</h3>
                      @if (evento.regionName) {
                        <span class="badge-region"><span class="punto"></span>{{ evento.regionName }}</span>
                      } @else {
                        <span class="badge-general">Institucional</span>
                      }
                    </div>
                    @if (evento.description) {
                      <p>{{ evento.description }}</p>
                    }
                    @if (evento.endAtUtc) {
                      <span class="evento-rango">Hasta el {{ formatShort(evento.endAtUtc) }}</span>
                    }
                  </div>
                }
              </div>
            </div>
          } @empty {
            <div class="vacio">
              <i class="bi bi-calendar3"></i>
              <p>No hay eventos próximos publicados por el momento.</p>
            </div>
          }
        </div>

        <div class="cta-inscripcion">
          <p>¿Aún no te has inscrito?</p>
          <a routerLink="/inscripcion" class="btn btn-primario"><i class="bi bi-journal-check"></i> Inscríbete ahora</a>
        </div>
      </div>
    </section>
  `,
  styles: `
    #hero {
      min-height: 38vh;
      padding: 70px 0;
      text-align: center;
      background: linear-gradient(120deg, rgba(16, 28, 54, 0.92), rgba(26, 39, 68, 0.85)), url('/img/instituto-6.jpg');
    }
    .hero-contenido { max-width: 680px; margin: 0 auto; }
    .hero-contenido h1 { font-size: clamp(28px, 4vw, 40px); }
    .hero-contenido p { font-size: 16px; margin: 0 auto; max-width: 560px; }

    .seccion { padding: 60px 0; }

    .leyenda { display: flex; flex-wrap: wrap; gap: 10px; margin-bottom: 34px; }
    .chip-region {
      display: inline-flex; align-items: center; gap: 7px;
      font-family: "Poppins", sans-serif; font-size: 12.5px; font-weight: 600;
      color: #1a2744; background: #f5f6f9; border: 1px solid rgba(16,28,54,0.08);
      padding: 6px 14px; border-radius: 20px;
    }
    .punto { width: 8px; height: 8px; border-radius: 50%; background: var(--chip-color, #c8a250); display: inline-block; }

    .linea-tiempo { display: flex; flex-direction: column; gap: 22px; }

    .dia-bloque { display: flex; gap: 20px; }

    .dia-fecha {
      flex: 0 0 64px; display: flex; flex-direction: column; align-items: center;
      background: linear-gradient(135deg, #1a2744, #101c36); color: #fff; border-radius: 14px;
      padding: 12px 6px; height: fit-content;
    }
    .dia-numero { font-family: "Poppins", sans-serif; font-size: 22px; font-weight: 700; line-height: 1; }
    .dia-mes { font-size: 11px; text-transform: uppercase; letter-spacing: 0.06em; color: #e8d5a3; margin-top: 2px; }
    .dia-semana { font-size: 10px; color: rgba(255,255,255,0.6); margin-top: 4px; }

    .dia-eventos { flex: 1; display: flex; flex-direction: column; gap: 12px; }

    .evento-card {
      background: #fff; border-radius: 14px; box-shadow: 0 8px 24px rgba(16,28,54,0.07);
      padding: 18px 22px; border-left: 4px solid var(--chip-color, #c8a250);
    }
    .evento-card.general { border-left-color: rgba(16,28,54,0.15); }

    .evento-cabecera { display: flex; align-items: flex-start; justify-content: space-between; gap: 12px; }
    .evento-cabecera h3 { font-size: 16px; margin: 0; color: #1a2744; }

    .badge-region, .badge-general {
      flex-shrink: 0; display: inline-flex; align-items: center; gap: 6px;
      font-size: 11.5px; font-weight: 600; padding: 4px 11px; border-radius: 16px; white-space: nowrap;
    }
    .badge-region { background: color-mix(in srgb, var(--chip-color, #c8a250) 15%, white); color: #1a2744; }
    .badge-general { background: rgba(16,28,54,0.06); color: #4a5568; }

    .evento-card p { margin: 8px 0 0 0; font-size: 14px; color: #4a5568; line-height: 1.6; }
    .evento-rango { display: inline-block; margin-top: 8px; font-size: 12px; color: #8994a8; }

    .vacio { text-align: center; padding: 60px 20px; color: #8994a8; }
    .vacio i { font-size: 32px; color: #c8a250; margin-bottom: 12px; display: block; }

    .cta-inscripcion {
      margin-top: 50px; text-align: center; padding: 30px; border-radius: 16px;
      background: rgba(200,162,80,0.08); border: 1px solid rgba(200,162,80,0.2);
    }
    .cta-inscripcion p { margin: 0 0 14px 0; font-weight: 600; color: #1a2744; }

    @media (max-width: 560px) {
      .dia-bloque { gap: 12px; }
      .dia-fecha { flex-basis: 52px; padding: 10px 4px; }
      .evento-cabecera { flex-direction: column; }
    }
  `,
})
export class PublicCalendarComponent {
  private readonly api = inject(ApiClient);

  private static readonly PALETTE = ['#c8a250', '#4a7c8c', '#8c4a6a', '#4a8c5f', '#8c6a4a', '#5a4a8c', '#8c4a4a', '#4a648c'];

  protected readonly events = toSignal(
    this.api.get<CalendarEventDto[]>('/calendar').pipe(catchError(() => of<CalendarEventDto[]>([]))),
    { initialValue: [] as CalendarEventDto[] },
  );

  protected readonly regionesActivas = computed(() => {
    const nombres = new Set<string>();
    for (const evento of this.events()) {
      if (evento.regionName) nombres.add(evento.regionName);
    }
    return [...nombres].sort();
  });

  protected readonly dias = computed<CalendarDay[]>(() => {
    const porDia = new Map<string, CalendarEventDto[]>();
    for (const evento of this.events()) {
      const key = evento.startAtUtc.slice(0, 10);
      porDia.set(key, [...(porDia.get(key) ?? []), evento]);
    }

    return [...porDia.entries()]
      .sort(([a], [b]) => a.localeCompare(b))
      .map(([key, events]) => {
        const fecha = new Date(`${key}T00:00:00Z`);
        return {
          key,
          dayNumber: fecha.getUTCDate().toString().padStart(2, '0'),
          monthLabel: fecha.toLocaleDateString('es-MX', { month: 'short', timeZone: 'UTC' }).replace('.', ''),
          weekdayLabel: fecha.toLocaleDateString('es-MX', { weekday: 'short', timeZone: 'UTC' }).replace('.', ''),
          events,
        };
      });
  });

  protected colorFor(regionName: string): string {
    let hash = 0;
    for (let i = 0; i < regionName.length; i++) {
      hash = (hash * 31 + regionName.charCodeAt(i)) >>> 0;
    }
    return PublicCalendarComponent.PALETTE[hash % PublicCalendarComponent.PALETTE.length] ?? '#c8a250';
  }

  protected formatShort(isoDate: string): string {
    return new Date(isoDate).toLocaleDateString('es-MX', { day: '2-digit', month: 'short', timeZone: 'UTC' });
  }
}
