import { Routes } from '@angular/router';
import { authGuard } from './core/guards/auth.guard';
import { roleGuard } from './core/guards/role.guard';
import { mustChangePasswordGuard } from './core/guards/must-change-password.guard';

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
    loadComponent: () => import('./layouts/admin-shell/admin-shell.component').then((m) => m.AdminShellComponent),
    canActivate: [authGuard],
    // SEO: todo el panel admin es privado y no debe aparecer en buscadores. SeoService (core/seo)
    // hereda este `noIndex` a cada sub-ruta y agrega <meta name="robots" content="noindex, nofollow">;
    // robots.txt además bloquea /admin por completo para el rastreo.
    data: { noIndex: true },
    children: [
      {
        path: 'cambiar-password',
        title: 'Cambiar contraseña | Panel ITS',
        loadComponent: () =>
          import('./features/admin/change-password/change-password.component').then((m) => m.ChangePasswordComponent),
      },
      {
        path: '',
        canActivate: [mustChangePasswordGuard],
        children: [
          {
            path: '',
            title: 'Panel Administrativo | Instituto Shekinah',
            loadComponent: () => import('./features/admin/dashboard/dashboard.component').then((m) => m.DashboardComponent),
          },
          {
            path: 'inscripciones',
            title: 'Inscripciones | Panel ITS',
            canActivate: [roleGuard(['Administrator'])],
            loadComponent: () =>
              import('./features/admin/admissions/admissions.component').then((m) => m.AdmissionsComponent),
          },
          {
            path: 'configuracion/regiones',
            title: 'Configuración · Regiones | Panel ITS',
            canActivate: [roleGuard(['Administrator'])],
            loadComponent: () =>
              import('./features/admin/settings/regions-settings.component').then((m) => m.RegionsSettingsComponent),
          },
          {
            path: 'configuracion/checklist',
            title: 'Configuración · Documentos de inscripción | Panel ITS',
            canActivate: [roleGuard(['Administrator'])],
            loadComponent: () =>
              import('./features/admin/settings/checklist-settings.component').then((m) => m.ChecklistSettingsComponent),
          },
          {
            path: 'avisos',
            title: 'Avisos | Panel ITS',
            loadComponent: () => import('./features/admin/announcements/announcements.component').then((m) => m.AnnouncementsComponent),
          },
          {
            path: 'alumnos',
            title: 'Alumnos | Panel ITS',
            canActivate: [roleGuard(['Administrator', 'RegionalCoordinator', 'RegionalSecretary'])],
            loadComponent: () => import('./features/admin/students/students.component').then((m) => m.StudentsComponent),
          },
          {
            path: 'docentes',
            title: 'Docentes | Panel ITS',
            canActivate: [roleGuard(['Administrator'])],
            loadComponent: () => import('./features/admin/teachers/teachers.component').then((m) => m.TeachersComponent),
          },
          {
            path: 'usuarios',
            title: 'Usuarios | Panel ITS',
            canActivate: [roleGuard(['Administrator'])],
            loadComponent: () => import('./features/admin/users/users.component').then((m) => m.UsersComponent),
          },
          {
            path: 'academico',
            title: 'Académico | Panel ITS',
            canActivate: [roleGuard(['Administrator'])],
            loadComponent: () => import('./features/admin/academic/academic.component').then((m) => m.AcademicComponent),
          },
          {
            path: 'calificaciones',
            title: 'Calificaciones | Panel ITS',
            canActivate: [roleGuard(['Teacher', 'Administrator'])],
            loadComponent: () => import('./features/admin/grades/grades.component').then((m) => m.GradesComponent),
          },
          {
            path: 'mis-materias',
            title: 'Mis Materias | Panel ITS',
            canActivate: [roleGuard(['Student'])],
            loadComponent: () =>
              import('./features/admin/my-courses/my-courses.component').then((m) => m.MyCoursesComponent),
          },
          {
            path: 'mi-perfil',
            title: 'Mi Perfil | Panel ITS',
            canActivate: [roleGuard(['Student'])],
            loadComponent: () =>
              import('./features/admin/my-profile/my-profile.component').then((m) => m.MyProfileComponent),
          },
          {
            path: 'pagos',
            title: 'Pagos | Panel ITS',
            canActivate: [roleGuard(['Administrator', 'RegionalCoordinator', 'RegionalSecretary'])],
            loadComponent: () => import('./features/admin/payments/payments.component').then((m) => m.PaymentsComponent),
          },
          {
            path: 'calendario',
            title: 'Calendario | Panel ITS',
            canActivate: [roleGuard(['Administrator', 'RegionalCoordinator', 'RegionalSecretary'])],
            loadComponent: () => import('./features/admin/calendar/calendar-admin.component').then((m) => m.CalendarAdminComponent),
          },
          {
            path: 'kardex',
            title: 'Kardex | Panel ITS',
            canActivate: [roleGuard(['Administrator', 'RegionalCoordinator', 'RegionalSecretary'])],
            loadComponent: () => import('./features/admin/kardex/kardex-admin.component').then((m) => m.KardexAdminComponent),
          },
          {
            path: 'mi-kardex',
            title: 'Mi Kardex | Panel ITS',
            canActivate: [roleGuard(['Student'])],
            loadComponent: () => import('./features/admin/kardex/my-kardex.component').then((m) => m.MyKardexComponent),
          },
        ],
      },
    ],
  },
  { path: '**', redirectTo: '' },
];
