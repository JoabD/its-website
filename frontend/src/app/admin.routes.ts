import { Routes } from '@angular/router';
import { provideIonicAngular } from '@ionic/angular';
import { authGuard } from './core/guards/auth.guard';
import { roleGuard } from './core/guards/role.guard';
import { mustChangePasswordGuard } from './core/guards/must-change-password.guard';

/**
 * Rutas del panel admin, en un archivo aparte cargado vía `loadChildren` (ver app.routes.ts) — NO
 * como children inline. Esto es a propósito: app.routes.ts se importa de forma estática (main.js lo
 * necesita para armar la tabla de rutas), así que CUALQUIER import a nivel de módulo dentro de ese
 * archivo viaja en el bundle inicial del sitio público, sin importar en qué route.providers quede
 * usado. Con `loadChildren`, en cambio, este archivo completo (y su import de `provideIonicAngular`)
 * solo se descarga cuando el navegador entra a /admin — el sitio público nunca lo toca.
 * Se comprobó con un build real: con Ionic en app.routes.ts, `main.js` seguía con un
 * `import { provideIonicAngular } from "./chunk-....js"` estático; movido aquí, desaparece.
 */
export const ADMIN_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () => import('./layouts/admin-shell/admin-shell.component').then((m) => m.AdminShellComponent),
    canActivate: [authGuard],
    // Ionic (tab bar móvil + swipe-to-close en drawers) SOLO se usa dentro de /admin.
    providers: [provideIonicAngular({})],
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
];
