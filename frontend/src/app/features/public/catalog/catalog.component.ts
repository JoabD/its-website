import { AfterViewInit, Component, ElementRef, HostListener, OnDestroy, computed, inject, signal, viewChildren } from '@angular/core';
import { RouterLink } from '@angular/router';
import { toSignal } from '@angular/core/rxjs-interop';
import { of } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { ApiClient } from '../../../core/http/api-client';
import { CurriculumSubjectDto } from '../../../api/schema';

interface Grupo {
  numero: number;
  titulo: string;
  materias: CurriculumSubjectDto[];
}

/**
 * Catálogo de Materias portado 1:1 desde resources/views/programas.blade.php +
 * public/css/pages/programas.css del proyecto Laravel original (C:\laragon\www\shekinah).
 * Mismas secciones/ids/clases (#hero, #salto-rapido con chips + scroll-spy, .nota-info,
 * .grupo-materias/.materia-item con estado "Próximamente", #diplomado). A diferencia de planes.blade.php
 * (contenido estático), esta página sí consume el catálogo real de materias del backend
 * (/catalog/curriculum), agrupado por cuatrimestre tal como lo hacía el controlador de Laravel.
 */
@Component({
  selector: 'shk-catalog',
  imports: [RouterLink],
  template: `
    <section id="hero">
      <div class="contenedor">
        <div class="hero-contenido">
          <span class="eyebrow">Catálogo de Materias</span>
          <h1>Programas de estudio por materia</h1>
          <p>
            Consulta el contenido de cada materia de la retícula y descarga su programa de estudio en
            PDF, organizado cuatrimestre por cuatrimestre.
          </p>
          <div class="hero-botones">
            <a href="#materias" class="btn btn-primario"><i class="bi bi-collection"></i> Ver materias</a>
            <a routerLink="/planes" class="btn btn-secundario"><i class="bi bi-diagram-3"></i> Ver planes de estudio</a>
          </div>
        </div>
      </div>
    </section>

    <nav id="salto-rapido" aria-label="Navegación rápida por cuatrimestre">
      <div class="contenedor">
        <div class="salto-lista">
          @for (grupo of grupos(); track grupo.numero) {
            <a
              [href]="'#cuatrimestre-' + grupo.numero"
              class="chip-salto"
              [class.activo]="seccionActiva() === 'cuatrimestre-' + grupo.numero"
            >
              {{ grupo.titulo }}
            </a>
          }
          <a href="#diplomado" class="chip-salto" [class.activo]="seccionActiva() === 'diplomado'">Diplomado</a>
        </div>
      </div>
    </nav>

    <section class="seccion" id="materias">
      <div class="contenedor">
        <div class="seccion-cabecera">
          <span class="eyebrow">Retícula completa</span>
          <h2>{{ subjects().length || 28 }} materias, organizadas por cuatrimestre</h2>
          <p>Cada materia incluye su programa de estudio descargable en PDF con los temas, objetivos y bibliografía del curso.</p>
        </div>

        <div class="nota-info">
          <i class="bi bi-info-circle"></i>
          <p>
            Estamos subiendo los programas de estudio en PDF de cada materia. Las materias marcadas
            como <strong>"Próximamente"</strong> estarán disponibles para descarga muy pronto.
          </p>
        </div>

        @for (grupo of grupos(); track grupo.numero) {
          <div class="grupo-materias" [id]="'cuatrimestre-' + grupo.numero" #seccion>
            <div class="grupo-cabecera">
              <div class="grupo-numero">{{ grupo.numero }}</div>
              <div class="grupo-titulo">
                <h3>{{ grupo.titulo }}</h3>
                <span>{{ grupo.materias.length }} materias</span>
              </div>
            </div>
            <div class="lista-materias">
              @for (materia of grupo.materias; track materia.id) {
                <div class="materia-item">
                  <div class="materia-info">
                    <i class="bi bi-file-earmark-text"></i>
                    <span class="materia-nombre">{{ materia.name }}</span>
                  </div>
                  @if (materia.hasSyllabus) {
                    <a [href]="materia.syllabusPdfUrl" class="materia-descarga" target="_blank" rel="noopener">
                      <i class="bi bi-download"></i> PDF
                    </a>
                  } @else {
                    <span class="materia-descarga pendiente"><i class="bi bi-clock"></i> Próximamente</span>
                  }
                </div>
              }
            </div>
          </div>
        }

        <div id="diplomado" #seccion>
          <div class="grupo-cabecera">
            <div class="grupo-numero"><i class="bi bi-mortarboard"></i></div>
            <div class="grupo-titulo">
              <h3>Diplomado en Formación Ministerial</h3>
              <span>20 semanas &middot; {{ diplomadoMaterias().length }} materias</span>
            </div>
          </div>
          <div class="lista-materias">
            @for (materia of diplomadoMaterias(); track materia.id) {
              <div class="materia-item">
                <div class="materia-info">
                  <i class="bi bi-file-earmark-text"></i>
                  <span class="materia-nombre">{{ materia.name }}</span>
                </div>
                @if (materia.hasSyllabus) {
                  <a [href]="materia.syllabusPdfUrl" class="materia-descarga" target="_blank" rel="noopener">
                    <i class="bi bi-download"></i> PDF
                  </a>
                } @else {
                  <span class="materia-descarga pendiente"><i class="bi bi-clock"></i> Próximamente</span>
                }
              </div>
            }
          </div>
        </div>
      </div>
    </section>

    <section id="cta-final">
      <div class="contenedor">
        <div class="cta-caja">
          <div>
            <h2>¿Listo para comenzar tu formación bíblica?</h2>
            <p>Inscríbete hoy y da el siguiente paso en tu preparación ministerial.</p>
          </div>
          <a routerLink="/inscripcion" class="btn btn-primario"><i class="bi bi-journal-check"></i> Inscríbete ahora</a>
        </div>
      </div>
    </section>
  `,
  styles: `
    /* ===== HERO ===== */
    #hero {
      min-height: 52vh;
      padding: 70px 0;
      text-align: center;
      background: linear-gradient(120deg, rgba(16, 28, 54, 0.92), rgba(26, 39, 68, 0.85)), url('/img/instituto-6.jpg');
    }

    #hero::before { background: radial-gradient(circle at 85% 15%, rgba(200, 162, 80, 0.22), transparent 45%); }

    .hero-contenido { max-width: 720px; margin: 0 auto; }
    .hero-contenido h1 { font-size: clamp(30px, 4.5vw, 46px); }
    .hero-contenido p { font-size: 17px; margin: 0 auto 34px auto; max-width: 580px; }
    .hero-botones { justify-content: center; }

    /* ===== NAVEGACIÓN RÁPIDA (chips por cuatrimestre) ===== */
    #salto-rapido {
      position: sticky;
      top: var(--shk-header-h, 75px);
      z-index: 900;
      background: rgba(245, 246, 249, 0.94);
      backdrop-filter: blur(6px);
      border-bottom: 1px solid rgba(16, 28, 54, 0.06);
      padding: 16px 0;
    }

    .salto-lista { display: flex; align-items: center; gap: 10px; overflow-x: auto; scrollbar-width: none; padding: 2px; }
    .salto-lista::-webkit-scrollbar { display: none; }

    .chip-salto {
      flex: 0 0 auto;
      font-family: "Poppins", sans-serif;
      font-size: 13px;
      font-weight: 600;
      color: #1a2744;
      background: #ffffff;
      border: 1px solid rgba(16, 28, 54, 0.1);
      padding: 9px 18px;
      border-radius: 30px;
      white-space: nowrap;
      transition: all 0.3s ease;
    }

    .chip-salto:hover { border-color: #c8a250; color: #c8a250; }
    .chip-salto.activo { background: #1a2744; border-color: #1a2744; color: #ffffff; }

    /* ===== AJUSTE DE SECCIONES PARA ESTA PÁGINA ===== */
    .seccion { padding: 80px 0; }
    .seccion-cabecera { margin: 0 auto 46px auto; }

    /* ===== GRUPOS DE MATERIAS POR CUATRIMESTRE ===== */
    .grupo-materias {
      scroll-margin-top: 150px;
      background: #ffffff;
      border-radius: 16px;
      box-shadow: 0 10px 30px rgba(16, 28, 54, 0.08);
      padding: 34px 34px 20px 34px;
      margin-bottom: 26px;
    }

    .grupo-cabecera {
      display: flex;
      align-items: center;
      gap: 16px;
      margin-bottom: 22px;
      padding-bottom: 22px;
      border-bottom: 1px solid rgba(16, 28, 54, 0.08);
    }

    .grupo-numero {
      width: 46px;
      height: 46px;
      min-width: 46px;
      border-radius: 50%;
      background: linear-gradient(135deg, #1a2744, #101c36);
      color: #e8d5a3;
      display: flex;
      align-items: center;
      justify-content: center;
      font-family: "Poppins", sans-serif;
      font-weight: 700;
      font-size: 15px;
    }

    .grupo-titulo h3 { font-size: 19px; margin-bottom: 2px; }
    .grupo-titulo span { font-size: 13.5px; color: #8994a8; }

    .lista-materias { display: flex; flex-direction: column; gap: 10px; padding-bottom: 14px; }

    .materia-item {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: 16px;
      padding: 14px 18px;
      background: #f5f6f9;
      border-radius: 10px;
      transition: all 0.3s ease;
    }

    .materia-item:hover { background: rgba(200, 162, 80, 0.1); }

    .materia-info { display: flex; align-items: center; gap: 12px; min-width: 0; }
    .materia-info i { color: #c8a250; font-size: 16px; flex-shrink: 0; }

    .materia-nombre {
      font-family: "Poppins", sans-serif;
      font-size: 14.5px;
      font-weight: 500;
      color: #1a2744;
      overflow-wrap: anywhere;
    }

    .materia-descarga {
      flex-shrink: 0;
      display: inline-flex;
      align-items: center;
      gap: 6px;
      font-family: "Poppins", sans-serif;
      font-weight: 600;
      font-size: 12.5px;
      color: #101c36;
      background: #c8a250;
      padding: 9px 16px;
      border-radius: 20px;
      transition: all 0.3s ease;
    }

    .materia-descarga:hover { background: #b28d3f; transform: translateY(-2px); }

    .materia-descarga.pendiente {
      background: transparent;
      border: 1.5px dashed rgba(16, 28, 54, 0.22);
      color: #8994a8;
      cursor: default;
      pointer-events: none;
    }

    /* ===== DIPLOMADO (bloque destacado) ===== */
    #diplomado {
      scroll-margin-top: 150px;
      position: relative;
      background: linear-gradient(120deg, #101c36, #1a2744);
      border-radius: 16px;
      padding: 40px 34px;
      overflow: hidden;
    }

    #diplomado::before {
      content: "";
      position: absolute;
      top: -120px;
      right: -120px;
      width: 320px;
      height: 320px;
      border-radius: 50%;
      background: radial-gradient(circle, rgba(200, 162, 80, 0.2), transparent 65%);
      pointer-events: none;
    }

    #diplomado .grupo-cabecera { position: relative; z-index: 2; border-bottom-color: rgba(255, 255, 255, 0.12); }
    #diplomado .grupo-titulo h3 { color: #ffffff; }
    #diplomado .grupo-titulo span { color: #e8d5a3; }
    #diplomado .lista-materias { position: relative; z-index: 2; }
    #diplomado .materia-item { background: rgba(255, 255, 255, 0.07); }
    #diplomado .materia-item:hover { background: rgba(255, 255, 255, 0.13); }
    #diplomado .materia-nombre { color: #ffffff; }
    #diplomado .materia-descarga.pendiente { border-color: rgba(255, 255, 255, 0.3); color: rgba(255, 255, 255, 0.65); }

    /* ===== NOTA INFORMATIVA ===== */
    .nota-info {
      display: flex;
      align-items: flex-start;
      gap: 14px;
      background: rgba(200, 162, 80, 0.1);
      border: 1px solid rgba(200, 162, 80, 0.25);
      border-radius: 14px;
      padding: 20px 24px;
      margin-bottom: 40px;
    }

    .nota-info i { color: #c8a250; font-size: 20px; margin-top: 2px; }
    .nota-info p { margin: 0; font-size: 14.5px; line-height: 1.6; color: #4a5568; }

    /* ===== RESPONSIVE — Catálogo de Materias ===== */
    @media (max-width: 860px) {
      #hero { min-height: auto; padding: 130px 0 70px 0; }
      .grupo-materias,
      #diplomado { padding: 26px 20px 16px 20px; }
      .materia-item { flex-wrap: wrap; }
    }

    @media (max-width: 640px) {
      .seccion { padding: 56px 0; }
      .grupo-cabecera { gap: 12px; }
    }
  `,
})
export class CatalogComponent implements AfterViewInit, OnDestroy {
  private readonly api = inject(ApiClient);
  private readonly secciones = viewChildren<ElementRef<HTMLElement>>('seccion');

