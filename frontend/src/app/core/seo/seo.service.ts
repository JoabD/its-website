import { DOCUMENT } from '@angular/common';
import { Injectable, inject } from '@angular/core';
import { Meta, Title } from '@angular/platform-browser';
import { ActivatedRouteSnapshot, NavigationEnd, Router } from '@angular/router';
import { filter } from 'rxjs/operators';

/**
 * Datos de SEO por ruta, declarados en `data` dentro de app.routes.ts (ver ese archivo).
 * `description` alimenta <meta name="description"> y las tarjetas Open Graph/Twitter; `ogImage`
 * permite reemplazar la imagen de vista previa por página (si no se declara, se usa la del
 * instituto); `noIndex` marca rutas que NO deben indexarse (todo el árbol /admin).
 */
export interface SeoRouteData {
  description?: string;
  ogImage?: string;
  noIndex?: boolean;
}

const SITE_NAME = 'Instituto Teológico Shekinah';
const SITE_URL = 'https://institutoteologicoshekinah.com';
const DEFAULT_DESCRIPTION =
  'Instituto Teológico Shekinah (ITS): formación bíblica y ministerial con planes de estudio, ' +
  'inscripción en línea y sedes regionales. Prepárate para servir con una base sólida en las Escrituras.';
const DEFAULT_OG_IMAGE = `${SITE_URL}/img/instituto-1.jpg`;

/**
 * SEO, fase 1 (ver docs/Plan-SEO-Google-Search.md): centraliza aquí, en un solo lugar, lo que
 * antes no existía en absoluto — meta description, Open Graph/Twitter Cards, canonical y el
 * bloqueo de indexación del panel admin — en vez de repetirlo componente por componente. Angular
 * Router ya actualiza <title> solo (routes.ts declara `title` por ruta); este servicio hace lo
 * mismo para el resto de las etiquetas, leyendo `data` de la ruta activa en cada NavigationEnd.
 *
 * Se inicializa una sola vez desde App (app.ts) con seo.init().
 */
@Injectable({ providedIn: 'root' })
export class SeoService {
  private readonly router = inject(Router);
  private readonly title = inject(Title);
  private readonly meta = inject(Meta);
  // "document is not defined" durante el prerender (ng build): el `document` global del navegador
  // no existe en el worker de Node que renderiza cada ruta — hay que pedirlo por DI (DOCUMENT, de
  // @angular/common), que ahí resuelve al documento del lado servidor.
  private readonly document = inject(DOCUMENT);

  init(): void {
    this.router.events.pipe(filter((event): event is NavigationEnd => event instanceof NavigationEnd)).subscribe(() => {
      this.updateTags();
    });
    // Primera carga: NavigationEnd del arranque puede dispararse antes de que este servicio se
    // suscriba, así que también actualizamos una vez de inmediato.
    this.updateTags();
  }

  private updateTags(): void {
    const data = this.collectRouteData();
    const description = data.description ?? DEFAULT_DESCRIPTION;
    const ogImage = data.ogImage ?? DEFAULT_OG_IMAGE;
    // tsconfig usa noUncheckedIndexedAccess, así que .split(...)[0] tipa como `string | undefined`
    // aunque en la práctica split() siempre devuelve al menos un elemento; replace() evita el
    // acceso por índice por completo.
    const path = this.router.url.replace(/[?#].*$/, '');
    const url = SITE_URL + path;
    const titleText = this.title.getTitle();

    this.meta.updateTag({ name: 'description', content: description });
    this.meta.updateTag({ property: 'og:site_name', content: SITE_NAME });
    this.meta.updateTag({ property: 'og:type', content: 'website' });
    this.meta.updateTag({ property: 'og:title', content: titleText });
    this.meta.updateTag({ property: 'og:description', content: description });
    this.meta.updateTag({ property: 'og:image', content: ogImage });
    this.meta.updateTag({ property: 'og:url', content: url });
    this.meta.updateTag({ name: 'twitter:card', content: 'summary_large_image' });
    this.meta.updateTag({ name: 'twitter:title', content: titleText });
    this.meta.updateTag({ name: 'twitter:description', content: description });
    this.meta.updateTag({ name: 'twitter:image', content: ogImage });
    this.meta.updateTag({ name: 'robots', content: data.noIndex ? 'noindex, nofollow' : 'index, follow' });

    this.updateCanonical(url);
  }

  // Recorre el árbol de rutas activo (raíz → hoja) acumulando `data`, para que una ruta hija
  // (p. ej. /admin/alumnos) herede automáticamente el noIndex declarado en su padre (/admin) sin
  // tener que repetirlo en cada sub-ruta.
  private collectRouteData(): SeoRouteData {
    let snapshot: ActivatedRouteSnapshot | null = this.router.routerState.snapshot.root;
    let merged: SeoRouteData = {};
    while (snapshot) {
      merged = { ...merged, ...(snapshot.data as SeoRouteData) };
      snapshot = snapshot.firstChild;
    }
    return merged;
  }

  private updateCanonical(url: string): void {
    let link = this.document.querySelector<HTMLLinkElement>('link[rel="canonical"]');
    if (!link) {
      link = this.document.createElement('link');
      link.setAttribute('rel', 'canonical');
      this.document.head.appendChild(link);
    }
    link.setAttribute('href', url);
  }
}
