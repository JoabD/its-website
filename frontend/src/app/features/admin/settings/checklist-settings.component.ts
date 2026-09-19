import { Component, inject, signal } from '@angular/core';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { of } from 'rxjs';
import { catchError, finalize, switchMap } from 'rxjs/operators';
import { ApiClient } from '../../../core/http/api-client';
import { ChecklistItemListItemDto } from '../../../api/schema';
import { CardComponent } from '../../../shared/ui/card/card.component';
import { ButtonComponent } from '../../../shared/ui/button/button.component';
import { ToastService } from '../../../shared/ui/toast/toast.service';

/**
 * Configuración → Documentos de inscripción (§3 de la spec): catálogo editable de qué documentos
 * pide el checklist del panel de revisión de admisiones. "Eliminar" desde aquí es desactivar
 * (nunca borra el historial de lo ya verificado en solicitudes con ese ítem todavía activo).
 * Catálogo vivo (opción A, confirmada): "completo" siempre se calcula contra los ítems activos.
 */
@Component({
  selector: 'shk-checklist-settings',
  imports: [CardComponent, ButtonComponent],
  template: `
    <h1 class="text-2xl font-bold text-slate-900">Configuración · Documentos de inscripción</h1>
    <p class="mt-1 text-sm text-slate-500">
      Estos son los documentos que el checklist del panel de revisión pide verificar antes de inscribir a un solicitante.
      Desactivar un documento no borra el progreso ya marcado en solicitudes existentes.
    </p>

    <shk-card class="mt-6">
      <div class="flex flex-col gap-2 sm:flex-row sm:items-end">
        <div class="flex-1">
          <label class="text-xs font-medium text-slate-500" for="new-item-label">Nuevo documento</label>
          <input
            id="new-item-label"
            type="text"
            class="shk-field mt-1 w-full"
            placeholder="Ej. Fotografía tamaño credencial"
            [value]="newLabel()"
            (input)="newLabel.set($any($event.target).value)"
            (keydown.enter)="create()"
          />
        </div>
        <shk-button variant="primary" [loading]="creating()" (click)="create()">Agregar documento</shk-button>
      </div>
    </shk-card>

    <shk-card class="mt-4 overflow-x-auto p-0">
      <table class="w-full text-left text-sm">
        <thead class="text-slate-500">
          <tr class="border-b border-slate-100">
            <th class="px-6 py-3 font-medium">Documento</th>
            <th class="hidden px-6 py-3 font-medium sm:table-cell">Orden</th>
            <th class="px-6 py-3 font-medium">Activo</th>
            <th class="px-6 py-3"></th>
          </tr>
        </thead>
        <tbody>
          @for (item of items(); track item.id; let i = $index; let count = $count) {
            <tr class="border-b border-slate-50 last:border-0">
              <td class="px-6 py-3">
                @if (editingId() === item.id) {
                  <input type="text" class="shk-field w-full" [value]="editingLabel()" (input)="editingLabel.set($any($event.target).value)" />
                } @else {
                  <span [class.text-slate-400]="!item.isActive">{{ item.label }}</span>
                }
              </td>
              <td class="hidden px-6 py-3 sm:table-cell">
                <div class="flex items-center gap-1">
                  <button type="button" class="shk-order-btn" [disabled]="i === 0 || busyId() === item.id" (click)="move(item, -1)" aria-label="Subir">
                    <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" class="h-4 w-4"><path stroke-linecap="round" stroke-linejoin="round" d="m4.5 15.75 7.5-7.5 7.5 7.5" /></svg>
                  </button>
                  <button type="button" class="shk-order-btn" [disabled]="i === count - 1 || busyId() === item.id" (click)="move(item, 1)" aria-label="Bajar">
                    <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" class="h-4 w-4"><path stroke-linecap="round" stroke-linejoin="round" d="m19.5 8.25-7.5 7.5-7.5-7.5" /></svg>
                  </button>
                </div>
              </td>
              <td class="px-6 py-3">
                <button
                  type="button"
                  class="relative h-6 w-11 rounded-full transition"
                  [class]="item.isActive ? 'bg-[var(--shk-color-accent)]' : 'bg-slate-200'"
                  [disabled]="busyId() === item.id"
                  (click)="toggleActive(item)"
                  [attr.aria-pressed]="item.isActive"
                  aria-label="Activo"
                >
                  <span class="absolute top-0.5 h-5 w-5 rounded-full bg-white shadow transition" [class]="item.isActive ? 'left-5' : 'left-0.5'"></span>
                </button>
              </td>
              <td class="px-6 py-3 text-right">
                @if (editingId() === item.id) {
                  <div class="flex justify-end gap-2">
                    <shk-button variant="ghost" (click)="cancelEdit()">Cancelar</shk-button>
                    <shk-button variant="secondary" [loading]="busyId() === item.id" (click)="saveEdit(item)">Guardar</shk-button>
                  </div>
                } @else {
                  <shk-button variant="ghost" (click)="startEdit(item)">Editar</shk-button>
                }
              </td>
            </tr>
          } @empty {
            <tr><td colspan="4" class="px-6 py-10 text-center text-slate-400">Sin documentos configurados.</td></tr>
          }
        </tbody>
      </table>
    </shk-card>
  `,
  styles: [
    `
    .shk-field {
      border-radius: 0.5rem;
      border: 1px solid #e2e8f0;
      background: #fff;
      padding: 0.5rem 0.75rem;
      font-size: 0.875rem;
    }
    .shk-field:focus { outline: none; border-color: var(--shk-color-accent); }
    .shk-order-btn {
      display: grid;
      place-items: center;
      height: 1.75rem;
      width: 1.75rem;
      border-radius: 0.5rem;
      color: #64748b;
      transition: all 0.15s ease;
    }
    .shk-order-btn:hover:not(:disabled) { background: #f1f5f9; color: var(--shk-color-primary); }
    .shk-order-btn:disabled { opacity: 0.3; cursor: not-allowed; }
    `,
  ],
})
export class ChecklistSettingsComponent {
  private readonly api = inject(ApiClient);
  private readonly toast = inject(ToastService);
  private readonly refreshTick = signal(0);

