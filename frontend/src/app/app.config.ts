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
