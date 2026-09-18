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
        loadComponent: () => import('./features/public/home/home.component').then((m) => m.HomeComponent),
      },
      {
        path: 'planes',
        title: 'Planes de Estudio | Instituto Shekinah',
        loadComponent: () => import('./features/public/plans/plans.component').then((m) => m.PlansComponent),
      },
      {
        path: 'programas',
        title: 'Catálogo de Materias | Instituto Shekinah',
        loadComponent: () => import('./features/public/catalog/catalog.component').then((m) => m.CatalogComponent),
      },
      {
        path: 'contacto',
        title: 'Contacto | Instituto Shekinah',
        loadComponent: () => import('./features/public/contact/contact.component').then((m) => m.ContactComponent),
      },
      {
        path: 'inscripcion',
        title: 'Formulario de Inscripción | Instituto Shekinah',
        loadComponent: () =>
          import('./features/public/admission/admission-form.component').then((m) => m.AdmissionFormComponent),
      },
      {
        path: 'calendario',
        title: 'Calendario Institucional | Instituto Shekinah',
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
