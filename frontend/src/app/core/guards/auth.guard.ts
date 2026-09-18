import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthStore } from '../auth/auth.store';
import { LoginModalService } from '../auth/login-modal.service';

/**
 * Ya no existe una página `/admin/login`: si no hay sesión, se redirige a la raíz y se abre el
 * modal de acceso sobre el sitio público (RN-08: esto es solo UX, el backend vuelve a validar).
 */
export const authGuard: CanActivateFn = () => {
  const authStore = inject(AuthStore);
  const router = inject(Router);
  const loginModal = inject(LoginModalService);

  if (authStore.isAuthenticated()) {
    return true;
  }

  loginModal.open();
  return router.createUrlTree(['/']);
};