  protected readonly newLabel = signal('');
  protected readonly creating = signal(false);
  protected readonly busyId = signal<string | null>(null);
  protected readonly editingId = signal<string | null>(null);
  protected readonly editingLabel = signal('');

  protected readonly items = toSignal(
    toObservable(this.refreshTick).pipe(
      switchMap(() =>
        this.api.get<ChecklistItemListItemDto[]>('/admissions/checklist-items').pipe(catchError(() => of<ChecklistItemListItemDto[]>([]))),
      ),
    ),
    { initialValue: [] as ChecklistItemListItemDto[] },
  );

  protected create(): void {
    const label = this.newLabel().trim();
    if (!label) return;

    this.creating.set(true);
    this.api
      .post('/admissions/checklist-items', { label })
      .pipe(
        catchError(() => {
          this.toast.error('No se pudo agregar el documento.');
          return of(null);
        }),
        finalize(() => this.creating.set(false)),
      )
      .subscribe((result) => {
        if (result === null) return;
        this.newLabel.set('');
        this.toast.success('Documento agregado.');
        this.refreshTick.update((n) => n + 1);
      });
  }

  protected toggleActive(item: ChecklistItemListItemDto): void {
    this.busyId.set(item.id);
    this.api
      .patch(`/admissions/checklist-items/${item.id}/active`, { isActive: !item.isActive })
      .pipe(
        catchError(() => {
          this.toast.error('No se pudo actualizar el documento.');
          return of(null);
        }),
        finalize(() => this.busyId.set(null)),
      )
      .subscribe((result) => {
        if (result === null) return;
        this.refreshTick.update((n) => n + 1);
      });
  }

  protected startEdit(item: ChecklistItemListItemDto): void {
    this.editingId.set(item.id);
    this.editingLabel.set(item.label);
  }

  protected cancelEdit(): void {
    this.editingId.set(null);
    this.editingLabel.set('');
  }

  protected saveEdit(item: ChecklistItemListItemDto): void {
    const label = this.editingLabel().trim();
    if (!label) {
      this.toast.error('El nombre del documento es requerido.');
      return;
    }

    this.busyId.set(item.id);
    this.api
      .put(`/admissions/checklist-items/${item.id}`, { label, displayOrder: item.displayOrder })
      .pipe(
        catchError(() => {
          this.toast.error('No se pudo guardar el documento.');
          return of(null);
        }),
        finalize(() => this.busyId.set(null)),
      )
      .subscribe((result) => {
        if (result === null) return;
        this.cancelEdit();
        this.toast.success('Documento actualizado.');
        this.refreshTick.update((n) => n + 1);
      });
  }

  protected move(item: ChecklistItemListItemDto, direction: -1 | 1): void {
    const current = this.items();
    const index = current.findIndex((i) => i.id === item.id);
    const swapIndex = index + direction;
    if (swapIndex < 0 || swapIndex >= current.length) return;

    const other = current[swapIndex];
    if (!other) return;
    this.busyId.set(item.id);

    this.api
      .put(`/admissions/checklist-items/${item.id}`, { label: item.label, displayOrder: other.displayOrder })
      .pipe(
        switchMap(() => this.api.put(`/admissions/checklist-items/${other.id}`, { label: other.label, displayOrder: item.displayOrder })),
        catchError(() => {
          this.toast.error('No se pudo reordenar.');
          return of(null);
        }),
        finalize(() => this.busyId.set(null)),
      )
      .subscribe((result) => {
        if (result === null) return;
        this.refreshTick.update((n) => n + 1);
      });
  }
}
