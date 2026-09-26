import { Routes } from '@angular/router';

/**
 * Mapa de rutas (PROMPT-MAESTRO.md §7 + §8): sitio público sin guard, panel administrativo
 * protegido por authGuard + mustChangePasswordGuard, y sub-rutas de rol protegidas además por
 * roleGuard. RN-08 recuerda que estos guards son solo UX — el backend vuelve a validar todo.
 * Cada ruta declara `title`: Angular Router actualiza el <title> de la pestaña automáticamente
 * al navegar (antes se quedaba en "Frontend" en todas las páginas).
 */
export const routes: Routes = [
  {
    path: '',
    loadComponent: () => import('./layouts/public-shell/public-shell.component').then((m) => m.PublicShellComponent),
    children: [
      {
        path: '',
        title: 'Instituto Shekinah | Formación Bíblica y Ministerial',
        data: {
          description:
            'Instituto Teológico Shekinah (ITS): formación bíblica y ministerial con planes de estudio, ' +
            'sedes regionales e inscripción en línea. Prepárate para servir con una base sólida en las Escrituras.',
        },
        loadComponent: () => import('./features/public/home/home.component').then((m) => m.HomeComponent),
      },
      {
        path: 'planes',
        title: 'Planes de Estudio | Instituto Shekinah',
        data: {
          description:
            'Conoce los planes de estudio del Instituto Teológico Shekinah: duración, modalidades y requisitos ' +
            'para cada nivel de formación bíblica y ministerial.',
        },
        loadComponent: () => import('./features/public/plans/plans.component').then((m) => m.PlansComponent),
      },
      {
        path: 'programas',
        title: 'Catálogo de Materias | Instituto Shekinah',
        data: {
          description:
            'Catálogo completo de materias del Instituto Teológico Shekinah, organizado por plan de estudios ' +
            'y semestre.',
        },
        loadComponent: () => import('./features/public/catalog/catalog.component').then((m) => m.CatalogComponent),
      },
      {
        path: 'contacto',
        title: 'Contacto | Instituto Shekinah',
        data: {
          description:
            'Contacto del Instituto Teológico Shekinah: correo, teléfono y datos de la asociación religiosa ' +
            '(ICAP A.R.). Escríbenos y con gusto resolvemos tus dudas.',
        },
        loadComponent: () => import('./features/public/contact/contact.component').then((m) => m.ContactComponent),
      },
      {
        path: 'inscripcion',
        title: 'Formulario de Inscripción | Instituto Shekinah',
        data: {
          description:
            'Inscríbete en línea al Instituto Teológico Shekinah: completa el formulario de admisión y elige tu ' +
            'sede regional y modalidad de estudio.',
        },
        loadComponent: () =>
          import('./features/public/admission/admission-form.component').then((m) => m.AdmissionFormComponent),
      },
      {
        path: 'calendario',
        title: 'Calendario Institucional | Instituto Shekinah',
        data: {
          description:
            'Calendario público de eventos del Instituto Teológico Shekinah: fechas de inscripción, actividades ' +
            'y eventos por sede regional.',
        },
        loadComponent: () =>
          import('./features/public/calendar/calendar.component').then((m) => m.PublicCalendarComponent),
      },
      // Alias de compatibilidad con las rutas previas del sitio Angular.
      { path: 'planes-de-estudio', redirectTo: 'planes' },
      { path: 'materias', redirectTo: 'programas' },
      { path: 'admision', redirectTo: 'inscripcion' },
    ],
  },
  // Ya no hay página `/admin/login`: el acceso es un modal global (shk-login-modal, montado en
  // app.html) que se abre desde el botón "Acceder" del header público o desde authGuard.
  { path: 'admin/login', redirectTo: '' },
  {
    path: 'admin',
    // Rutas + providers del panel admin en un archivo aparte cargado con loadChildren (no children
    // inline): así todo lo que ese módulo importe a nivel estático — incluyendo provideIonicAngular
    // de @ionic/angular — solo se descarga cuando el navegador entra a /admin, y el sitio público
    // nunca lo toca. Ver admin.routes.ts para el detalle de por qué esto es necesario.
    loadChildren: () => import('./admin.routes').then((m) => m.ADMIN_ROUTES),
  },
  { path: '**', redirectTo: '' },
];