  protected readonly subjects = toSignal(
    this.api.get<CurriculumSubjectDto[]>('/catalog/curriculum').pipe(catchError(() => of<CurriculumSubjectDto[]>([]))),
    { initialValue: [] as CurriculumSubjectDto[] },
  );

  protected readonly grupos = computed<Grupo[]>(() => {
    const porTermino = new Map<number, CurriculumSubjectDto[]>();
    for (const materia of this.subjects()) {
      if (materia.programType === 'Quarterly' && materia.termNumber !== null) {
        porTermino.set(materia.termNumber, [...(porTermino.get(materia.termNumber) ?? []), materia]);
      }
    }
    const titulos = ['Primer', 'Segundo', 'Tercer', 'Cuarto', 'Quinto', 'Sexto'];
    return [1, 2, 3, 4, 5, 6]
      .filter((numero) => porTermino.has(numero))
      .map((numero) => ({
        numero,
        titulo: `${titulos[numero - 1]} Cuatrimestre`,
        materias: porTermino.get(numero) ?? [],
      }));
  });

  protected readonly diplomadoMaterias = computed(() => this.subjects().filter((s) => s.programType === 'Diploma'));

  protected readonly seccionActiva = signal<string>('');

  ngAfterViewInit(): void {
    // Evalúa la sección visible una vez montado el DOM (equivalente al scroll-spy de programas.js).
    queueMicrotask(() => this.actualizarSeccionActiva());
  }

  ngOnDestroy(): void {}

  @HostListener('window:scroll')
  protected actualizarSeccionActiva(): void {
    const secciones = this.secciones();
    if (!secciones.length) return;
    let activa = '';
    for (const seccion of secciones) {
      const rect = seccion.nativeElement.getBoundingClientRect();
      if (rect.top <= 170) {
        activa = seccion.nativeElement.id;
      }
    }
    this.seccionActiva.set(activa || secciones[0]?.nativeElement.id || '');
  }
}
