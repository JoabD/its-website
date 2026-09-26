import { Component, ElementRef, effect, inject, input, output, signal, viewChild } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { of } from 'rxjs';
import { catchError, map } from 'rxjs/operators';
import { ApiClient } from '../../../core/http/api-client';
import {
  CreateStudentRequestDto,
  CreateStudentResponseDto,
  ImportStudentsResponseDto,
  ModalityDto,
  RegionListItemDto,
  StudentImportTemplateResponseDto,
  StudyPlanDto,
} from '../../../api/schema';
import { ButtonComponent } from '../../../shared/ui/button/button.component';
import { InputComponent } from '../../../shared/ui/input/input.component';
import { ToastService } from '../../../shared/ui/toast/toast.service';

type AddStudentTab = 'manual' | 'excel';

/**
 * Alumnos → "Agregar alumno": drawer con dos formas de alta (RN de producto, no vienen de una
 * solicitud pública): manual (formulario directo, con Plan y cuatrimestre/semestre a elegir) o
 * masiva por Excel/CSV (mismo patrón de reporte fila-a-fila que Pagos → Importar). Estructura
 * calcada de ApplicationReviewDrawerComponent (encabezado/cuerpo/pie fijos, Esc cierra, foco
 * regresa a quien abrió el drawer) para mantener consistencia visual entre ambos drawers.
 */
