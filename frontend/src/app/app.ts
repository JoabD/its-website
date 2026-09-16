import { Component, inject } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { ToastContainerComponent } from './shared/ui/toast/toast-container.component';
import { AuthStore } from './core/auth/auth.store';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, ToastContainerComponent],
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
