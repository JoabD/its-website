import { Injectable, signal } from '@angular/core';

export interface ToastMessage {
  readonly id: number;
  readonly text: string;
  readonly kind: 'success' | 'error' | 'info';
}

let nextId = 0;

@Injectable({ providedIn: 'root' })
export class ToastService {
  private readonly _messages = signal<readonly ToastMessage[]>([]);
  readonly messages = this._messages.asReadonly();

  success(text: string): void {
    this.push(text, 'success');
  }

  error(text: string): void {
    this.push(text, 'error');
  }

  info(text: string): void {
    this.push(text, 'info');
  }

  dismiss(id: number): void {
    this._messages.update((messages) => messages.filter((m) => m.id !== id));
  }

  private push(text: string, kind: ToastMessage['kind']): void {
    const message: ToastMessage = { id: nextId++, text, kind };
    this._messages.update((messages) => [...messages, message]);
    setTimeout(() => this.dismiss(message.id), 5000);
  }
}
