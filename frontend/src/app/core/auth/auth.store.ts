import { HttpErrorResponse } from '@angular/common/http';
import { computed, inject } from '@angular/core';
import { Router } from '@angular/router';
import { patchState, signalStore, withComputed, withHooks, withMethods, withState } from '@ngrx/signals';
import { Observable, catchError, of, pipe, switchMap, tap } from 'rxjs';
import { tapResponse } from '@ngrx/operators';
import { rxMethod } from '@ngrx/signals/rxjs-interop';
import { LoginResponseDto, RefreshTokenResponseDto } from '../../api/schema';
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
  // BUG REAL encontrado (el que en realidad causaba "pierdo la sesión al refrescar/cambiar de
  // pestaña"): roleGuard evalúa authStore.role() de forma SÍNCRONA en cuanto Angular activa la
  // ruta. Al recargar la página, `accessToken` ya está disponible (viene de localStorage), pero
  // `user` (de donde sale `role`) todavía es null porque /auth/me es una llamada HTTP asíncrona que
  // apenas se disparó. roleGuard veía role()===null y redirigía de inmediato a /admin — la persona
  // ya estaba autenticada, pero cualquier ruta con roleGuard (inscripciones, configuración, etc.) la
  // rebotaba antes de que /auth/me alcanzara a responder. `bootstrapping` le dice a los guards
  // "todavía no sé el rol, espera" en vez de asumir que no hay sesión.
  readonly bootstrapping: boolean;
}

// BUG REAL encontrado: los tokens vivían en sessionStorage, que NUNCA se comparte entre ventanas
// (solo lo hereda una pestaña duplicada) — así que abrir una ventana nueva del mismo navegador
// perdía la sesión aunque el usuario ya hubiera iniciado sesión en otra. localStorage sí se comparte
// entre todas las pestañas/ventanas del mismo origen, que es el comportamiento "sesión abierta"
// esperado. La expiración real de la sesión ya no depende de esto — la controla el refresh token
// (7 días, Jwt:RefreshTokenDays) vía refreshTokens() más abajo.
//
// SEO fase 3 (prerendering, ver docs/Plan-SEO-Google-Search.md): este módulo se importa siempre
// (App inyecta AuthStore en el constructor, incluso en rutas públicas), y este bloque de nivel de
// módulo se ejecuta en cuanto se importa. Al prerenderizar (ng build con outputMode: 'static') este
// código corre en Node vía @angular/platform-server, donde `localStorage` NO existe — sin esta
// guarda, `ng build` truena con "localStorage is not defined" al generar el HTML estático de home,
// planes, etc. `safeLocalStorage()` devuelve null fuera del navegador en vez de lanzar.
function safeLocalStorage(): Storage | null {
  return typeof localStorage === 'undefined' ? null : localStorage;
}

const storedAccessToken = safeLocalStorage()?.getItem(ACCESS_TOKEN_KEY) ?? null;

