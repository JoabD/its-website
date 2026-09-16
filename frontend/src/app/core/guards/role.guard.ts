import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthStore } from '../auth/auth.store';
import { UserRole } from '../../domain/models';

/**
 * RN-08: "la autorización se evalúa siempre en servidor" — este guard es solo UX (ocultar rutas),
 * nunca la frontera de seguridad real; el servidor vuelve a validar todo (AuthorizationBehavior).
 */
export function roleGuard(allowedRoles: readonly UserRole[]): CanActivateFn {
  return () => {
    const authStore = inject(AuthStore);
    const router = inject(Router);
    const role = authStore.role();

    if (role !== null && allowedRoles.includes(role)) {
      return true;
    }

    return router.createUrlTree(['/admin']);
  };
}
