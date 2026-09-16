import { Component, input } from '@angular/core';

export type ButtonVariant = 'primary' | 'secondary' | 'danger' | 'ghost';

@Component({
  selector: 'shk-button',
  template: `
    <button
      [type]="type()"
      [disabled]="disabled() || loading()"
      class="inline-flex items-center justify-center gap-2 rounded-full px-5 py-2.5 text-sm font-semibold
             font-[var(--shk-font-heading)] tracking-tight transition disabled:cursor-not-allowed disabled:opacity-60"
      [class]="variantClasses()"
    >
      @if (loading()) {
        <span class="h-3.5 w-3.5 animate-spin rounded-full border-2 border-current border-t-transparent"></span>
      }
      <ng-content />
    </button>
  `,
})
export class ButtonComponent {
  readonly variant = input<ButtonVariant>('primary');
  readonly type = input<'button' | 'submit'>('button');
  readonly disabled = input(false);
  readonly loading = input(false);

  protected variantClasses(): string {
    switch (this.variant()) {
      case 'primary':
        return 'bg-[var(--shk-color-accent)] text-[var(--shk-color-primary-dark)] shadow-sm hover:bg-[var(--shk-color-accent-dark)]';
      case 'secondary':
        return 'bg-transparent text-[var(--shk-color-primary)] border border-[var(--shk-color-primary)] hover:bg-[var(--shk-color-primary)]/5';
      case 'danger':
        return 'bg-red-600 text-white hover:bg-red-700';
      case 'ghost':
        return 'bg-transparent text-slate-700 hover:bg-slate-100';
    }
  }
}
