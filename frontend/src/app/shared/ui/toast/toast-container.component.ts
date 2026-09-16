import { Component, inject } from '@angular/core';
import { ToastService } from './toast.service';

@Component({
  selector: 'shk-toast-container',
  template: `
    <div class="fixed top-4 right-4 z-50 flex flex-col gap-2">
      @for (message of toast.messages(); track message.id) {
        <div
          class="rounded-[var(--shk-radius)] px-4 py-3 text-sm text-white shadow-lg"
          [class.bg-emerald-600]="message.kind === 'success'"
          [class.bg-red-600]="message.kind === 'error'"
          [class.bg-slate-700]="message.kind === 'info'"
        >
          {{ message.text }}
        </div>
      }
    </div>
  `,
})
export class ToastContainerComponent {
  protected readonly toast = inject(ToastService);
}
