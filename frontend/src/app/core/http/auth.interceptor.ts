import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, throwError } from 'rxjs';
import { AuthStore } from '../auth/auth.store';

/** Interceptor auth bearer: adjunta el access token a toda petición hacia la API. */
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const authStore = inject(AuthStore);
  const token = authStore.accessToken();

  if (!token || !req.url.startsWith('/api/')) {
    return next(req);
  }

  return next(req.clone({ setHeaders: { Authorization: `Bearer ${token}` } }));
};

/**
 * Interceptor de 401: en un refactor posterior encolaría la petición fallida y reintentaría tras
 * refrescar el token (spec técnico §6: "interceptores (auth bearer, refresh 401, error → toast,
 * loading)"). Aquí, para mantener el alcance de este entregable, se cierra sesión de forma segura
 * ante un 401 — el flujo de refresh completo (cola de peticiones en vuelo) queda documentado como
 * siguiente paso en docs/DECISIONS.md.
 */
export const refreshInterceptor: HttpInterceptorFn = (req, next) => {
  const authStore = inject(AuthStore);
  const router = inject(Router);

  return next(req).pipe(
    catchError((error: unknown) => {
      if (error instanceof HttpErrorResponse && error.status === 401 && req.url.startsWith('/api/') && !req.url.includes('/auth/login')) {
        authStore.logout();
        void router.navigateByUrl('/admin/login');
      }

      return throwError(() => error);
    }),
  );
};
