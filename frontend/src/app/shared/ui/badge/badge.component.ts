import { Component, input } from '@angular/core';

export type BadgeTone = 'neutral' | 'success' | 'warning' | 'danger';

@Component({
  selector: 'shk-badge',
  template: `
    <span class="rounded-full px-2.5 py-0.5 text-xs font-medium" [class]="toneClasses()">
      <ng-content />
    </span>
  `,
})
export class BadgeComponent {
  readonly tone = input<BadgeTone>('neutral');

  protected toneClasses(): string {
    switch (this.tone()) {
      case 'success':
        return 'bg-emerald-100 text-emerald-800';
      case 'warning':
        return 'bg-amber-100 text-amber-800';
      case 'danger':
        return 'bg-red-100 text-red-800';
      case 'neutral':
        return 'bg-slate-100 text-slate-700';
    }
  }
}
