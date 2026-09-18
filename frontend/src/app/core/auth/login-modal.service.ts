import { Injectable, signal } from '@angular/core';

/**
 * Estado global (mínimo, un booleano) de visibilidad del modal de acceso. Vive fuera del
 * AuthStore a propósito: el store gestiona sesión/tokens, este servicio solo gestiona si el
 * diálogo de login está abierto o cerrado — cualquier parte de la app (header público, guards)
 * puede pedir que se abra sin acoplarse a la lógica de autenticación.
 */
@Injectable({ providedIn: 'root' })
export class LoginModalService {
  private readonly _isOpen = signal(false);
  readonly isOpen = this._isOpen.asReadonly();

  open(): void {
    this._isOpen.set(true);
  }

  close(): void {
    this._isOpen.set(false);
  }
}
