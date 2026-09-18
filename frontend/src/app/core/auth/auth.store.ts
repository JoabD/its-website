import { HttpErrorResponse } from '@angular/common/http';
import { computed, inject } from '@angular/core';
import { Router } from '@angular/router';
import { patchState, signalStore, withComputed, withMethods, withState } from '@ngrx/signals';
import { pipe, switchMap, tap } from 'rxjs';
import { tapResponse } from '@ngrx/operators';
import { rxMethod } from '@ngrx/signals/rxjs-interop';
import { LoginResponseDto } from '../../api/schema';
import { CurrentUser } from '../../domain/models';
import { ApiClient } from '../http/api-client';
import { LoginModalService } from './login-modal.service';

const ACCESS_TOKEN_KEY = 'shk_access_token';
const REFRESH_TOKEN_KEY = 'shk_refresh_token';

interface AuthState {
  readonly user: CurrentUser | null;
  readonly accessToken: string | null;
  readonly refreshToken: string | null;
  readonly loading: boolean;
  readonly error: string | null;
}

const initialState: AuthState = {
  user: null,
  accessToken: sessionStorage.getItem(ACCESS_TOKEN_KEY),
  refreshToken: sessionStorage.getItem(REFRESH_TOKEN_KEY),
  loading: false,
  error: null,
};

/**
 * Estado de autenticación (SignalStore, spec técnico §6.3). Prioridad de reactividad: signals >
 * toSignal > async pipe > rxMethod > subscribe+SubSink (R7) — este store no tiene ni una sola
 * suscripción manual: todo pasa por rxMethod.
 */
export const AuthStore = signalStore(
  { providedIn: 'root' },
  withState(initialState),
  withComputed(({ user, accessToken }) => ({
    isAuthenticated: computed(() => accessToken() !== null),
    role: computed(() => user()?.role ?? null),
    mustChangePassword: computed(() => user()?.mustChangePassword ?? false),
  })),
  // Separado en dos withMethods: `login` necesita llamar a `loadCurrentUser` (ver más abajo), y
  // dentro de un mismo withMethods TypeScript no puede referenciar un método hermano que se está
  // definiendo en ese mismo bloque (el tipo de `store` ahí todavía no lo incluye).
  withMethods((store, api = inject(ApiClient)) => ({
    loadCurrentUser: rxMethod<void>(
      pipe(
        switchMap(() =>
          api.get<CurrentUser>('/auth/me').pipe(
            tapResponse({
              next: (user) => patchState(store, { user }),
              error: () => patchState(store, { user: null, accessToken: null, refreshToken: null }),
            }),
          ),
        ),
      ),
    ),
  })),
  withMethods((store, api = inject(ApiClient), router = inject(Router), loginModal = inject(LoginModalService)) => ({
    login: rxMethod<{ email: string; password: string }>(
      pipe(
        tap(() => patchState(store, { loading: true, error: null })),
        switchMap((credentials) =>
          api.post<LoginResponseDto>('/auth/login', credentials).pipe(
            tapResponse({
              next: (response) => {
                sessionStorage.setItem(ACCESS_TOKEN_KEY, response.accessToken);
                sessionStorage.setItem(REFRESH_TOKEN_KEY, response.refreshToken);
                patchState(store, { accessToken: response.accessToken, refreshToken: response.refreshToken, loading: false });
                loginModal.close();
                // BUG REAL encontrado: sin esto, `user` (y por lo tanto `role`, calculado de
                // `user()?.role`) se quedaba en null hasta que la página se recargaba manualmente
                // — así que justo después de iniciar sesión, todo el contenido condicionado por rol
                // (menú, atajos del dashboard, formularios exclusivos de Administrator) no se
                // mostraba, dando la impresión de un panel vacío ("no veo nada de nada").
                store.loadCurrentUser();
                void router.navigateByUrl(response.mustChangePassword ? '/admin/cambiar-password' : '/admin');
              },
              error: (error: HttpErrorResponse) => {
                const detail = (error.error as { detail?: string } | null)?.detail ?? 'No se pudo iniciar sesión.';
                patchState(store, { loading: false, error: detail });
              },
            }),
          ),
        ),
      ),
    ),

    logout(): void {
      sessionStorage.removeItem(ACCESS_TOKEN_KEY);
      sessionStorage.removeItem(REFRESH_TOKEN_KEY);
      patchState(store, { user: null, accessToken: null, refreshToken: null });
      void router.navigateByUrl('/');
    },

    setTokens(accessToken: string, refreshToken: string): void {
      sessionStorage.setItem(ACCESS_TOKEN_KEY, accessToken);
      sessionStorage.setItem(REFRESH_TOKEN_KEY, refreshToken);
      patchState(store, { accessToken, refreshToken });
    },
  })),
);
