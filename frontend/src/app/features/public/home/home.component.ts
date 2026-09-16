import { Component, signal } from '@angular/core';
import { RouterLink } from '@angular/router';

/**
 * Página de inicio portada 1:1 desde resources/views/index.blade.php + public/css/pages/home.css
 * del proyecto Laravel original (C:\laragon\www\shekinah). Mismas secciones/ids/clases (#hero,
 * #valores, #conocenos, #mision, #ofrecemos, #dirigido-a, #galeria, #cta-final), mismos textos,
 * mismos íconos Bootstrap Icons y las mismas imágenes reales del instituto. El carrusel de la
 * galería replica el crossfade + flechas + puntos de home.js.
 */
@Component({
  selector: 'shk-home',
  imports: [RouterLink],
  template: `
    <section id="hero">
      <div class="contenedor">
        <div class="hero-contenido">
          <span class="eyebrow">Instituto Shekinah</span>
          <h1>Formación bíblica sólida para siervos comprometidos con la obra de Dios</h1>
          <p>
            Capacitamos y formamos líderes fieles en el estudio de las Sagradas Escrituras, manteniendo
            una sana doctrina y un corazón dispuesto al servicio.
          </p>
          <div class="hero-botones">
            <a routerLink="/inscripcion" class="btn btn-primario"><i class="bi bi-journal-check"></i> Inscríbete ahora</a>
            <a routerLink="/planes" class="btn btn-secundario"><i class="bi bi-book"></i> Ver planes de estudio</a>
          </div>
        </div>
      </div>
    </section>

    <section id="valores">
      <div class="contenedor">
        <div class="valores-grid">
          <div class="valor-card">
            <div class="valor-icono"><i class="bi bi-book-half"></i></div>
            <h3>Formación estructurada</h3>
            <p>Programas teológicos organizados por niveles, fieles a la Palabra de Dios.</p>
          </div>
          <div class="valor-card">
            <div class="valor-icono"><i class="bi bi-people"></i></div>
            <h3>Desarrollo ministerial</h3>
            <p>Fortalecemos habilidades para el servicio y el liderazgo en la iglesia.</p>
          </div>
          <div class="valor-card">
            <div class="valor-icono"><i class="bi bi-hand-thumbs-up"></i></div>
            <h3>Preparación práctica</h3>
            <p>Formación orientada a la obra y el servicio real dentro de la congregación.</p>
          </div>
          <div class="valor-card">
            <div class="valor-icono"><i class="bi bi-heart"></i></div>
            <h3>Acompañamiento espiritual</h3>
            <p>Un seguimiento cercano y continuo durante todo el proceso formativo.</p>
          </div>
        </div>
      </div>
    </section>

    <section class="seccion seccion-alt" id="conocenos">
      <div class="contenedor">
        <div class="bloque-imagen">
          <figure>
            <img src="/img/instituto-1.jpg" alt="Instalaciones del Instituto Shekina" />
          </figure>
          <div class="bloque-texto">
            <span class="eyebrow">Conócenos</span>
            <h2>Un instituto comprometido con la sana doctrina</h2>
            <p>
              Somos un instituto dedicado a la formación y capacitación bíblica y ministerial. Creemos
              en la enseñanza sólida de la Palabra de Dios y en el desarrollo espiritual de todo
              creyente.
            </p>
            <p>
              Desde nuestra fundación, nuestro objetivo ha sido formar siervos comprometidos con la obra
              del Señor y con un corazón dispuesto al servicio.
            </p>
          </div>
        </div>
      </div>
    </section>

    <section id="mision">
      <div class="contenedor">
        <span class="eyebrow">Nuestra misión</span>
        <h2>Formar líderes fieles al servicio de Dios</h2>
        <p>
          Capacitar y formar líderes fieles siervos de Dios, en el estudio de las Sagradas Escrituras y
          mantener una sana doctrina.
        </p>
      </div>
    </section>

    <section class="seccion" id="ofrecemos">
      <div class="contenedor">
        <div class="bloque-imagen invertido">
          <figure>
            <img src="/img/instituto-2.jpg" alt="Clases del Instituto Shekina" />
          </figure>
          <div class="bloque-texto">
            <span class="eyebrow">Ofrecemos</span>
            <h2>Una formación sólida, bíblica y ministerial</h2>
            <p>
              En nuestro instituto brindamos una formación sólida, basada en la Biblia y orientada al
              crecimiento espiritual, doctrinal y ministerial.
            </p>
            <ul class="lista-check">
              <li><i class="bi bi-check-lg"></i> Formación teológica estructurada.</li>
              <li><i class="bi bi-check-lg"></i> Desarrollo de habilidades ministeriales.</li>
              <li><i class="bi bi-check-lg"></i> Preparación para la obra y el servicio en la iglesia.</li>
              <li><i class="bi bi-check-lg"></i> Acompañamiento espiritual continuo.</li>
            </ul>
          </div>
        </div>
      </div>
    </section>

    <section class="seccion seccion-alt" id="dirigido-a">
      <div class="contenedor">
        <div class="seccion-cabecera">
          <span class="eyebrow">Dirigido a</span>
          <h2>¿Para quién es este instituto?</h2>
        </div>
        <div class="dirigido-grid">
          <div class="dirigido-card">
            <i class="bi bi-person-check"></i>
            <p>Siervos de Dios que aún no han cursado el Instituto.</p>
          </div>
          <div class="dirigido-card">
            <i class="bi bi-briefcase"></i>
            <p>Ministerios de oficio interesados en formación continua.</p>
          </div>
          <div class="dirigido-card">
            <i class="bi bi-people-fill"></i>
            <p>Ayudas, gobernaciones y la iglesia en general.</p>
          </div>
          <div class="dirigido-card">
            <i class="bi bi-geo-alt"></i>
            <p>Zonas donde no se imparte enseñanza presencial.</p>
          </div>
        </div>
      </div>
    </section>

    <section class="seccion" id="galeria">
      <div class="contenedor">
        <div class="seccion-cabecera">
          <span class="eyebrow">Galería</span>
          <h2>Un vistazo a nuestra comunidad</h2>
        </div>
        <div class="carrusel">
          <span class="flecha izq" role="button" aria-label="Anterior" (click)="prevSlide()">&#10094;</span>
          <span class="flecha der" role="button" aria-label="Siguiente" (click)="nextSlide()">&#10095;</span>

          @for (image of galleryImages; track image; let i = $index) {
            <img [src]="image" [class.activa]="activeSlide() === i" [alt]="'Galería Instituto Shekina ' + (i + 1)" />
          }

          <div class="carrusel-puntos">
            @for (image of galleryImages; track image; let i = $index) {
              <span class="punto" [class.activo]="activeSlide() === i" (click)="goToSlide(i)"></span>
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
      min-height: 88vh;
      padding: 60px 0;
      background: linear-gradient(120deg, rgba(16, 28, 54, 0.92), rgba(26, 39, 68, 0.82)), url('/img/instituto-1.jpg');
    }

    #hero::before {
      background: radial-gradient(circle at 85% 20%, rgba(200, 162, 80, 0.25), transparent 45%);
    }

    .hero-contenido { max-width: 680px; }
    .hero-contenido h1 { font-size: clamp(32px, 5vw, 52px); }
    .hero-contenido p { font-size: 18px; max-width: 560px; }

    /* ===== VALORES / FEATURES ===== */
    #valores { margin-top: -70px; position: relative; z-index: 5; }

    .valores-grid { display: grid; grid-template-columns: repeat(4, 1fr); gap: 22px; }

    .valor-card {
      background: #ffffff;
      border-radius: 14px;
      padding: 30px 24px;
      box-shadow: 0 10px 30px rgba(16, 28, 54, 0.08);
      text-align: left;
      transition: all 0.3s ease;
    }

    .valor-card:hover { transform: translateY(-6px); box-shadow: 0 20px 45px rgba(16, 28, 54, 0.16); }

    .valor-icono {
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

    .valor-card h3 { font-size: 17px; margin-bottom: 8px; }
    .valor-card p { font-size: 14.5px; line-height: 1.6; margin: 0; }

    /* ===== CONÓCENOS / OFRECEMOS (bloques con imagen) ===== */
    .bloque-imagen { display: grid; grid-template-columns: 1fr 1fr; gap: 60px; align-items: center; }
    .bloque-imagen.invertido { direction: rtl; }
    .bloque-imagen.invertido > * { direction: ltr; }

    .bloque-imagen figure {
      margin: 0;
      border-radius: 14px;
      overflow: hidden;
      box-shadow: 0 20px 45px rgba(16, 28, 54, 0.16);
      position: relative;
    }

    .bloque-imagen figure::after {
      content: "";
      position: absolute;
      inset: 0;
      border: 6px solid rgba(255, 255, 255, 0.5);
      border-radius: 14px;
    }

    .bloque-imagen img { width: 100%; height: 420px; object-fit: cover; }

    .bloque-texto h2 { font-size: clamp(24px, 3.5vw, 32px); margin-bottom: 18px; }
    .bloque-texto p { font-size: 16px; line-height: 1.75; margin-bottom: 16px; }

    .lista-check { margin-top: 22px; display: flex; flex-direction: column; gap: 14px; }

    .lista-check li {
      display: flex;
      align-items: flex-start;
      gap: 12px;
      font-size: 15.5px;
      line-height: 1.6;
      color: #4a5568;
    }

    .lista-check i {
      color: #c8a250;
      background: rgba(200, 162, 80, 0.12);
      border-radius: 50%;
      width: 26px;
      height: 26px;
      min-width: 26px;
      display: flex;
      align-items: center;
      justify-content: center;
      font-size: 13px;
      margin-top: 2px;
    }

    /* ===== MISIÓN ===== */
    #mision {
      position: relative;
      background: linear-gradient(120deg, #101c36, #1a2744);
      padding: 90px 0;
      text-align: center;
      overflow: hidden;
    }

    #mision::before {
      content: "\\201C";
      position: absolute;
      top: -40px;
      left: 50%;
      transform: translateX(-50%);
      font-family: Georgia, serif;
      font-size: 220px;
      color: rgba(255, 255, 255, 0.05);
      line-height: 1;
    }

    #mision .contenedor { position: relative; z-index: 2; max-width: 760px; }
    #mision .eyebrow { color: #e8d5a3; }
    #mision h2 { color: #ffffff; font-size: clamp(26px, 4vw, 34px); margin-bottom: 18px; }
    #mision p { color: rgba(255, 255, 255, 0.85); font-size: 18px; line-height: 1.8; }

    /* ===== DIRIGIDO A ===== */
    .dirigido-grid { display: grid; grid-template-columns: repeat(4, 1fr); gap: 22px; }

    .dirigido-card {
      background: #ffffff;
      border-radius: 14px;
      padding: 32px 22px;
      text-align: center;
      box-shadow: 0 10px 30px rgba(16, 28, 54, 0.08);
      transition: all 0.3s ease;
      border-top: 3px solid transparent;
    }

    .dirigido-card:hover { transform: translateY(-6px); border-top-color: #c8a250; box-shadow: 0 20px 45px rgba(16, 28, 54, 0.16); }

    .dirigido-card i { font-size: 26px; color: #c8a250; margin-bottom: 14px; display: inline-block; }
    .dirigido-card p { font-size: 14.5px; line-height: 1.6; color: #4a5568; margin: 0; }

    /* ===== GALERÍA / CARRUSEL ===== */
    .carrusel {
      position: relative;
      border-radius: 14px;
      overflow: hidden;
      box-shadow: 0 20px 45px rgba(16, 28, 54, 0.16);
      aspect-ratio: 16 / 7;
      background: #1a2744;
    }

    .carrusel img {
      position: absolute;
      inset: 0;
      width: 100%;
      height: 100%;
      object-fit: cover;
      opacity: 0;
      transition: opacity 0.6s ease;
    }

    .carrusel img.activa { opacity: 1; position: relative; }

    .carrusel .flecha {
      position: absolute;
      top: 50%;
      transform: translateY(-50%);
      color: #101c36;
      font-size: 18px;
      background: rgba(255, 255, 255, 0.85);
      width: 44px;
      height: 44px;
      border-radius: 50%;
      display: flex;
      align-items: center;
      justify-content: center;
      cursor: pointer;
      user-select: none;
      z-index: 3;
      transition: all 0.3s ease;
    }

    .carrusel .flecha:hover { background: #c8a250; color: #ffffff; }
    .flecha.izq { left: 16px; }
    .flecha.der { right: 16px; }

    .carrusel-puntos {
      position: absolute;
      bottom: 18px;
      left: 50%;
      transform: translateX(-50%);
      display: flex;
      gap: 8px;
      z-index: 3;
    }

    .punto {
      width: 9px;
      height: 9px;
      border-radius: 50%;
      background: rgba(255, 255, 255, 0.55);
      cursor: pointer;
      transition: all 0.3s ease;
    }

    .punto.activo { background: #c8a250; width: 24px; border-radius: 6px; }

    /* ===== RESPONSIVE — Inicio ===== */
    @media (max-width: 1024px) {
      .valores-grid,
      .dirigido-grid { grid-template-columns: repeat(2, 1fr); }
    }

    @media (max-width: 860px) {
      .bloque-imagen,
      .bloque-imagen.invertido { grid-template-columns: 1fr; direction: ltr; gap: 30px; }
      .bloque-imagen img { height: 280px; }
      #valores { margin-top: 40px; }
      #hero { min-height: auto; padding: 130px 0 90px 0; text-align: left; }
    }

    @media (max-width: 640px) {
      .valores-grid,
      .dirigido-grid { grid-template-columns: 1fr; }
    }
  `,
})
export class HomeComponent {
  protected readonly galleryImages = [
    '/img/instituto-5.jpg',
    '/img/instituto-6.jpg',
    '/img/instituto-3.jpg',
    '/img/instituto-4.jpg',
    '/img/instituto-7.jpg',
    '/img/instituto-8.jpg',
  ];

  protected readonly activeSlide = signal(0);

  protected nextSlide(): void {
    this.activeSlide.update((i) => (i + 1) % this.galleryImages.length);
  }

  protected prevSlide(): void {
    this.activeSlide.update((i) => (i - 1 + this.galleryImages.length) % this.galleryImages.length);
  }

  protected goToSlide(index: number): void {
    this.activeSlide.set(index);
  }
}
