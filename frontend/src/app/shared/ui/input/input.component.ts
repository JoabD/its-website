import { Component, input } from '@angular/core';
import { ControlValueAccessor, FormsModule, NG_VALUE_ACCESSOR } from '@angular/forms';

@Component({
  selector: 'shk-input',
  imports: [FormsModule],
  providers: [{ provide: NG_VALUE_ACCESSOR, useExisting: InputComponent, multi: true }],
  template: `
    <label class="flex flex-col gap-1.5 text-sm">
      @if (label()) {
        <span class="font-medium text-slate-700">{{ label() }}@if (required()) { <span class="text-[var(--shk-color-accent-dark)]">*</span> }</span>
      }
      <input
        class="rounded-xl border border-slate-200 bg-white px-4 py-2.5 text-sm shadow-sm transition
               placeholder:text-slate-400
               focus:border-[var(--shk-color-accent)] focus:outline-none focus:ring-2 focus:ring-[var(--shk-color-accent)]/30"
        [type]="type()"
        [placeholder]="placeholder()"
        [ngModel]="value"
        (ngModelChange)="onChange($event)"
        (blur)="onTouched()"
      />
      @if (error()) {
        <span class="text-xs text-red-600">{{ error() }}</span>
      }
    </label>
  `,
})
export class InputComponent implements ControlValueAccessor {
  readonly label = input<string>('');
  readonly type = input<string>('text');
  readonly placeholder = input<string>('');
  readonly required = input(false);
  readonly error = input<string | null>(null);

  protected value = '';
  protected onChange: (value: string) => void = () => {};
  protected onTouched: () => void = () => {};

  writeValue(value: string): void {
    this.value = value ?? '';
  }

  registerOnChange(fn: (value: string) => void): void {
    this.onChange = fn;
  }

  registerOnTouched(fn: () => void): void {
    this.onTouched = fn;
  }
}
