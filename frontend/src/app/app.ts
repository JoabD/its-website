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
  private readonly auth = inject(AuthStore);

  constructor() {
    // Rehidrata el usuario actual si ya había un access token en sessionStorage (recarga de página).
    if (this.auth.isAuthenticated()) {
      this.auth.loadCurrentUser();
    }
  }
}
