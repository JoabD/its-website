import { AfterViewInit, Component, ElementRef, HostListener, OnDestroy, inject, signal, viewChild } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { LoginModalService } from '../../core/auth/login-modal.service';

/**
 * Header + footer portados 1:1 desde resources/views/partials/header.blade.php y
 * footer.blade.php del proyecto Laravel original (C:\laragon\www\shekinah). Mismas
 * clases/ids (#header, #menu, #menu-links, #btn-acceder, #menu-toggle, #foot, .foot-grid...)
 * para reusar tal cual el CSS portado en styles.scss. El toggle móvil y la sombra al hacer
 * scroll replican el comportamiento de site.js.
 */
@Component({
  selector: 'shk-public-shell',
  imports: [RouterLink, RouterLinkActive, RouterOutlet],
  template: `
    <header id="header" #headerEl [class.con-sombra]="scrolled()">
      <nav id="menu">
        <div id="menu-logo">
          <img src="/img/shekina-logo.png" alt="Logo Instituto Shekinah" id="logo-img" />
          <div class="marca">
            Instituto Teológico Shekinah<span>Formación Bíblica y Ministerial</span>
          </div>
        </div>

        <ul id="menu-links" [class.abierto]="menuOpen()">
          <li>
            <a routerLink="/" routerLinkActive="activo" [routerLinkActiveOptions]="{ exact: true }" (click)="closeMenu()">
              Inicio
            </a>
          </li>
          <li>
            <a routerLink="/inscripcion" routerLinkActive="activo" (click)="closeMenu()">Inscripción</a>
          </li>
          <li>
            <a routerLink="/planes" routerLinkActive="activo" (click)="closeMenu()">Planes de Estudio</a>
          </li>
          <li>
            <a routerLink="/programas" routerLinkActive="activo" (click)="closeMenu()">Catálogo de Materias</a>
          </li>
        </ul>

        <div id="menu-acceder">
          <a href="javascript:void(0)" id="btn-acceder" (click)="openLogin()">Acceder</a>
        </div>

        <button
          id="menu-toggle"
          aria-label="Abrir menú"
          [attr.aria-expanded]="menuOpen()"
          (click)="toggleMenu()"
        >
          <span class="icono-hamburguesa"></span>
        </button>
      </nav>
    </header>

    <main>
      <router-outlet />
    </main>

    <footer id="foot">
      <div class="contenedor">
        <div class="foot-grid">
          <div>
            <div class="foot-marca">
              <img src="/img/icap-logo.png" alt="Logo Icap" id="logoi-img" />
              <span>ICAP A.R.</span>
            </div>
            <p style="font-size: 14.5px; line-height: 1.7; max-width: 320px;">
              Instituto dedicado a la formación y capacitación bíblica y ministerial, comprometido
              con la sana doctrina y el servicio a la iglesia.
            </p>
          </div>
          <div>
            <h4>Enlaces</h4>
            <ul>
              <li><a routerLink="/">Inicio</a></li>
              <li><a routerLink="/inscripcion">Inscripción</a></li>
              <li><a routerLink="/planes">Planes de Estudio</a></li>
              <li><a routerLink="/programas">Catálogo de Materias</a></li>
              <li><a href="javascript:void(0)" (click)="openLogin()">Acceder</a></li>
            </ul>
          </div>
          <div>
            <h4>Instituto Teológico Shekinah</h4>
            <ul>
              <li><a routerLink="/" fragment="conocenos">Conócenos</a></li>
              <li><a routerLink="/" fragment="ofrecemos">Ofrecemos</a></li>
              <li><a routerLink="/" fragment="dirigido-a">Dirigido a</a></li>
              <li><a routerLink="/" fragment="galeria">Galería</a></li>
            </ul>
          </div>
        </div>
        <div class="foot-legal">
          <span>&copy; {{ currentYear }} ICAP A.R. — Instituto Teológico Shekinah. Todos los derechos reservados.</span>
        </div>
      </div>
    </footer>
  `,
})
export class PublicShellComponent implements AfterViewInit, OnDestroy {
  private readonly loginModal = inject(LoginModalService);

  protected readonly currentYear = new Date().getFullYear();
  protected readonly menuOpen = signal(false);
  protected readonly scrolled = signal(false);

  private readonly headerEl = viewChild<ElementRef<HTMLElement>>('headerEl');
  private resizeObserver?: ResizeObserver;

  protected toggleMenu(): void {
    this.menuOpen.update((open) => !open);
  }

  protected closeMenu(): void {
    this.menuOpen.set(false);
  }

  protected openLogin(): void {
    this.closeMenu();
    this.loginModal.open();
  }

  @HostListener('window:scroll')
  protected onScroll(): void {
    this.scrolled.set(window.scrollY > 10);
  }

  /**
   * El navbar real (header.blade.php) mide distinto según breakpoint/estado (logo más chico en
   * móvil, etc.). En vez de asumir un alto fijo (como hacía el `top: 75px` portado 1:1 de
   * programas.css), medimos el alto real del header y lo exponemos como variable CSS para que
   * cualquier elemento sticky de página (p. ej. #salto-rapido en el catálogo) se pegue justo debajo,
   * sin dejar huecos ni desajustarse al redimensionar la ventana.
   */
  ngAfterViewInit(): void {
    const el = this.headerEl()?.nativeElement;
    if (!el || typeof ResizeObserver === 'undefined') return;

    const setHeaderHeightVar = () => {
      document.documentElement.style.setProperty('--shk-header-h', `${el.getBoundingClientRect().height}px`);
    };

    setHeaderHeightVar();
    this.resizeObserver = new ResizeObserver(setHeaderHeightVar);
    this.resizeObserver.observe(el);
  }

  ngOnDestroy(): void {
    this.resizeObserver?.disconnect();
  }
}
