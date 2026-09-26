import { provideHttpClient, withFetch, withInterceptors } from '@angular/common/http';
import { ApplicationConfig, provideBrowserGlobalErrorListeners, provideZonelessChangeDetection } from '@angular/core';
import { provideRouter } from '@angular/router';
import { DateAdapter, provideCalendar } from 'angular-calendar';
import { adapterFactory } from 'angular-calendar/date-adapters/date-fns';

import { routes } from './app.routes';
import { authInterceptor, refreshInterceptor } from './core/http/auth.interceptor';
import { errorToastInterceptor } from './core/http/error-toast.interceptor';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    // Angular 21 zoneless (PROMPT-MAESTRO.md §3 stack frontend): sin Zone.js, detección de cambios
    // dirigida por signals.
    provideZonelessChangeDetection(),
    provideRouter(routes),
    // Ionic (usabilidad móvil, pedido del cliente 2026-09) se registra a nivel de la ruta /admin en
    // app.routes.ts, NO aquí: si se registrara aquí (root providers) su runtime se metería en el
    // bundle inicial que descarga CUALQUIER visitante del sitio público, aunque nunca vea un
    // componente ion-*. Se comprobó con un build real: puesto aquí, ~190 KB de JS de Ionic/Stencil
    // aparecían en el "Initial chunk" (el que se manda a /, /planes, /contacto, etc.) y el <html>
    // de esas páginas quedaba con la clase "ion-ce" agregada por su inicializador. Registrado en la
    // ruta 'admin' en vez, Angular lo empaqueta junto con el resto del panel admin (que ya es
    // lazy-loaded) y el sitio público no lo descarga ni lo inicializa en absoluto.
    // withFetch: SEO fase 3 (prerendering, ver docs/Plan-SEO-Google-Search.md) — /programas e
    // /inscripcion llaman a la API (catalog/curriculum, catalog/regions) al inicializar; durante el
    // prerender esas peticiones corren en Node (@angular/platform-server), que no tiene el backend
    // XHR tradicional. La API Fetch sí funciona ahí, así que esto evita que esas rutas se
    // prerendericen con la lista vacía por un fallo silencioso de red.
    provideHttpClient(withFetch(), withInterceptors([authInterceptor, refreshInterceptor, errorToastInterceptor])),
    // Calendario visual del panel admin (angular-calendar): adaptador date-fns, más liviano que
    // moment y sin dependencias extra en el proyecto.
    provideCalendar({ provide: DateAdapter, useFactory: adapterFactory }),
  ],
};
