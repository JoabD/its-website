import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthStore } from '../auth/auth.store';

/** RN-24: ningún otro endpoint/pantalla responde hasta que ocurra el cambio de contraseña obligatorio. */
export const mustChangePasswordGuard: CanActivateFn = () => {
  const authStore = inject(AuthStore);
  const router = inject(Router);

  if (authStore.mustChangePassword()) {
    return router.createUrlTree(['/admin/cambiar-password']);
  }

  return true;
};
