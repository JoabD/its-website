import { inject } from '@angular/core';
import { toObservable } from '@angular/core/rxjs-interop';
import { CanActivateFn, Router } from '@angular/router';
import { filter, map, take } from 'rxjs';
import { AuthStore } from '../auth/auth.store';
import { UserRole } from '../../domain/models';

/**
 * RN-08: "la autorización se evalúa siempre en servidor" — este guard es solo UX (ocultar rutas),
 * nunca la frontera de seguridad real; el servidor vuelve a validar todo (AuthorizationBehavior).
 *
 * BUG REAL encontrado — esta era la causa de "pierdo la sesión al refrescar/cambiar de pestaña":
 * este guard evaluaba authStore.role() de forma SÍNCRONA. Al recargar la página, accessToken ya
 * estaba disponible (localStorage), pero `user` (de donde sale role()) seguía null porque /auth/me
 * es una llamada HTTP en curso, todavía sin responder — el guard veía role()===null y rebotaba a
 * /admin de inmediato, como si no hubiera sesión, aunque el token fuera perfectamente válido. Ahora
 * espera a que authStore.bootstrapping() termine (login ya resuelto o fallido) antes de decidir.
 */
export function roleGuard(allowedRoles: readonly UserRole[]): CanActivateFn {
  return () => {
    const authStore = inject(AuthStore);
    const router = inject(Router);

    return toObservable(authStore.bootstrapping).pipe(
      filter((bootstrapping) => !bootstrapping),
      take(1),
      map(() => {
        const role = authStore.role();

        if (role !== null && allowedRoles.includes(role)) {
          return true;
        }

        return router.createUrlTree(['/admin']);
      }),
    );
  };
}
