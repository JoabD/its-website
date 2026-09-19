import { registerLocaleData } from '@angular/common';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import localeEsMx from '@angular/common/locales/es-MX';
import {
  ApplicationConfig,
  LOCALE_ID,
  provideBrowserGlobalErrorListeners,
  provideZonelessChangeDetection,
} from '@angular/core';
import { provideRouter } from '@angular/router';
import { DateAdapter, provideCalendar } from 'angular-calendar';
import { adapterFactory } from 'angular-calendar/date-adapters/date-fns';

import { routes } from './app.routes';
import { authInterceptor, refreshInterceptor } from './core/http/auth.interceptor';
import { errorToastInterceptor } from './core/http/error-toast.interceptor';

// 1. Registra los datos de la región de México para eliminar el error NG0701
registerLocaleData(localeEsMx, 'es-MX');

export const appConfig: ApplicationConfig = {
  providers: [
    // 2. Define es-MX como el idioma por defecto para los pipes y componentes de Angular
    { provide: LOCALE_ID, useValue: 'es-MX' },
    provideBrowserGlobalErrorListeners(),
    // Angular 21 zoneless (PROMPT-MAESTRO.md §3 stack frontend): sin Zone.js, detección de cambios
    // dirigida por signals.
    provideZonelessChangeDetection(),
    provideRouter(routes),
    provideHttpClient(
      withInterceptors([authInterceptor, refreshInterceptor, errorToastInterceptor]),
    ),
    // Calendario visual del panel admin (angular-calendar): adaptador date-fns, más liviano que
    // moment y sin dependencias extra en el proyecto.
    provideCalendar({ provide: DateAdapter, useFactory: adapterFactory }),
  ],
};
