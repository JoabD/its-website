import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, throwError } from 'rxjs';
import { AuthStore } from '../auth/auth.store';
import { LoginModalService } from '../auth/login-modal.service';

/** Interceptor auth bearer: adjunta el access token a toda petición hacia la API. */
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const authStore = inject(AuthStore);
  const token = authStore.accessToken();

  // BUG REAL encontrado: en producción `req.url` es una URL ABSOLUTA (environment.apiBaseUrl
  // apunta al App Service de Azure — ver api-client.ts), así que nunca empezaba con "/api/" y este
  // interceptor jamás adjuntaba el Authorization: Bearer. En dev pasaba inadvertido porque el proxy
  // de Angular sí deja la URL relativa ("/api/v1/..."). Resultado: TODO endpoint autenticado
  // (cambiar contraseña, /auth/me, el panel entero) respondía 401 solo en producción, nunca en local.
  if (!token || !req.url.includes('/api/')) {
    return next(req);
  }

  return next(req.clone({ setHeaders: { Authorization: `Bearer ${token}` } }));
};

/**
 * Interceptor de 401: en un refactor posterior encolaría la petición fallida y reintentaría tras
 * refrescar el token (spec técnico §6: "interceptores (auth bearer, refresh 401, error → toast,
 * loading)"). Aquí, para mantener el alcance de este entregable, se cierra sesión de forma segura
 * ante un 401 — el flujo de refresh completo (cola de peticiones en vuelo) queda documentado como
 * siguiente paso en docs/DECISIONS.md. `authStore.logout()` ya redirige a la raíz; aquí solo se
 * reabre el modal de acceso para que la persona pueda volver a autenticarse sin buscar el botón.
 */
export const refreshInterceptor: HttpInterceptorFn = (req, next) => {
  const authStore = inject(AuthStore);
  const loginModal = inject(LoginModalService);

  return next(req).pipe(
    catchError((error: unknown) => {
      if (error instanceof HttpErrorResponse && error.status === 401 && req.url.includes('/api/') && !req.url.includes('/auth/login')) {
        authStore.logout();
        loginModal.open();
      }

      return throwError(() => error);
    }),
  );
};
