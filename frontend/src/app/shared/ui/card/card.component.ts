import { Component } from '@angular/core';

@Component({
  selector: 'shk-card',
  template: `
    <div class="rounded-2xl border border-slate-100 bg-[var(--shk-color-surface)] p-6 shadow-[0_2px_16px_rgba(26,39,68,0.06)]">
      <ng-content />
    </div>
  `,
})
export class CardComponent {}
