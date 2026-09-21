import { BootstrapContext, bootstrapApplication } from '@angular/platform-browser';

import { App } from './app/app';
import { config } from './app/app.config.server';

// NG0401 ("Angular requires a platform to be initialized"): en esta versión, el bootstrap del
// lado servidor (usado por el prerender de ng build) exige recibir y reenviar el BootstrapContext
// que el motor de renderizado de @angular/ssr le pasa a esta función — sin el tercer argumento,
// bootstrapApplication no puede inicializar la plataforma de servidor.
const bootstrap = (context: BootstrapContext) => bootstrapApplication(App, config, context);

export default bootstrap;
