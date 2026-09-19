import { Component, inject } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { ToastContainerComponent } from './shared/ui/toast/toast-container.component';
import { AuthStore } from './core/auth/auth.store';
import { LoginModalComponent } from './core/auth/login-modal.component';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, ToastContainerComponent, LoginModalComponent],
  templateUrl: './app.html',
})
export class App {
  // Inyectar AuthStore aquí (aunque no se use el valor) fuerza su construcción tan pronto arranca
  // la app — dispara su hook onInit (auth.store.ts), que ya rehidrata el usuario si hay un
  // accessToken guardado en localStorage. No se llama a loadCurrentUser() aquí también: hacerlo
  // duplicaba la petición a /auth/me en cada arranque (una desde este constructor, otra desde
  // onInit) sin ningún beneficio.
  private readonly auth = inject(AuthStore);
}
