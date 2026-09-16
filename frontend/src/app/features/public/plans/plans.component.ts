import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';

/**
 * Planes de Estudio portada 1:1 desde resources/views/planes.blade.php + public/css/pages/planes.css
 * del proyecto Laravel original (C:\laragon\www\shekinah). Mismas secciones/ids/clases (#hero,
 * #stats/.stats-grid, #malla/.planes-grid/.plan-card, #diplomado/.diplomado-lista) y los mismos
 * textos de la malla curricular (contenido estático en el Blade original, no viene de una API).
 * El link "Descargar programas por materia" lleva al catálogo real (/programas), que sí consume
 * el catálogo de materias del backend.
 */
@Component({
  selector: 'shk-plans',
  imports: [RouterLink],
  template: `
    <section id="hero">
      <div class="contenedor">
        <div class="hero-contenido">
          <span class="eyebrow">Programa Académico</span>
          <h1>Planes de Estudio</h1>
          <p>
            Un recorrido formativo de seis cuatrimestres y un diplomado final, diseñado para preparar
            siervos fieles, capacitados y comprometidos con la obra del Señor.
          </p>
          <div class="hero-botones">
            <a routerLink="/inscripcion" class="btn btn-primario"><i class="bi bi-journal-check"></i> Inscríbete ahora</a>
            <a href="#malla" class="btn btn-secundario"><i class="bi bi-diagram-3"></i> Ver mapa curricular</a>
          </div>
        </div>
      </div>
    </section>

    <section id="stats">
      <div class="contenedor">
        <div class="stats-grid">
          <div class="stat-card">
            <div class="stat-icono"><i class="bi bi-calendar3"></i></div>
            <h3>6 Cuatrimestres</h3>
            <p>Formación teológica progresiva, organizada por niveles.</p>
          </div>
          <div class="stat-card">
            <div class="stat-icono"><i class="bi bi-mortarboard"></i></div>
            <h3>1 Diplomado</h3>
            <p>Etapa final de especialización y práctica ministerial.</p>
          </div>
          <div class="stat-card">
            <div class="stat-icono"><i class="bi bi-clock-history"></i></div>
            <h3>20 Semanas</h3>
            <p>Duración del diplomado que cierra el programa.</p>
          </div>
          <div class="stat-card">
            <div class="stat-icono"><i class="bi bi-book"></i></div>
            <h3>28 Materias</h3>
            <p>Áreas de estudio bíblico, teológico y ministerial.</p>
          </div>
        </div>
      </div>
    </section>

    <section class="seccion" id="malla">
      <div class="contenedor">
        <div class="seccion-cabecera">
          <span class="eyebrow">Mapa Curricular</span>
          <h2>Un camino formativo paso a paso</h2>
          <p>
            Cada cuatrimestre construye sobre el anterior, combinando fundamentos bíblicos, teología
            sistemática y preparación para el servicio.
          </p>
          <p style="margin-top: 22px;">
            <a
              routerLink="/programas"
              class="btn btn-secundario"
              style="color:#1a2744;border-color:rgba(26,39,68,0.25);display:inline-flex;"
            >
              <i class="bi bi-file-earmark-arrow-down"></i> Descargar programas por materia
            </a>
          </p>
        </div>

        <div class="planes-grid">
          @for (cuatrimestre of cuatrimestres; track cuatrimestre.numero) {
            <div class="plan-card">
              <div class="plan-numero">{{ cuatrimestre.numero }}</div>
              <h3>{{ cuatrimestre.titulo }}</h3>
              <ul class="lista-check">
                @for (materia of cuatrimestre.materias; track materia) {
                  <li><i class="bi bi-check-lg"></i> {{ materia }}.</li>
                }
              </ul>
            </div>
          }
        </div>
      </div>
    </section>

    <section id="diplomado">
      <div class="contenedor">
        <div class="diplomado-cabecera">
          <span class="eyebrow">Etapa final</span>
          <h2>Diplomado en Formación Ministerial</h2>
          <p>
            La culminación del programa: veinte semanas de profundización teológica y práctica
            ministerial para consolidar el llamado al servicio.
          </p>
        </div>

        <div class="diplomado-caja">
          <div class="pill"><i class="bi bi-clock-history"></i> Duración: 20 semanas</div>
          <ul class="diplomado-lista">
            @for (materia of diplomadoMaterias; track materia) {
              <li><i class="bi bi-check-lg"></i> {{ materia }}.</li>
            }
          </ul>
          <div class="diplomado-caja-pie">
            <a routerLink="/inscripcion" class="btn btn-primario">
              <i class="bi bi-journal-check"></i> Inscríbete al Diplomado
            </a>
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
      min-height: 58vh;
      padding: 70px 0;
      text-align: center;
      background: linear-gradient(120deg, rgba(16, 28, 54, 0.92), rgba(26, 39, 68, 0.85)), url('/img/instituto-3.jpg');
    }

    #hero::before { background: radial-gradient(circle at 15% 20%, rgba(200, 162, 80, 0.22), transparent 45%); }

    .hero-contenido { max-width: 720px; margin: 0 auto; }
    .hero-contenido h1 { font-size: clamp(30px, 4.5vw, 46px); }
    .hero-contenido p { font-size: 17px; margin: 0 auto 34px auto; max-width: 560px; }
    .hero-botones { justify-content: center; }

    /* ===== STATS (resumen del programa) ===== */
    #stats { margin-top: -60px; position: relative; z-index: 5; }

    .stats-grid { display: grid; grid-template-columns: repeat(4, 1fr); gap: 22px; }

    .stat-card {
      background: #ffffff;
      border-radius: 14px;
      padding: 28px 24px;
      box-shadow: 0 10px 30px rgba(16, 28, 54, 0.08);
      text-align: left;
      transition: all 0.3s ease;
    }

    .stat-card:hover { transform: translateY(-6px); box-shadow: 0 20px 45px rgba(16, 28, 54, 0.16); }

    .stat-icono {
      width: 52px;
      height: 52px;
      border-radius: 14px;
      background: linear-gradient(135deg, #1a2744, #101c36);
      color: #e8d5a3;
      display: flex;
      align-items: center;
      justify-content: center;
      font-size: 22px;
      margin-bottom: 18px;
    }

    .stat-card h3 { font-size: 22px; margin-bottom: 6px; }
    .stat-card p { font-size: 14px; line-height: 1.6; margin: 0; }

    /* ===== MAPA CURRICULAR ===== */
    .planes-grid { display: grid; grid-template-columns: repeat(3, 1fr); gap: 24px; }

    .plan-card {
      background: #ffffff;
      border-radius: 14px;
      padding: 32px 28px;
      box-shadow: 0 10px 30px rgba(16, 28, 54, 0.08);
      border-top: 3px solid transparent;
      transition: all 0.3s ease;
    }

    .plan-card:hover { transform: translateY(-6px); border-top-color: #c8a250; box-shadow: 0 20px 45px rgba(16, 28, 54, 0.16); }

    .plan-numero {
      width: 42px;
      height: 42px;
      border-radius: 50%;
      background: linear-gradient(135deg, #1a2744, #101c36);
      color: #e8d5a3;
      display: flex;
      align-items: center;
      justify-content: center;
      font-family: "Poppins", sans-serif;
      font-weight: 700;
      font-size: 14px;
      margin-bottom: 18px;
    }

    .plan-card h3 { font-size: 18px; margin-bottom: 18px; }

    .lista-check { display: flex; flex-direction: column; gap: 12px; }

    .lista-check li {
      display: flex;
      align-items: flex-start;
      gap: 12px;
      font-size: 14.5px;
      line-height: 1.5;
      color: #4a5568;
    }

    .lista-check i {
      color: #c8a250;
      background: rgba(200, 162, 80, 0.12);
      border-radius: 50%;
      width: 24px;
      height: 24px;
      min-width: 24px;
      display: flex;
      align-items: center;
      justify-content: center;
      font-size: 12px;
      margin-top: 1px;
    }

    /* ===== DIPLOMADO (bloque destacado) ===== */
    #diplomado {
      position: relative;
      background: linear-gradient(120deg, #101c36, #1a2744);
      padding: 90px 0;
      overflow: hidden;
    }

    #diplomado::before {
      content: "";
      position: absolute;
      top: -120px;
      right: -120px;
      width: 360px;
      height: 360px;
      border-radius: 50%;
      background: radial-gradient(circle, rgba(200, 162, 80, 0.18), transparent 65%);
      pointer-events: none;
    }

    #diplomado .contenedor { position: relative; z-index: 2; }

    .diplomado-cabecera { max-width: 620px; margin: 0 auto 40px auto; text-align: center; }
    #diplomado .eyebrow { color: #e8d5a3; }
    #diplomado h2 { color: #ffffff; font-size: clamp(26px, 4vw, 34px); margin-bottom: 16px; }
    #diplomado .diplomado-cabecera p { color: rgba(255, 255, 255, 0.8); font-size: 16px; line-height: 1.7; }

    .pill {
      display: inline-flex;
      align-items: center;
      gap: 8px;
      background: rgba(200, 162, 80, 0.15);
      color: #e8d5a3;
      padding: 8px 18px;
      border-radius: 30px;
      font-family: "Poppins", sans-serif;
      font-size: 13px;
      font-weight: 600;
      margin-bottom: 20px;
    }

    .diplomado-caja {
      background: rgba(255, 255, 255, 0.06);
      border: 1px solid rgba(255, 255, 255, 0.12);
      border-radius: 16px;
      padding: 36px;
    }

    .diplomado-lista { display: grid; grid-template-columns: repeat(2, 1fr); gap: 16px 32px; margin-bottom: 30px; }

    .diplomado-lista li {
      display: flex;
      align-items: flex-start;
      gap: 12px;
      font-size: 15.5px;
      line-height: 1.6;
      color: rgba(255, 255, 255, 0.9);
    }

    .diplomado-lista i {
      color: #101c36;
      background: #c8a250;
      border-radius: 50%;
      width: 24px;
      height: 24px;
      min-width: 24px;
      display: flex;
      align-items: center;
      justify-content: center;
      font-size: 12px;
      margin-top: 2px;
    }

    .diplomado-caja .btn-primario { margin: 0 auto; }
    .diplomado-caja-pie { display: flex; justify-content: center; }

    /* ===== RESPONSIVE — Planes ===== */
    @media (max-width: 1024px) {
      .stats-grid { grid-template-columns: repeat(2, 1fr); }
      .planes-grid { grid-template-columns: repeat(2, 1fr); }
    }

    @media (max-width: 860px) {
      #stats { margin-top: 40px; }
      #hero { min-height: auto; padding: 130px 0 90px 0; }
      .diplomado-lista { grid-template-columns: 1fr; }
      .diplomado-caja { padding: 28px; }
    }

    @media (max-width: 640px) {
      .stats-grid,
      .planes-grid {
        display: flex;
        grid-template-columns: none;
        overflow-x: auto;
        scroll-snap-type: x mandatory;
        -webkit-overflow-scrolling: touch;
        scrollbar-width: none;
        margin: 0 -24px;
        padding: 4px 24px;
      }

      .stats-grid::-webkit-scrollbar,
      .planes-grid::-webkit-scrollbar { display: none; }

      .stat-card,
      .plan-card { flex: 0 0 100%; scroll-snap-align: start; }
    }
  `,
})
export class PlansComponent {
  protected readonly cuatrimestres = [
    { numero: '01', titulo: 'Primer Cuatrimestre', materias: ['Bibliología', 'Introducción a la Teología', 'Pentateuco', 'Historia Eclesiástica'] },
    { numero: '02', titulo: 'Segundo Cuatrimestre', materias: ['Homilética', 'Teología Sistemática II', 'Hermenéutica', 'Evangelios Sinópticos'] },
    { numero: '03', titulo: 'Tercer Cuatrimestre', materias: ['Teología Sistemática', 'Sermón Expositivo', 'Hechos de los Apóstoles', 'Liderazgo'] },
    { numero: '04', titulo: 'Cuarto Cuatrimestre', materias: ['Teología Sistemática IV', 'Escatología', 'Epístolas Paulinas', 'Libros Sapienciales'] },
    { numero: '05', titulo: 'Quinto Cuatrimestre', materias: ['Ejercicios ministeriales', 'Teología Sistemática V', 'Evangelismo', 'Libros Históricos'] },
    { numero: '06', titulo: 'Sexto Cuatrimestre', materias: ['Evangelio de Juan', 'Apologética', 'Consejería pastoral', 'Ética ministerial'] },
  ];

  protected readonly diplomadoMaterias = ['Eclesiología', 'Apocalipsis', 'Neumatología', 'Administración pastoral'];
}
