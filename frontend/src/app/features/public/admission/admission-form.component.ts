import { Component, computed, inject, signal } from '@angular/core';
import { AbstractControl, NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { toSignal } from '@angular/core/rxjs-interop';
import { of } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { ApiClient } from '../../../core/http/api-client';
import { RegionListItemDto, SubmitApplicationRequestDto, SubmitApplicationResponseDto } from '../../../api/schema';
import { Modality, MODALITY_LABELS } from '../../../domain/models';
import { ButtonComponent } from '../../../shared/ui/button/button.component';
import { AdmissionFormModel } from './admission-form.model';

type WizardStage = 'form' | 'summary' | 'confirmation';

interface FormStepDef {
  readonly key: 'personal' | 'address' | 'church' | 'education' | 'modality';
  readonly label: string;
}

/**
 * RN-02/RN-03: modalidades mutuamente excluyentes; región derivada de la modalidad (Onsite ⇒ el
 * solicitante elige; Online/Diploma ⇒ forzada). La validación final SIEMPRE la hace el dominio en
 * el backend — este formulario solo ofrece una buena experiencia, nunca es la frontera de seguridad.
 *
 * Convertido a wizard por pasos (un grupo de campos a la vez, con barra de progreso) para que un
 * formulario de ~30 campos no se sienta abrumador de una sola vista.
 */
@Component({
  selector: 'shk-admission-form',
  imports: [ReactiveFormsModule, ButtonComponent],
  template: `
    <section class="bg-[var(--shk-color-primary)] py-14 text-center text-white md:py-16">
      <div class="mx-auto max-w-2xl px-4">
        <p class="text-sm font-bold tracking-widest text-[var(--shk-color-accent)]">ADMISIONES</p>
        <h1 class="mt-3 font-[var(--shk-font-heading)] text-3xl font-extrabold md:text-4xl">Solicitud de admisión</h1>
        <p class="mt-3 text-slate-200">Completa tus datos y da el primer paso en tu formación ministerial.</p>
      </div>
    </section>

    <section class="mx-auto max-w-3xl px-4 py-14">
      @if (stage() === 'form') {
        <!-- Barra de progreso del wizard -->
        <ol class="mb-10 flex items-center justify-between">
          @for (step of formSteps; track step.key; let i = $index; let last = $last) {
            <li class="flex flex-1 items-center" [class.flex-none]="last">
              <button
                type="button"
                (click)="goToStep(i)"
                [disabled]="i > furthestStepReached()"
                class="flex h-9 w-9 shrink-0 items-center justify-center rounded-full text-sm font-bold transition disabled:cursor-not-allowed"
                [class]="
                  i === formStepIndex()
                    ? 'bg-[var(--shk-color-accent)] text-[var(--shk-color-primary-dark)]'
                    : i < furthestStepReached()
                      ? 'bg-[var(--shk-color-primary)] text-white'
                      : 'bg-slate-200 text-slate-500'
                "
                [attr.aria-current]="i === formStepIndex() ? 'step' : null"
                [attr.aria-label]="'Paso ' + (i + 1) + ': ' + step.label"
              >
                @if (i < furthestStepReached()) {
                  ✓
                } @else {
                  {{ i + 1 }}
                }
              </button>
              @if (!last) {
                <span class="mx-2 h-0.5 flex-1 rounded" [class]="i < furthestStepReached() ? 'bg-[var(--shk-color-primary)]' : 'bg-slate-200'"></span>
              }
            </li>
          }
        </ol>
        <p class="-mt-6 mb-8 text-center text-xs font-semibold uppercase tracking-widest text-[var(--shk-color-accent-dark)]">
          Paso {{ formStepIndex() + 1 }} de {{ formSteps.length }} &middot; {{ currentStep().label }}
        </p>

        <form (ngSubmit)="goNext()">
          @switch (currentStep().key) {
            @case ('personal') {
              <div class="rounded-2xl border border-slate-100 bg-white p-6 shadow-sm md:p-8" [formGroup]="form.personal">
                <h2 class="flex items-center gap-2 font-[var(--shk-font-heading)] font-semibold text-[var(--shk-color-primary)]">
                  <span class="flex h-7 w-7 items-center justify-center rounded-full bg-[var(--shk-color-accent)]/15 text-xs font-bold text-[var(--shk-color-accent-dark)]">1</span>
                  Datos personales
                </h2>
                <div class="mt-5 grid gap-4 md:grid-cols-2">
                  <label class="shk-label md:col-span-2">
                    <span>Nombre completo</span>
                    <input formControlName="fullName" placeholder="Nombre y apellidos" class="shk-field" [class.shk-field-error]="invalid(form.personal.controls.fullName)" />
                    @if (invalid(form.personal.controls.fullName)) {
                      <span class="shk-error">Este campo es obligatorio.</span>
                    }
                  </label>
                  <label class="shk-label">
                    <span>Fecha de nacimiento</span>
                    <input formControlName="birthDate" type="date" class="shk-field" [class.shk-field-error]="invalid(form.personal.controls.birthDate)" />
                    @if (invalid(form.personal.controls.birthDate)) {
                      <span class="shk-error">Este campo es obligatorio.</span>
                    }
                  </label>
                  <label class="shk-label">
                    <span>Estado civil</span>
                    <input formControlName="maritalStatus" placeholder="Ej. Casado(a)" class="shk-field" />
                  </label>
                  <label class="shk-label">
                    <span>Correo electrónico</span>
                    <input formControlName="email" type="email" placeholder="correo@ejemplo.com" class="shk-field" [class.shk-field-error]="invalid(form.personal.controls.email)" />
                    @if (invalid(form.personal.controls.email)) {
                      <span class="shk-error">Ingresa un correo válido.</span>
                    }
                  </label>
                  <label class="shk-label">
                    <span>Teléfono</span>
                    <input formControlName="phone" placeholder="10 dígitos" class="shk-field" [class.shk-field-error]="invalid(form.personal.controls.phone)" />
                    @if (invalid(form.personal.controls.phone)) {
                      <span class="shk-error">Este campo es obligatorio.</span>
                    }
                  </label>
                </div>
              </div>
            }

            @case ('address') {
              <div class="rounded-2xl border border-slate-100 bg-white p-6 shadow-sm md:p-8" [formGroup]="form.address">
                <h2 class="flex items-center gap-2 font-[var(--shk-font-heading)] font-semibold text-[var(--shk-color-primary)]">
                  <span class="flex h-7 w-7 items-center justify-center rounded-full bg-[var(--shk-color-accent)]/15 text-xs font-bold text-[var(--shk-color-accent-dark)]">2</span>
                  Domicilio
                </h2>
                <div class="mt-5 grid gap-4 md:grid-cols-2">
                  <label class="shk-label md:col-span-2">
                    <span>Calle y número</span>
                    <input formControlName="street" class="shk-field" [class.shk-field-error]="invalid(form.address.controls.street)" />
                    @if (invalid(form.address.controls.street)) {
                      <span class="shk-error">Este campo es obligatorio.</span>
                    }
                  </label>
                  <label class="shk-label">
                    <span>Colonia</span>
                    <input formControlName="neighborhood" class="shk-field" />
                  </label>
                  <label class="shk-label">
                    <span>Localidad</span>
                    <input formControlName="locality" class="shk-field" />
                  </label>
                  <label class="shk-label">
                    <span>Municipio</span>
                    <input formControlName="municipality" class="shk-field" [class.shk-field-error]="invalid(form.address.controls.municipality)" />
                    @if (invalid(form.address.controls.municipality)) {
                      <span class="shk-error">Este campo es obligatorio.</span>
                    }
                  </label>
                  <label class="shk-label">
                    <span>Estado</span>
                    <input formControlName="state" class="shk-field" />
                  </label>
                </div>
              </div>
            }

            @case ('church') {
              <div class="rounded-2xl border border-slate-100 bg-white p-6 shadow-sm md:p-8" [formGroup]="form.church">
                <h2 class="flex items-center gap-2 font-[var(--shk-font-heading)] font-semibold text-[var(--shk-color-primary)]">
                  <span class="flex h-7 w-7 items-center justify-center rounded-full bg-[var(--shk-color-accent)]/15 text-xs font-bold text-[var(--shk-color-accent-dark)]">3</span>
                  Datos eclesiásticos
                </h2>
                <div class="mt-5 grid gap-4 md:grid-cols-2">
                  <label class="shk-label md:col-span-2">
                    <span>Nombre de la iglesia</span>
                    <input formControlName="churchName" class="shk-field" [class.shk-field-error]="invalid(form.church.controls.churchName)" />
                    @if (invalid(form.church.controls.churchName)) {
                      <span class="shk-error">Este campo es obligatorio.</span>
                    }
                  </label>
                  <label class="shk-label md:col-span-2">
                    <span>Calle y número (iglesia)</span>
                    <input formControlName="churchStreet" class="shk-field" [class.shk-field-error]="invalid(form.church.controls.churchStreet)" />
                    @if (invalid(form.church.controls.churchStreet)) {
                      <span class="shk-error">Este campo es obligatorio.</span>
                    }
                  </label>
                  <label class="shk-label">
                    <span>Colonia (iglesia)</span>
                    <input formControlName="churchNeighborhood" class="shk-field" />
                  </label>
                  <label class="shk-label">
                    <span>Localidad (iglesia)</span>
                    <input formControlName="churchLocality" class="shk-field" />
                  </label>
                  <label class="shk-label">
                    <span>Municipio (iglesia)</span>
                    <input formControlName="churchMunicipality" class="shk-field" [class.shk-field-error]="invalid(form.church.controls.churchMunicipality)" />
                    @if (invalid(form.church.controls.churchMunicipality)) {
                      <span class="shk-error">Este campo es obligatorio.</span>
                    }
                  </label>
                  <label class="shk-label">
                    <span>Nombre del pastor</span>
                    <input formControlName="pastorName" class="shk-field" [class.shk-field-error]="invalid(form.church.controls.pastorName)" />
                    @if (invalid(form.church.controls.pastorName)) {
                      <span class="shk-error">Este campo es obligatorio.</span>
                    }
                  </label>
                  <label class="shk-label">
                    <span>Tiempo de congregarse</span>
                    <input formControlName="timeAttending" class="shk-field" />
                  </label>
                  <label class="flex items-center gap-2 text-sm text-slate-700 md:col-span-2">
                    <input type="checkbox" formControlName="hasMinistryRole" class="h-4 w-4 rounded border-slate-300 text-[var(--shk-color-accent-dark)] focus:ring-[var(--shk-color-accent)]" />
                    Tengo un cargo ministerial
                  </label>
                  @if (form.church.controls.hasMinistryRole.value) {
                    <label class="shk-label md:col-span-2">
                      <span>¿Cuál cargo?</span>
                      <input formControlName="ministryRoleName" class="shk-field" />
                    </label>
                  }
                </div>
              </div>
            }

            @case ('education') {
              <div class="rounded-2xl border border-slate-100 bg-white p-6 shadow-sm md:p-8" [formGroup]="form.education">
                <h2 class="flex items-center gap-2 font-[var(--shk-font-heading)] font-semibold text-[var(--shk-color-primary)]">
                  <span class="flex h-7 w-7 items-center justify-center rounded-full bg-[var(--shk-color-accent)]/15 text-xs font-bold text-[var(--shk-color-accent-dark)]">4</span>
                  Formación
                </h2>
                <div class="mt-5 grid gap-4 md:grid-cols-2">
                  <label class="shk-label">
                    <span>Escolaridad</span>
                    <select formControlName="level" class="shk-field">
                      <option value="Primary">Primaria</option>
                      <option value="Secondary">Secundaria</option>
                      <option value="HighSchool">Bachillerato</option>
                      <option value="Other">Otra</option>
                    </select>
                  </label>
                  @if (form.education.controls.level.value === 'Other') {
                    <label class="shk-label">
                      <span>Especifique su escolaridad</span>
                      <input formControlName="otherDescription" class="shk-field" />
                    </label>
                  }
                  <label class="shk-label md:col-span-2">
                    <span>Formación teológica previa (opcional)</span>
                    <input formControlName="theologicalBackground" class="shk-field" />
                  </label>
                  <label class="shk-label md:col-span-2">
                    <span>¿Cuál es tu propósito al estudiar aquí?</span>
                    <textarea formControlName="studyPurpose" class="shk-field" rows="3" [class.shk-field-error]="invalid(form.education.controls.studyPurpose)"></textarea>
                    @if (invalid(form.education.controls.studyPurpose)) {
                      <span class="shk-error">Este campo es obligatorio.</span>
                    }
                  </label>
                </div>
              </div>
            }

            @case ('modality') {
              <div class="rounded-2xl border border-slate-100 bg-white p-6 shadow-sm md:p-8" [formGroup]="form.modality">
                <h2 class="flex items-center gap-2 font-[var(--shk-font-heading)] font-semibold text-[var(--shk-color-primary)]">
                  <span class="flex h-7 w-7 items-center justify-center rounded-full bg-[var(--shk-color-accent)]/15 text-xs font-bold text-[var(--shk-color-accent-dark)]">5</span>
                  Modalidad
                </h2>
                <div class="mt-5 grid gap-3 sm:grid-cols-3">
                  @for (option of modalityOptions; track option) {
                    <label
                      class="cursor-pointer rounded-xl border px-4 py-3 text-center text-sm font-medium transition"
                      [class]="form.modality.controls.modality.value === option
                        ? 'border-[var(--shk-color-accent)] bg-[var(--shk-color-accent)]/10 text-[var(--shk-color-primary)]'
                        : 'border-slate-200 text-slate-600 hover:border-slate-300'"
                    >
                      <input type="radio" formControlName="modality" [value]="option" class="sr-only" />
                      {{ modalityLabel(option) }}
                    </label>
                  }
                </div>

                @if (form.modality.controls.modality.value === 'Onsite') {
                  <label class="shk-label mt-4">
                    <span>Elige tu región</span>
                    <select formControlName="requestedRegionId" class="shk-field">
                      <option value="" disabled>Elige tu región</option>
                      @for (region of onsiteRegions(); track region.id) {
                        <option [value]="region.id">{{ region.name }}</option>
                      }
                    </select>
                  </label>
                }

                @if (form.modality.controls.modality.value === 'Online') {
                  <label class="shk-label mt-4">
                    <span>¿Por qué eliges la modalidad virtual?</span>
                    <textarea formControlName="onlineReason" class="shk-field" rows="2"></textarea>
                  </label>
                }
              </div>
            }
          }

          <div class="mt-6 flex items-center justify-between gap-3">
            <shk-button
              type="button"
              variant="secondary"
              (click)="goBack()"
              [class.invisible]="formStepIndex() === 0"
            >
              Atrás
            </shk-button>
            <shk-button type="submit">
              {{ formStepIndex() === formSteps.length - 1 ? 'Revisar mi solicitud' : 'Continuar' }}
            </shk-button>
          </div>
        </form>
      }

      @if (stage() === 'summary') {
        <div class="rounded-2xl border border-slate-100 bg-white p-8 shadow-sm">
          <h2 class="font-[var(--shk-font-heading)] font-semibold text-[var(--shk-color-primary)]">Resumen de tu solicitud</h2>
          <p class="mt-3 text-sm text-slate-600">
            <strong class="text-slate-800">{{ form.personal.controls.fullName.value }}</strong>
            — {{ modalityLabel(form.modality.controls.modality.value) }}
          </p>
          <p class="mt-1 text-sm text-slate-600">{{ form.personal.controls.email.value }}</p>
          <p class="mt-1 text-sm text-slate-600">{{ form.personal.controls.phone.value }}</p>
          <p class="mt-1 text-sm text-slate-600">{{ form.church.controls.churchName.value }}</p>
          <div class="mt-6 flex gap-3">
            <shk-button variant="secondary" (click)="editFromSummary()">Editar</shk-button>
            <shk-button [loading]="submitting()" (click)="submit()">Confirmar y enviar</shk-button>
          </div>
        </div>
      }

      @if (stage() === 'confirmation') {
        <div class="rounded-2xl border border-emerald-100 bg-emerald-50 p-8 text-center shadow-sm">
          <span class="mx-auto flex h-12 w-12 items-center justify-center rounded-full bg-emerald-100 text-emerald-700">✓</span>
          <h2 class="mt-4 font-[var(--shk-font-heading)] font-semibold text-emerald-800">¡Solicitud enviada!</h2>
          <p class="mt-2 text-sm text-emerald-700">Tu folio es <strong>{{ folio() }}</strong>. Te notificaremos por correo el resultado.</p>
        </div>
      }
    </section>
  `,
  styles: [
    `
    .shk-label {
      display: flex;
      flex-direction: column;
      gap: 0.375rem;
      font-size: 0.8125rem;
      font-weight: 500;
      color: #475569;
    }
    .shk-field {
      border-radius: 0.75rem;
      border: 1px solid #e2e8f0;
      background: #fff;
      padding: 0.625rem 1rem;
      font-size: 0.875rem;
      line-height: 1.25rem;
      box-shadow: 0 1px 2px rgba(0,0,0,0.03);
      transition: border-color 0.15s, box-shadow 0.15s;
    }
    .shk-field:focus {
      outline: none;
      border-color: var(--shk-color-accent);
      box-shadow: 0 0 0 3px rgba(200,162,80,0.25);
    }
    .shk-field-error {
      border-color: #dc2626;
    }
    .shk-error {
      font-size: 0.75rem;
      font-weight: 500;
      color: #dc2626;
    }
    `,
  ],
})
export class AdmissionFormComponent {
  private readonly fb = inject(NonNullableFormBuilder);
  private readonly api = inject(ApiClient);

  protected readonly stage = signal<WizardStage>('form');
  protected readonly submitting = signal(false);
  protected readonly folio = signal('');

  protected readonly formSteps: readonly FormStepDef[] = [
    { key: 'personal', label: 'Datos personales' },
    { key: 'address', label: 'Domicilio' },
    { key: 'church', label: 'Datos eclesiásticos' },
    { key: 'education', label: 'Formación' },
    { key: 'modality', label: 'Modalidad' },
  ];

  protected readonly formStepIndex = signal(0);
  private readonly maxStepReached = signal(0);
  protected readonly furthestStepReached = computed(() => this.maxStepReached());
  protected readonly currentStep = computed<FormStepDef>(
    () => (this.formSteps[this.formStepIndex()] ?? this.formSteps[0]) as FormStepDef,
  );

  protected readonly modalityOptions: readonly Modality[] = ['Onsite', 'Online', 'Diploma'];

  private readonly regions = toSignal(
    this.api.get<RegionListItemDto[]>('/catalog/regions').pipe(catchError(() => of<RegionListItemDto[]>([]))),
    { initialValue: [] as RegionListItemDto[] },
  );

  protected readonly onsiteRegions = computed(() => this.regions().filter((r) => r.modalityScope.includes('Onsite')));

  protected readonly form: AdmissionFormModel = {
    personal: this.fb.group({
      fullName: this.fb.control('', Validators.required),
      birthDate: this.fb.control('', Validators.required),
      maritalStatus: this.fb.control(''),
      email: this.fb.control('', [Validators.required, Validators.email]),
      phone: this.fb.control('', Validators.required),
    }),
    address: this.fb.group({
      street: this.fb.control('', Validators.required),
      neighborhood: this.fb.control(''),
      locality: this.fb.control(''),
      municipality: this.fb.control('', Validators.required),
      state: this.fb.control(''),
    }),
    church: this.fb.group({
      churchName: this.fb.control('', Validators.required),
      churchStreet: this.fb.control('', Validators.required),
      churchNeighborhood: this.fb.control(''),
      churchLocality: this.fb.control(''),
      churchMunicipality: this.fb.control('', Validators.required),
      pastorName: this.fb.control('', Validators.required),
      timeAttending: this.fb.control(''),
      hasMinistryRole: this.fb.control(false),
      ministryRoleName: this.fb.control(''),
    }),
    education: this.fb.group({
      level: this.fb.control<'Primary' | 'Secondary' | 'HighSchool' | 'Other'>('HighSchool'),
      otherDescription: this.fb.control(''),
      theologicalBackground: this.fb.control(''),
      studyPurpose: this.fb.control('', Validators.required),
    }),
    modality: this.fb.group({
      modality: this.fb.control<Modality>('Onsite'),
      requestedRegionId: this.fb.control(''),
      onlineReason: this.fb.control(''),
    }),
  };

  protected modalityLabel(modality: Modality): string {
    return MODALITY_LABELS[modality];
  }

  protected invalid(control: AbstractControl): boolean {
    return control.invalid && (control.touched || control.dirty);
  }

  private currentGroup() {
    return this.form[this.currentStep().key];
  }

  /** Avanza al siguiente paso solo si el grupo del paso actual es válido; si no, marca los campos. */
  protected goNext(): void {
    const group = this.currentGroup();
    if (group.invalid) {
      group.markAllAsTouched();
      return;
    }

    if (this.formStepIndex() < this.formSteps.length - 1) {
      const next = this.formStepIndex() + 1;
      this.formStepIndex.set(next);
      this.maxStepReached.update((max) => Math.max(max, next));
      window.scrollTo({ top: 0, behavior: 'smooth' });
    } else {
      this.stage.set('summary');
    }
  }

  protected goBack(): void {
    if (this.formStepIndex() === 0) return;
    this.formStepIndex.update((i) => i - 1);
    window.scrollTo({ top: 0, behavior: 'smooth' });
  }

  protected goToStep(index: number): void {
    if (index > this.furthestStepReached()) return;
    this.formStepIndex.set(index);
    window.scrollTo({ top: 0, behavior: 'smooth' });
  }

  protected editFromSummary(): void {
    this.stage.set('form');
    this.formStepIndex.set(0);
  }

  protected submit(): void {
    this.submitting.set(true);

    const request: SubmitApplicationRequestDto = {
      fullName: this.form.personal.controls.fullName.value,
      birthDate: this.form.personal.controls.birthDate.value,
      maritalStatus: this.form.personal.controls.maritalStatus.value,
      email: this.form.personal.controls.email.value,
      phone: this.form.personal.controls.phone.value,
      street: this.form.address.controls.street.value,
      neighborhood: this.form.address.controls.neighborhood.value,
      locality: this.form.address.controls.locality.value,
      municipality: this.form.address.controls.municipality.value,
      state: this.form.address.controls.state.value,
      churchName: this.form.church.controls.churchName.value,
      churchStreet: this.form.church.controls.churchStreet.value,
      churchNeighborhood: this.form.church.controls.churchNeighborhood.value,
      churchLocality: this.form.church.controls.churchLocality.value,
      churchMunicipality: this.form.church.controls.churchMunicipality.value,
      pastorName: this.form.church.controls.pastorName.value,
      timeAttending: this.form.church.controls.timeAttending.value,
      hasMinistryRole: this.form.church.controls.hasMinistryRole.value,
      ministryRoleName: this.form.church.controls.ministryRoleName.value || null,
      educationLevel: this.form.education.controls.level.value,
      otherEducationDescription: this.form.education.controls.otherDescription.value || null,
      theologicalBackground: this.form.education.controls.theologicalBackground.value,
      studyPurpose: this.form.education.controls.studyPurpose.value,
      modality: this.form.modality.controls.modality.value,
      requestedRegionId: this.form.modality.controls.requestedRegionId.value || null,
      onlineReason: this.form.modality.controls.onlineReason.value || null,
    };

    this.api.post<SubmitApplicationResponseDto>('/admissions/applications', request).subscribe({
      next: (response) => {
        this.folio.set(response.folio);
        this.stage.set('confirmation');
        this.submitting.set(false);
      },
      error: () => this.submitting.set(false),
    });
  }
}