@Component({
  selector: 'shk-add-student-drawer',
  imports: [ButtonComponent, InputComponent, FormsModule],
  template: `
    @if (open()) {
      <div class="fixed inset-0 z-40 flex justify-end" (keydown.escape)="close.emit()">
        <div class="absolute inset-0 bg-slate-900/40 backdrop-blur-[2px]" (click)="close.emit()"></div>

        <aside
          #panel
          class="relative flex h-full w-full max-w-xl flex-col bg-[var(--shk-color-surface)] shadow-2xl focus:outline-none"
          role="dialog"
          aria-modal="true"
          tabindex="-1"
        >
          <header class="flex items-start justify-between gap-4 border-b border-slate-100 px-6 py-5">
            <div>
              <h2 class="text-xl font-bold text-slate-900">Agregar alumno</h2>
              <p class="mt-1 text-sm text-slate-500">Alta manual — no pasa por el flujo de solicitud pública.</p>
            </div>
            <button
              type="button"
              class="rounded-full p-2 text-slate-400 transition hover:bg-slate-100 hover:text-slate-600"
              aria-label="Cerrar"
              (click)="close.emit()"
            >
              <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" class="h-5 w-5">
                <path stroke-linecap="round" stroke-linejoin="round" d="M6 18 18 6M6 6l12 12" />
              </svg>
            </button>
          </header>

          <div class="flex gap-1 border-b border-slate-100 px-6 pt-3">
            <button
              type="button"
              class="rounded-t-lg px-4 py-2 text-sm font-semibold transition"
              [class]="tab() === 'manual' ? 'bg-[var(--shk-color-accent)]/10 text-[var(--shk-color-primary)]' : 'text-slate-500 hover:text-slate-700'"
              (click)="tab.set('manual')"
            >
              Formulario manual
            </button>
            <button
              type="button"
              class="rounded-t-lg px-4 py-2 text-sm font-semibold transition"
              [class]="tab() === 'excel' ? 'bg-[var(--shk-color-accent)]/10 text-[var(--shk-color-primary)]' : 'text-slate-500 hover:text-slate-700'"
              (click)="tab.set('excel')"
            >
              Importar Excel
            </button>
          </div>

          <div class="flex-1 overflow-y-auto px-6 py-5">
            @if (tab() === 'manual') {
              <div class="space-y-4">
                <shk-input label="Nombre completo" [required]="true" [ngModel]="manualFullName()" (ngModelChange)="manualFullName.set($event)" [ngModelOptions]="{standalone: true}" />
                <shk-input label="Correo (opcional)" type="email" [ngModel]="manualEmail()" (ngModelChange)="manualEmail.set($event)" [ngModelOptions]="{standalone: true}" />
                <p class="-mt-2.5 text-xs text-slate-400">Si no lo capturas, el alumno queda registrado pero sin acceso al sistema por ahora.</p>
                <shk-input label="Teléfono (opcional)" [ngModel]="manualPhone()" (ngModelChange)="manualPhone.set($event)" [ngModelOptions]="{standalone: true}" />
                <p class="-mt-2.5 text-xs text-slate-400">Si no lo capturas, el alumno queda registrado pero sin la opción de "enviar por WhatsApp".</p>
                <shk-input label="Fecha de nacimiento" type="date" [required]="true" [ngModel]="manualBirthDate()" (ngModelChange)="manualBirthDate.set($event)" [ngModelOptions]="{standalone: true}" />

                <label class="flex flex-col gap-1.5 text-sm">
                  <span class="font-medium text-slate-700">Región <span class="text-[var(--shk-color-accent-dark)]">*</span></span>
                  <!-- BUG REAL encontrado (mismo caso que add-user-drawer): [value]/(change) planos en
                       un <select> con <option> generadas por @for pueden desincronizarse y seguir
                       enviando el valor inicial del signal sin importar qué se elija en pantalla.
                       [ngModel]/(ngModelChange) usa el SelectControlValueAccessor de Angular, que sí
                       mantiene la selección sincronizada de forma confiable. -->
                  <select
                    class="shk-field"
                    [ngModel]="manualRegionId()"
                    (ngModelChange)="manualRegionId.set($event)"
                    [ngModelOptions]="{standalone: true}"
                    name="manualRegionId"
                  >
                    <option value="">Selecciona una región…</option>
                    @for (region of regions(); track region.id) {
                      <option [value]="region.id">{{ region.name }}</option>
                    }
                  </select>
                </label>

                <label class="flex flex-col gap-1.5 text-sm">
                  <span class="font-medium text-slate-700">Modalidad <span class="text-[var(--shk-color-accent-dark)]">*</span></span>
                  <select class="shk-field" [ngModel]="manualModality()" (ngModelChange)="manualModality.set($event)" [ngModelOptions]="{standalone: true}" name="manualModality">
                    <option value="Onsite">Presencial</option>
                    <option value="Online">Virtual</option>
                    <option value="Diploma">Diplomado</option>
                  </select>
                </label>

                <label class="flex flex-col gap-1.5 text-sm">
                  <span class="font-medium text-slate-700">Plan <span class="text-[var(--shk-color-accent-dark)]">*</span></span>
                  <select class="shk-field" [ngModel]="manualPlan()" (ngModelChange)="manualPlan.set($event)" [ngModelOptions]="{standalone: true}" name="manualPlan">
                    <option value="Quarterly">Cuatrimestral</option>
                    <option value="Semester">Semestral</option>
                  </select>
                </label>

                <label class="flex flex-col gap-1.5 text-sm">
                  <span class="font-medium text-slate-700">
                    {{ manualPlan() === 'Semester' ? 'Semestre' : 'Cuatrimestre' }} actual <span class="text-[var(--shk-color-accent-dark)]">*</span>
                  </span>
                  <select class="shk-field" [ngModel]="manualCurrentTerm()" (ngModelChange)="manualCurrentTerm.set(+$event)" [ngModelOptions]="{standalone: true}" name="manualCurrentTerm">
                    @for (n of termOptions(); track n) {
                      <option [value]="n">{{ n }}</option>
                    }
                  </select>
                </label>

                @if (manualError()) {
                  <p class="text-sm text-red-600">{{ manualError() }}</p>
                }
              </div>
            } @else {
              <div class="space-y-4">
                <div class="flex flex-wrap items-center justify-between gap-3 rounded-xl bg-slate-50 p-4">
                  <p class="text-sm text-slate-600">
                    Usa la plantilla oficial para asegurar que las columnas y valores sean los esperados. Las columnas CORREO y TELEFONO son
                    opcionales — sin correo, el alumno queda registrado pero sin acceso al sistema por ahora; sin teléfono, se queda sin la
                    opción de "enviar por WhatsApp".
                  </p>
                  <button
                    type="button"
                    class="flex items-center gap-2 whitespace-nowrap rounded-full border border-slate-200 bg-white px-4 py-2 text-sm font-semibold text-[var(--shk-color-primary)] transition hover:border-[var(--shk-color-accent)] disabled:cursor-not-allowed disabled:opacity-50"
                    [disabled]="downloadingTemplate()"
                    (click)="downloadTemplate()"
                  >
                    <i class="bi bi-download"></i>
                    {{ downloadingTemplate() ? 'Descargando…' : 'Descargar plantilla' }}
                  </button>
                </div>

                @if (templateError()) {
                  <p class="text-sm text-red-600">{{ templateError() }}</p>
                }

                <div class="rounded-xl border border-dashed border-slate-300 p-4">
                  <input
                    #fileInput
                    type="file"
                    accept=".xlsx,.csv"
                    class="block w-full text-sm text-slate-600 file:mr-3 file:rounded-full file:border-0 file:bg-[var(--shk-color-accent)]/15 file:px-4 file:py-2 file:text-sm file:font-semibold file:text-[var(--shk-color-primary)] hover:file:bg-[var(--shk-color-accent)]/25"
                    (change)="onFileSelected($any($event.target).files)"
                  />
                  @if (selectedFileName()) {
                    <p class="mt-2 text-xs text-slate-500">Archivo seleccionado: {{ selectedFileName() }}</p>
                  }
                </div>

                @if (importError()) {
                  <p class="text-sm text-red-600">{{ importError() }}</p>
                }

                @if (importResult(); as result) {
                  <div class="rounded-xl bg-slate-50 p-4">
                    <p class="text-sm font-semibold text-slate-800">
                      {{ result.importedRows }} de {{ result.totalRows }} filas importadas.
                    </p>
                    @if (result.errors.length > 0) {
                      <ul class="mt-3 max-h-56 space-y-2 overflow-y-auto text-xs text-slate-600">
                        @for (err of result.errors; track err.rowNumber) {
                          <li class="rounded-lg bg-white p-2 shadow-sm">
                            <span class="font-semibold text-red-600">Fila {{ err.rowNumber }}:</span> {{ err.message }}
                          </li>
                        }
                      </ul>
                    }
                  </div>
                }
              </div>
            }
          </div>

          <footer class="flex flex-col-reverse gap-3 border-t border-slate-100 px-6 py-4 sm:flex-row sm:items-center sm:justify-end">
            <shk-button variant="ghost" (click)="close.emit()">Cancelar</shk-button>
            @if (tab() === 'manual') {
              <shk-button variant="primary" [loading]="submitting()" (click)="submitManual()">Agregar alumno</shk-button>
            } @else {
              <shk-button variant="primary" [loading]="submitting()" [disabled]="!selectedFile()" (click)="submitImport()">
                Importar
              </shk-button>
            }
          </footer>
        </aside>
      </div>
    }
  `,
  styles: [
    `
    .shk-field {
      border-radius: 0.75rem;
      border: 1px solid #e2e8f0;
      background: #fff;
      padding: 0.625rem 1rem;
      font-size: 0.875rem;
    }
    .shk-field:focus { outline: none; border-color: var(--shk-color-accent); box-shadow: 0 0 0 2px color-mix(in srgb, var(--shk-color-accent) 30%, transparent); }
    `,
  ],
})
export class AddStudentDrawerComponent {
  private readonly api = inject(ApiClient);
  private readonly toast = inject(ToastService);
  private readonly panelRef = viewChild<ElementRef<HTMLElement>>('panel');

