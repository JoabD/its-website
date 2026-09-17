import { Component, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { of } from 'rxjs';
import { catchError, switchMap, tap } from 'rxjs/operators';
import { ApiClient } from '../../../core/http/api-client';
import { AnnouncementListItemDto } from '../../../api/schema';
import { AuthStore } from '../../../core/auth/auth.store';
import { CardComponent } from '../../../shared/ui/card/card.component';
import { ButtonComponent } from '../../../shared/ui/button/button.component';
import { ToastService } from '../../../shared/ui/toast/toast.service';

/**
 * Plan de control escolar, fase 6: avisos institucionales. Publicarlos es EXCLUSIVO de cuentas
 * maestras (Administrator) — el formulario de creación solo se muestra a ese rol; el backend
 * revalida con [RequireRole(Administrator)] (nunca confiar solo en ocultar el botón). Cualquier
 * persona autenticada puede leerlos.
 */
@Component({
  selector: 'shk-announcements',
  imports: [CardComponent, ButtonComponent, ReactiveFormsModule, DatePipe],
  template: `
    <h1 class="text-2xl font-bold text-slate-900">Avisos</h1>

    @if (auth.role() === 'Administrator') {
      <shk-card class="mt-6">
        <form [formGroup]="form" class="space-y-3" (ngSubmit)="submit()">
          <label class="flex flex-col gap-1.5 text-sm">
            <span class="font-medium text-slate-700">Título</span>
            <input formControlName="title" type="text" class="shk-field" placeholder="Ej. Suspensión de clases" />
          </label>
          <label class="flex flex-col gap-1.5 text-sm">
            <span class="font-medium text-slate-700">Contenido</span>
            <textarea formControlName="body" rows="4" class="shk-field" placeholder="Detalle del aviso…"></textarea>
          </label>
          <p class="text-xs text-slate-500">Se enviará por correo a todos los docentes activos.</p>
          <shk-button type="submit" [loading]="publishing()" [disabled]="form.invalid">Publicar aviso</shk-button>
        </form>
      </shk-card>
    }

    <div class="mt-6 space-y-4">
      @for (item of announcements(); track item.id) {
        <shk-card>
          <div class="flex items-start justify-between gap-4">
            <h2 class="font-semibold text-slate-900">{{ item.title }}</h2>
            <span class="shrink-0 text-xs text-slate-400">{{ item.publishedAtUtc | date: 'dd/MM/yyyy HH:mm' }}</span>
          </div>
          <p class="mt-2 whitespace-pre-line text-sm text-slate-600">{{ item.body }}</p>
        </shk-card>
      } @empty {
        <p class="py-6 text-center text-slate-400">Sin avisos publicados todavía.</p>
      }
    </div>
  `,
  styles: [
    `
    .shk-field {
      border-radius: 0.5rem;
      border: 1px solid #e2e8f0;
      background: #fff;
      padding: 0.5rem 0.75rem;
      font-size: 0.875rem;
      width: 100%;
    }
    .shk-field:focus { outline: none; border-color: var(--shk-color-accent); }
    `,
  ],
})
export class AnnouncementsComponent {
  private readonly fb = inject(NonNullableFormBuilder);
  private readonly api = inject(ApiClient);
  private readonly toast = inject(ToastService);
  protected readonly auth = inject(AuthStore);

  protected readonly publishing = signal(false);
  private readonly refreshTick = signal(0);

  protected readonly form = this.fb.group({
    title: this.fb.control('', [Validators.required, Validators.maxLength(160)]),
    body: this.fb.control('', Validators.required),
  });

  protected readonly announcements = toSignal(
    toObservable(this.refreshTick).pipe(
      switchMap(() =>
        this.api
          .get<AnnouncementListItemDto[]>('/announcements', { limit: 30 })
          .pipe(catchError(() => of<AnnouncementListItemDto[]>([]))),
      ),
    ),
    { initialValue: [] as AnnouncementListItemDto[] },
  );

  protected submit(): void {
    if (this.form.invalid) return;
    this.publishing.set(true);

    this.api.post('/announcements', this.form.getRawValue()).pipe(
      tap({
        next: () => {
          this.toast.success('Aviso publicado y enviado a los docentes.');
          this.form.reset({ title: '', body: '' });
          this.refreshTick.update((n) => n + 1);
        },
        error: () => this.toast.error('No se pudo publicar el aviso.'),
      }),
      catchError(() => of(null)),
    ).subscribe(() => this.publishing.set(false));
  }
}
