import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, switchMap, throwError } from 'rxjs';
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
 * Interceptor de 401: BUG REAL encontrado — este interceptor nunca intentaba refrescar el token,
 * pese a que el backend ya soporta POST /auth/refresh (7 días, Jwt:RefreshTokenDays) y a que el
 * access token dura apenas 15 minutos (Jwt:AccessTokenMinutes). En la práctica, cualquier sesión se
 * cerraba sola a los 15 minutos de la siguiente petición — el usuario percibía esto como "la sesión
 * no se mantiene", agravado por sessionStorage (ver auth.store.ts) y por que el 401 llegaba en
 * cualquier ventana nueva que no tuviera el token todavía.
 *
 * Ahora: ante un 401 que no sea de /auth/login ni /auth/refresh (para no reintentar en bucle),
 * intenta renovar en silencio con el refresh token guardado y reintenta la petición original con el
 * access token nuevo. Solo si el refresh también falla (refresh token vencido/revocado, o no había
 * ninguno guardado) se cierra sesión y se reabre el modal de acceso.
 */
export const refreshInterceptor: HttpInterceptorFn = (req, next) => {
  const authStore = inject(AuthStore);
  const loginModal = inject(LoginModalService);

  return next(req).pipe(
    catchError((error: unknown) => {
      const isAuthEndpoint = req.url.includes('/auth/login') || req.url.includes('/auth/refresh');

      if (error instanceof HttpErrorResponse && error.status === 401 && req.url.includes('/api/') && !isAuthEndpoint) {
        return authStore.refreshAccessToken().pipe(
          switchMap((refreshed) => {
            if (!refreshed) {
              authStore.logout();
              loginModal.open();
              return throwError(() => error);
            }

            return next(req.clone({ setHeaders: { Authorization: `Bearer ${refreshed.accessToken}` } }));
          }),
        );
      }

      return throwError(() => error);
    }),
  );
};