  readonly open = input<boolean>(false);
  readonly close = output<void>();
  readonly added = output<void>();

  protected readonly tab = signal<AddStudentTab>('manual');
  protected readonly submitting = signal(false);

  protected readonly manualFullName = signal('');
  protected readonly manualEmail = signal('');
  protected readonly manualPhone = signal('');
  protected readonly manualBirthDate = signal('');
  protected readonly manualRegionId = signal('');
  protected readonly manualModality = signal<ModalityDto>('Onsite');
  protected readonly manualPlan = signal<StudyPlanDto>('Quarterly');
  protected readonly manualCurrentTerm = signal(1);
  protected readonly manualError = signal<string | null>(null);

  protected readonly selectedFile = signal<File | null>(null);
  protected readonly selectedFileName = signal<string | null>(null);
  protected readonly importResult = signal<ImportStudentsResponseDto | null>(null);
  protected readonly importError = signal<string | null>(null);

  protected readonly downloadingTemplate = signal(false);
  protected readonly templateError = signal<string | null>(null);

  protected readonly termOptions = () => Array.from({ length: 6 }, (_, i) => i + 1);

  private readonly regionsResource = toSignal(
    this.api.get<RegionListItemDto[]>('/catalog/regions').pipe(catchError(() => of<RegionListItemDto[]>([]))),
    { initialValue: [] as RegionListItemDto[] },
  );
  protected readonly regions = () => this.regionsResource();

  private lastFocusedElement: HTMLElement | null = null;

  constructor() {
    effect(() => {
      const isOpen = this.open();
      if (!isOpen) {
        if (this.lastFocusedElement) {
          this.lastFocusedElement.focus();
          this.lastFocusedElement = null;
        }
        return;
      }

      this.lastFocusedElement = (document.activeElement as HTMLElement) ?? null;
      this.resetForm();
      queueMicrotask(() => this.panelRef()?.nativeElement.focus());
    });
  }

