import { registerLocaleData } from '@angular/common';
import localeEsMx from '@angular/common/locales/es-MX';
import localeEsMxExtra from '@angular/common/locales/extra/es-MX';
import { bootstrapApplication } from '@angular/platform-browser';
import { appConfig } from './app/app.config';
import { App } from './app/app';

// NG0701 "Missing locale data for the locale 'es-MX'": Angular solo trae registrado 'en-US' por
// defecto; cualquier otro locale (aquí usado en el calendario admin — angular-calendar recibe
// locale="es-MX" — y en los toLocaleDateString('es-MX', ...) del resto del sitio) necesita
// registrarse explícitamente una sola vez, antes de bootstrapApplication.
registerLocaleData(localeEsMx, 'es-MX', localeEsMxExtra);

bootstrapApplication(App, appConfig)
  .catch((err) => console.error(err));
