import { Component } from '@angular/core';
import { KardexViewComponent } from './kardex-view.component';

/** Plan de control escolar, fase 8: autoservicio — el alumno consulta/descarga/envía el suyo. */
@Component({
  selector: 'shk-my-kardex',
  imports: [KardexViewComponent],
  template: `
    <h1 class="text-2xl font-bold text-slate-900">Mi Kardex</h1>
    <div class="mt-6">
      <shk-kardex-view studentId="me" />
    </div>
  `,
})
export class MyKardexComponent {}