  private resetForm(): void {
    this.tab.set('manual');
    this.manualFullName.set('');
    this.manualEmail.set('');
    this.manualPhone.set('');
    this.manualBirthDate.set('');
    this.manualRegionId.set('');
    this.manualModality.set('Onsite');
    this.manualPlan.set('Quarterly');
    this.manualCurrentTerm.set(1);
    this.manualError.set(null);
    this.selectedFile.set(null);
    this.selectedFileName.set(null);
    this.importResult.set(null);
    this.importError.set(null);
    this.downloadingTemplate.set(false);
    this.templateError.set(null);
  }

  protected downloadTemplate(): void {
    if (this.downloadingTemplate()) return;

    this.templateError.set(null);
    this.downloadingTemplate.set(true);
    this.api
      .get<StudentImportTemplateResponseDto>('/students/import/template')
      .pipe(
        map((response) => ({ ok: true as const, response })),
        catchError((error) => of({ ok: false as const, error })),
      )
      .subscribe((result) => {
        this.downloadingTemplate.set(false);
        if (!result.ok) {
          this.templateError.set(result.error?.error?.detail ?? 'No se pudo descargar la plantilla.');
          return;
        }
        this.downloadFile(
          result.response.contentBase64,
          result.response.fileName,
          'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
        );
      });
  }

  protected onFileSelected(files: FileList | null): void {
    const file = files?.item(0) ?? null;
    this.selectedFile.set(file);
    this.selectedFileName.set(file?.name ?? null);
    this.importResult.set(null);
    this.importError.set(null);
  }

  protected submitManual(): void {
    this.manualError.set(null);

    if (!this.manualFullName().trim() || !this.manualBirthDate() || !this.manualRegionId()) {
      this.manualError.set('Completa todos los campos obligatorios.');
      return;
    }

    const body: CreateStudentRequestDto = {
      fullName: this.manualFullName().trim(),
      email: this.manualEmail().trim() || null,
      phone: this.manualPhone().trim() || null,
      birthDate: this.manualBirthDate(),
      regionId: this.manualRegionId(),
      modality: this.manualModality(),
      plan: this.manualPlan(),
      currentTerm: this.manualCurrentTerm(),
    };

    this.submitting.set(true);
    this.api
      .post<CreateStudentResponseDto>('/students', body)
      .pipe(
        map((response) => ({ ok: true as const, response })),
        catchError((error) => of({ ok: false as const, error })),
      )
      .subscribe((result) => {
        this.submitting.set(false);
        if (!result.ok) {
          this.manualError.set(result.error?.error?.detail ?? 'No se pudo agregar al alumno.');
          return;
        }
        this.toast.success(`Alumno agregado. Matrícula ${result.response.matricula}.`);
        this.added.emit();
        this.close.emit();
      });
  }

  protected submitImport(): void {
    const file = this.selectedFile();
    if (!file) return;

    this.importError.set(null);
    this.importResult.set(null);
    this.submitting.set(true);

    const formData = new FormData();
    formData.append('file', file);

    this.api
      .postForm<ImportStudentsResponseDto>('/students/import', formData)
      .pipe(
        map((response) => ({ ok: true as const, response })),
        catchError((error) => of({ ok: false as const, error })),
      )
      .subscribe((result) => {
        this.submitting.set(false);
        if (!result.ok) {
          this.importError.set(result.error?.error?.detail ?? 'No se pudo importar el archivo.');
          return;
        }
        this.importResult.set(result.response);
        if (result.response.importedRows > 0) {
          this.toast.success(`${result.response.importedRows} alumno(s) importado(s).`);
          this.added.emit();
        }
        if (result.response.errors.length > 0) {
          this.toast.error(`${result.response.errors.length} fila(s) con error — revisa el detalle.`);
        }
      });
  }

  /** Mismo patrón que payment-detail-drawer.downloadPdf: el archivo viaja en base64 dentro del JSON
   * (requiere Authorization, así que no puede ser un <a href> plano) y la descarga se dispara desde
   * JS creando un blob temporal. */
  private downloadFile(base64: string, fileName: string, contentType: string): void {
    const bytes = atob(base64);
    const buffer = new Uint8Array(bytes.length);
    for (let i = 0; i < bytes.length; i++) {
      buffer[i] = bytes.charCodeAt(i);
    }

    const blob = new Blob([buffer], { type: contentType });
    const url = URL.createObjectURL(blob);
    const anchor = document.createElement('a');
    anchor.href = url;
    anchor.download = fileName;
    anchor.click();
    URL.revokeObjectURL(url);
  }
}
