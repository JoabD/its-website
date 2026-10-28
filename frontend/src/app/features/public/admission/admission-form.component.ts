import { Component, computed, inject, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { toSignal } from '@angular/core/rxjs-interop';
import { of } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { ApiClient } from '../../../core/http/api-client';
import { RecaptchaService } from '../../../core/security/recaptcha.service';
import { RegionListItemDto, SubmitApplicationRequestDto, SubmitApplicationResponseDto } from '../../../api/schema';
import { Modality } from '../../../domain/models';
import { ButtonComponent } from '../../../shared/ui/button/button.component';
import { SubscriberComponent } from '../../../shared/base/subscriber.component';

const RECAPTCHA_ACTION = 'submit_application';

@Component({
  selector: 'shk-admission-form',
  imports: [ReactiveFormsModule, ButtonComponent],
  template: `
    <section class="bg-[var(--shk-color-primary)] py-14 text-center text-white md:py-16">
      <div class="mx-auto max-w-2xl px-4">
        <p class="text-sm font-bold tracking-widest text-[var(--shk-color-accent)]">ADMISIONES</p>
        <h1 class="mt-3 font-[var(--shk-font-heading)] text-3xl font-extrabold md:text-4xl">Solicitud de admisión</h1>
        <p class="mt-3 text-slate-200">Déjanos tus datos y nos pondremos en contacto contigo.</p>
      </div>
    </section>

    <section class="mx-auto max-w-3xl px-4 py-14">
      @if (!sent()) {
        <form
          class="rounded-2xl border border-slate-100 bg-white p-6 shadow-sm md:p-8"
          [formGroup]="form"
          (ngSubmit)="submit()"
          novalidate
        >
          <h2 class="font-[var(--shk-font-heading)] text-lg font-semibold text-[var(--shk-color-primary)]">Datos de contacto</h2>
          <div class="mt-5 grid gap-4 md:grid-cols-2">
            <label class="shk-label md:col-span-2">
              <span>Nombre completo *</span>
              <input formControlName="fullName" autocomplete="name" class="shk-field" [class.shk-field-error]="invalid('fullName')" />
              @if (invalid('fullName')) { <span class="shk-error">Escribe tu nombre completo.</span> }
            </label>
            <label class="shk-label">
              <span>Correo electrónico *</span>
              <input formControlName="email" type="email" autocomplete="email" class="shk-field" [class.shk-field-error]="invalid('email')" />
              @if (invalid('email')) { <span class="shk-error">Ingresa un correo válido.</span> }
            </label>
            <label class="shk-label">
              <span>Teléfono o WhatsApp *</span>
              <input formControlName="phone" type="tel" autocomplete="tel" class="shk-field" [class.shk-field-error]="invalid('phone')" />
              @if (invalid('phone')) { <span class="shk-error">Escribe un teléfono de contacto.</span> }
            </label>
            <label class="shk-label">
              <span>Modalidad de interés *</span>
              <select formControlName="modality" class="shk-field">
                <option value="Onsite">Presencial</option>
                <option value="Online">En línea</option>
                <option value="Diploma">Diplomado</option>
              </select>
            </label>
            @if (form.controls.modality.value === 'Onsite') {
              <label class="shk-label">
                <span>Región o sede *</span>
                <select formControlName="requestedRegionId" class="shk-field" [class.shk-field-error]="showRegionError()">
                  <option value="">Elige una región</option>
                  @for (region of onsiteRegions(); track region.id) { <option [value]="region.id">{{ region.name }}</option> }
                </select>
                @if (showRegionError()) { <span class="shk-error">Selecciona la región o sede de tu preferencia.</span> }
              </label>
            }
          </div>

          <h2 class="mt-8 font-[var(--shk-font-heading)] text-lg font-semibold text-[var(--shk-color-primary)]">
            Para conocerte mejor <span class="text-sm font-normal text-slate-500">(opcional)</span>
          </h2>
          <p class="mt-2 text-sm text-slate-600">
            Consulta los formatos y documentos de inscripción en
            <a
              href="https://drive.google.com/drive/folders/1dn26yWvpntK7KClQ2C1GSYvKVd5ofpHv?usp=sharing"
              target="_blank"
              rel="noopener noreferrer"
              class="font-semibold text-[var(--shk-color-primary)] underline decoration-[var(--shk-color-accent)] underline-offset-2 hover:text-[var(--shk-color-accent-dark)]"
            >esta carpeta de documentos</a>.
          </p>
          <div class="mt-4 grid gap-4 md:grid-cols-2">
            <label class="shk-label"><span>Iglesia</span><input formControlName="churchName" class="shk-field" /></label>
            <label class="shk-label"><span>Área</span><input formControlName="area" class="shk-field" placeholder="Colonia o zona" /></label>
            <label class="shk-label"><span>Comunidad</span><input formControlName="community" class="shk-field" /></label>
            <label class="shk-label"><span>Municipio</span><input formControlName="municipality" autocomplete="address-level2" class="shk-field" /></label>
            <label class="shk-label"><span>Estado</span><input formControlName="state" autocomplete="address-level1" class="shk-field" /></label>
            <label class="shk-label md:col-span-2">
              <span>¿Qué te interesa estudiar o cuál es tu propósito?</span>
              <textarea formControlName="studyPurpose" rows="3" class="shk-field"></textarea>
            </label>
          </div>

          @if (error()) { <p class="mt-4 rounded-lg bg-red-50 px-4 py-2.5 text-sm font-medium text-red-700" role="alert">{{ error() }}</p> }
          <div class="mt-6 flex justify-end">
            <shk-button type="submit" [loading]="submitting()">Enviar solicitud</shk-button>
          </div>
        </form>
      } @else {
        <div class="rounded-2xl border border-emerald-100 bg-emerald-50 p-8 text-center shadow-sm">
          <span class="mx-auto flex h-12 w-12 items-center justify-center rounded-full bg-emerald-100 text-emerald-700">✓</span>
          <h2 class="mt-4 font-[var(--shk-font-heading)] font-semibold text-emerald-800">¡Solicitud enviada!</h2>
          <p class="mt-2 text-sm text-emerald-700">Tu folio es <strong>{{ folio() }}</strong>. Te notificaremos por correo.</p>
        </div>
      }
    </section>
  `,
  styles: [`
    .shk-label { display: flex; flex-direction: column; gap: .375rem; font-size: .8125rem; font-weight: 500; color: #475569; }
    .shk-field { border-radius: .75rem; border: 1px solid #e2e8f0; background: #fff; padding: .625rem 1rem; font-size: .875rem; line-height: 1.25rem; box-shadow: 0 1px 2px rgba(0,0,0,.03); }
    .shk-field:focus { outline: none; border-color: var(--shk-color-accent); box-shadow: 0 0 0 3px rgba(200,162,80,.25); }
    .shk-field-error { border-color: #dc2626; }
    .shk-error { font-size: .75rem; font-weight: 500; color: #dc2626; }
  `],
})
export class AdmissionFormComponent extends SubscriberComponent {
  private readonly fb = inject(NonNullableFormBuilder);
  private readonly api = inject(ApiClient);
  private readonly recaptcha = inject(RecaptchaService);

  protected readonly submitting = signal(false);
  protected readonly sent = signal(false);
  protected readonly error = signal('');
  protected readonly folio = signal('');

  private readonly regions = toSignal(
    this.api.get<RegionListItemDto[]>('/catalog/regions').pipe(catchError(() => of<RegionListItemDto[]>([]))),
    { initialValue: [] as RegionListItemDto[] },
  );
  protected readonly onsiteRegions = computed(() => this.regions().filter((region) => region.modalityScope.includes('Onsite')));

  protected readonly form = this.fb.group({
    fullName: this.fb.control('', Validators.required),
    email: this.fb.control('', [Validators.required, Validators.email]),
    phone: this.fb.control('', Validators.required),
    modality: this.fb.control<Modality>('Onsite'),
    requestedRegionId: this.fb.control(''),
    churchName: this.fb.control(''),
    area: this.fb.control(''),
    community: this.fb.control(''),
    municipality: this.fb.control(''),
    state: this.fb.control(''),
    studyPurpose: this.fb.control(''),
  });

  protected invalid(control: 'fullName' | 'email' | 'phone'): boolean {
    const field = this.form.controls[control];
    return field.invalid && (field.touched || field.dirty);
  }

  protected showRegionError(): boolean {
    const control = this.form.controls.requestedRegionId;
    return this.form.controls.modality.value === 'Onsite' && control.touched && !control.value;
  }

  protected async submit(): Promise<void> {
    if (this.submitting()) return;
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const value = this.form.getRawValue();
    if (value.modality === 'Onsite' && !value.requestedRegionId) {
      this.form.controls.requestedRegionId.markAsTouched();
      return;
    }

    this.submitting.set(true);
    this.error.set('');

    try {
      const recaptchaToken = await this.recaptcha.execute(RECAPTCHA_ACTION);
      const request: SubmitApplicationRequestDto = {
        ...value,
        requestedRegionId: value.modality === 'Onsite' ? value.requestedRegionId : null,
        onlineReason: null,
        recaptchaToken,
      };

      this.subs.sink = this.api.post<SubmitApplicationResponseDto>('/admissions/applications', request).subscribe({
        next: (response) => {
          this.folio.set(response.folio);
          this.sent.set(true);
          this.submitting.set(false);
        },
        error: (err: unknown) => {
          this.submitting.set(false);
          this.error.set((err as { error?: { detail?: string } })?.error?.detail ?? 'No se pudo enviar tu solicitud. Intenta de nuevo.');
        },
      });
    } catch {
      this.submitting.set(false);
      this.error.set('No pudimos verificar que no eres un robot. Revisa tu conexión e intenta de nuevo.');
    }
  }
}