const initialState: AuthState = {
  user: null,
  accessToken: storedAccessToken,
  refreshToken: safeLocalStorage()?.getItem(REFRESH_TOKEN_KEY) ?? null,
  loading: false,
  error: null,
  // true solo si hay un token guardado: en ese caso SÍ vamos a intentar /auth/me al arrancar
  // (onInit, más abajo) y los guards deben esperar esa respuesta antes de decidir por rol.
  bootstrapping: storedAccessToken !== null,
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
              next: (user) => patchState(store, { user, bootstrapping: false }),
              error: () => patchState(store, { user: null, accessToken: null, refreshToken: null, bootstrapping: false }),
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
                localStorage.setItem(ACCESS_TOKEN_KEY, response.accessToken);
                localStorage.setItem(REFRESH_TOKEN_KEY, response.refreshToken);
                patchState(store, { accessToken: response.accessToken, refreshToken: response.refreshToken, loading: false, bootstrapping: true });
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
      localStorage.removeItem(ACCESS_TOKEN_KEY);
      localStorage.removeItem(REFRESH_TOKEN_KEY);
      patchState(store, { user: null, accessToken: null, refreshToken: null, bootstrapping: false });
      void router.navigateByUrl('/');
    },

    setTokens(accessToken: string, refreshToken: string): void {
      localStorage.setItem(ACCESS_TOKEN_KEY, accessToken);
      localStorage.setItem(REFRESH_TOKEN_KEY, refreshToken);
      patchState(store, { accessToken, refreshToken });
    },

    // BUG REAL encontrado: refreshInterceptor (auth.interceptor.ts) NUNCA llamaba a este endpoint —
    // ante cualquier 401 (el access token dura solo 15 min, Jwt:AccessTokenMinutes) cerraba la
    // sesión de inmediato. Con Jwt:RefreshTokenDays=7, la sesión debería durar días, no 15 minutos;
    // este método es lo que el interceptor usa ahora para renovar en silencio y reintentar la
    // petición, en vez de expulsar al usuario. Nota aparte, ya corregida: LoginCommandHandler no
    // persistía el refresh token emitido en el login (solo lo hacía RefreshTokenCommandHandler al
    // rotar), así que la primera llamada a este endpoint tras iniciar sesión siempre fallaba.
    refreshAccessToken(): Observable<RefreshTokenResponseDto | null> {
      const currentRefreshToken = store.refreshToken();
      if (!currentRefreshToken) {
        return of(null);
      }

      return api.post<RefreshTokenResponseDto>('/auth/refresh', { refreshToken: currentRefreshToken }).pipe(
        tap((response) => {
          localStorage.setItem(ACCESS_TOKEN_KEY, response.accessToken);
          localStorage.setItem(REFRESH_TOKEN_KEY, response.refreshToken);
          patchState(store, { accessToken: response.accessToken, refreshToken: response.refreshToken });
        }),
        catchError(() => of(null)),
      );
    },
  })),
  // BUG REAL encontrado: aunque el token sobreviviera en storage, `user` (y por lo tanto role(),
  // mustChangePassword(), todo el contenido condicionado por rol) SOLO se llenaba dentro de login().
  // Al recargar la página o abrir una ventana nueva, isAuthenticated() podía ser true (había
  // accessToken) pero user() seguía null — el panel se veía "logeado pero vacío". onInit rehidrata
  // el usuario actual contra /auth/me en cuanto arranca la app, si ya hay un token guardado.
  //
  // BUG REAL encontrado (la causa real de "se pierde la sesión al refrescar/cambiar de pestaña"):
  // onInit se ejecuta de forma SÍNCRONA como parte de la propia construcción del singleton
  // `AuthStore` (providedIn: 'root'). Llamar aquí a `store.loadCurrentUser()` directamente dispara
  // de inmediato `ApiClient.get()` -> `HttpClient` -> `authInterceptor`, y ese interceptor hace
  // `inject(AuthStore)` — es decir, pide el MISMO singleton que Angular todavía está construyendo.
  // Angular no puede resolver esa referencia circular y lanza `NG0200: Circular dependency detected
  // for 'SignalStore'`. Ese error cae en el `error` handler de `loadCurrentUser`, que hace
  // `patchState(store, { accessToken: null, refreshToken: null, ... })` — borrando la sesión en
  // memoria en cada carga dura de la página, aunque el token en localStorage siga siendo válido.
  // Encolar la llamada con `queueMicrotask` deja que la construcción de `AuthStore` termine y quede
  // registrada en el inyector ANTES de que se dispare la petición HTTP, así que cuando
  // `authInterceptor` hace `inject(AuthStore)` ya recibe la instancia completa, sin ciclo.
  withHooks({
    onInit(store) {
      if (store.accessToken()) {
        queueMicrotask(() => store.loadCurrentUser());
      }
    },
  }),
);
